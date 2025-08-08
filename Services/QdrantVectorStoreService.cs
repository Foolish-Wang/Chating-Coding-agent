#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Qdrant.Client;          // 仍保留（未来可恢复 gRPC）
using Qdrant.Client.Grpc;
using DotNetEnv;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Qdrant 向量存储服务（当前：仅 REST；gRPC 失败可后续再启用）
    /// </summary>
    public class QdrantVectorStoreService : IDisposable
    {
        private readonly QdrantClient? _client = null;   // 暂不使用（保留占位）
        private readonly HttpClient _http;
        private readonly string _host;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOpt = new() { PropertyNameCaseInsensitive = true };
        private readonly string _collectionName = "sk_agent_knowledge_base";

        public QdrantVectorStoreService()
        {
            LoadEnvIfNeeded();
            _host    = GetRequiredEnv("QDRANT_CLOUD_HOST");
            _apiKey  = GetRequiredEnv("QDRANT_CLOUD_API_KEY");
            _baseUrl = $"https://{_host}";
            Console.WriteLine($"🔗 使用 REST 方式连接 Qdrant: {_host}");

            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("api-key", _apiKey);
            _http.DefaultRequestHeaders.Add("User-Agent", "sk-agent/1.0");
        }

        private static void LoadEnvIfNeeded()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QDRANT_CLOUD_HOST"))) return;
            var local = Path.Combine(AppContext.BaseDirectory, ".env");
            var back  = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env"));
            if (File.Exists(local)) Env.Load(local);
            else if (File.Exists(back)) Env.Load(back);
        }

        private static string GetRequiredEnv(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException($"缺少环境变量: {key}");
            return v;
        }

        /// <summary>
        /// 简单连通性测试：GET /collections
        /// </summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/collections");
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ping 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取集合名称列表（REST）
        /// </summary>
        public async Task<IReadOnlyList<string>> ListCollectionsAsync()
        {
            var url = $"{_baseUrl}/collections";
            var httpResp = await _http.GetAsync(url);
            httpResp.EnsureSuccessStatusCode();
            using var stream = await httpResp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var result = new List<string>();
            if (doc.RootElement.TryGetProperty("result", out var r) &&
                r.TryGetProperty("collections", out var cols))
            {
                foreach (var item in cols.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp))
                        result.Add(nameProp.GetString()!);
                }
            }
            Console.WriteLine($"✅ REST 列表成功（{result.Count}）");
            return result;
        }

        /// <summary>
        /// 判断集合是否存在（REST）
        /// </summary>
        public async Task<bool> CollectionExistsAsync(string name)
        {
            var resp = await _http.GetAsync($"{_baseUrl}/collections/{name}");
            if (resp.IsSuccessStatusCode) return true;
            if ((int)resp.StatusCode == 404) return false;
            return false;
        }

        /// <summary>
        /// 创建默认集合（若不存在）
        /// </summary>
        public async Task EnsureDefaultCollectionAsync(int vectorSize = 1536, Distance distance = Distance.Cosine)
        {
            if (await CollectionExistsAsync(_collectionName))
            {
                Console.WriteLine($"ℹ️ 集合已存在：{_collectionName}");
                return;
            }

            var url = $"{_baseUrl}/collections/{_collectionName}";
            var payload = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = distance.ToString().ToLower()
                }
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await _http.PutAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ REST 创建集合成功：{_collectionName}");
        }

        /// <summary>
        /// 删除集合（REST）
        /// </summary>
        public async Task DeleteCollectionAsync(string name)
        {
            var resp = await _http.DeleteAsync($"{_baseUrl}/collections/{name}");
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ REST 删除集合：{name}");
        }

        /// <summary>
        /// 向集合中批量写入点（向量 + 载荷），若集合不存在可先调用 EnsureDefaultCollectionAsync
        /// REST: POST /collections/{collection}/points
        /// </summary>
        public async Task UpsertPointsAsync(
            string collectionName,
            IEnumerable<(string Id, float[] Vector, Dictionary<string, object> Payload)> points)
        {
            var pointsArray = new List<object>();
            foreach (var p in points)
            {
                pointsArray.Add(new
                {
                    id = p.Id,
                    vector = p.Vector,
                    payload = p.Payload
                });
            }

            var body = new
            {
                points = pointsArray
            };

            var json = JsonSerializer.Serialize(body);
            var resp = await _http.PostAsync(
                $"{_baseUrl}/collections/{collectionName}/points",
                new StringContent(json, Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ 写入 {pointsArray.Count} 个向量到集合 {collectionName}");
        }

        /// <summary>
        /// 直接对文档分块及对应向量批量写入（对齐顺序）
        /// </summary>
        public async Task IngestDocumentChunksAsync(
            string collectionName,
            string sourceFile,
            IList<SemanticKernelAgent.Models.DocumentChunk> chunks,
            IList<float[]> vectors)
        {
            if (chunks.Count != vectors.Count)
                throw new InvalidOperationException("chunks 数量与 vectors 数量不匹配");

            var batch = new List<(string Id, float[] Vector, Dictionary<string, object> Payload)>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                batch.Add((
                    Id: c.Id ?? SemanticKernelAgent.Services.RagUtils.GenerateChunkId(sourceFile, c.ChunkIndex),
                    Vector: vectors[i],
                    Payload: new Dictionary<string, object>
                    {
                        ["text"] = c.Content,
                        ["chunk_index"] = c.ChunkIndex,
                        ["source"] = sourceFile,
                        ["start"] = c.StartPosition,
                        ["end"] = c.EndPosition
                    }
                ));
            }

            await UpsertPointsAsync(collectionName, batch);
        }

        public void Dispose()
        {
            _http.Dispose();
            _client?.Dispose();
        }
    }
}

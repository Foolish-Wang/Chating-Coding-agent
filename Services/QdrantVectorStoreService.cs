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
using System.Linq;

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
        /// 创建默认集合（若不存在）(旧方法保留但不再直接用于动态维度场景)
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
                    distance = distance.ToString() // 使用 Qdrant API 大小写：Cosine / Dot / Euclid
                }
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await _http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ 创建集合失败 HTTP {(int)resp.StatusCode}: {resp.StatusCode}");
                Console.WriteLine($"📄 返回内容: {body}");
            }
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ 创建集合成功：{_collectionName} (size={vectorSize})");
        }

        /// <summary>
        /// 根据向量真实维度确保集合存在（推荐）
        /// </summary>
        public async Task EnsureCollectionForVectorSizeAsync(int vectorSize, string? collectionName = null, Distance distance = Distance.Cosine)
        {
            var name = collectionName ?? _collectionName;
            if (await CollectionExistsAsync(name))
            {
                Console.WriteLine($"ℹ️ 集合已存在：{name}");
                return;
            }
            var url = $"{_baseUrl}/collections/{name}";
            var payload = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = distance.ToString()
                }
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await _http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ 创建集合失败 HTTP {(int)resp.StatusCode}: {resp.StatusCode}");
                Console.WriteLine($"📄 返回内容: {body}");
            }
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ 创建集合成功：{name} (size={vectorSize})");
        }

        /// <summary>
        /// 批量写入向量
        /// </summary>
        public async Task UpsertPointsAsync(
            string collectionName,
            IEnumerable<(string Id, float[] Vector, Dictionary<string, object> Payload)> points)
        {
            var list = points.ToList();
            if (list.Count == 0) return;

            int dim = list[0].Vector.Length;
            // 可选：检查集合是否存在；若不存在提醒
            if (!await CollectionExistsAsync(collectionName))
            {
                throw new InvalidOperationException($"集合 {collectionName} 不存在，请先调用 EnsureCollectionForVectorSizeAsync({dim}).");
            }

            var body = new
            {
                points = list.Select(p => new
                {
                    id = p.Id,
                    vector = p.Vector,
                    payload = p.Payload
                })
            };

            var json = JsonSerializer.Serialize(body);
            var resp = await _http.PostAsync(
                $"{_baseUrl}/collections/{collectionName}/points",
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Upsert 失败 HTTP {(int)resp.StatusCode}: {resp.StatusCode}");
                Console.WriteLine($"📄 返回: {err}");
            }
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ 写入 {list.Count} 个向量 (dim={dim}) 到 {collectionName}");
        }

        /// <summary>
        /// 文档分块入库
        /// </summary>
        public async Task IngestDocumentChunksAsync(
            string collectionName,
            string sourceFile,
            IList<SemanticKernelAgent.Models.DocumentChunk> chunks,
            IList<float[]> vectors)
        {
            if (chunks.Count != vectors.Count)
                throw new InvalidOperationException("chunks 与 vectors 数量不一致");

            var batch = new List<(string Id, float[] Vector, Dictionary<string, object> Payload)>(chunks.Count);
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                batch.Add((
                    Id: $"{sourceFile}:{c.ChunkIndex}",
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

        public string DefaultCollectionName => _collectionName;

        /// <summary>
        /// 删除指定集合（REST）
        /// </summary>
        public async Task DeleteCollectionAsync(string name)
        {
            var resp = await _http.DeleteAsync($"{_baseUrl}/collections/{name}");
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ 删除集合失败 {name}: {(int)resp.StatusCode} {resp.StatusCode}");
                Console.WriteLine($"📄 返回: {body}");
            }
            resp.EnsureSuccessStatusCode();
            Console.WriteLine($"✅ 已删除集合：{name}");
        }

        /// <summary>
        /// 删除默认集合（便于测试）
        /// </summary>
        public Task DeleteCollectionAsync() => DeleteCollectionAsync(_collectionName);

        public void Dispose()
        {
            _http.Dispose();
            _client?.Dispose();
        }
    }
}

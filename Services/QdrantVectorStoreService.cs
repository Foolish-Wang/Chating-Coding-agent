using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using DotNetEnv;
using System.Reflection;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Qdrant 向量存储服务
    /// </summary>
    public class QdrantVectorStoreService
    {
        private readonly QdrantClient _client;
        private readonly string _collectionName;

        public QdrantVectorStoreService()
        {
            // 从环境变量加载配置
            Env.Load();
            
            var endpoint = Environment.GetEnvironmentVariable("QDRANT_CLOUD_ENDPOINT");
            var apiKey = Environment.GetEnvironmentVariable("QDRANT_CLOUD_API_KEY");
            
            // 解析主机名（去掉 https:// 前缀）
            var host = endpoint?.Replace("https://", "").Replace("http://", "");
            
            Console.WriteLine($"🔗 连接 Qdrant: {host}");
            
            _client = new QdrantClient(
                host: host,
                https: true,
                apiKey: apiKey
            );
            
            _collectionName = "sk_agent_knowledge_base";
        }

        /// <summary>
        /// 列出所有集合
        /// </summary>
        public async Task<IEnumerable<string>> ListCollectionsAsync()
        {
            try
            {
                Console.WriteLine("📋 获取 Qdrant 集合列表...");
                var collections = await _client.ListCollectionsAsync();
                
                // 根据官方文档，ListCollectionsAsync 返回 string 列表
                var collectionNames = collections.ToList();
                Console.WriteLine($"✅ 找到 {collectionNames.Count} 个集合: {string.Join(", ", collectionNames)}");
                
                return collectionNames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取集合列表失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建集合
        /// </summary>
        public async Task CreateCollectionAsync(string collectionName = null, int vectorSize = 1024)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"🏗️ 创建集合: {targetCollection} (向量维度: {vectorSize})");
                
                // 检查集合是否已存在
                var collections = await ListCollectionsAsync();
                if (collections.Contains(targetCollection))
                {
                    Console.WriteLine($"ℹ️ 集合 {targetCollection} 已存在，跳过创建");
                    return;
                }
                
                // 修复：直接使用 VectorParams 而不是 VectorsConfig
                await _client.CreateCollectionAsync(
                    collectionName: targetCollection,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)vectorSize,
                        Distance = Distance.Cosine
                    }
                );
                
                Console.WriteLine($"✅ 集合 {targetCollection} 创建成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 创建集合失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 插入向量
        /// </summary>
        public async Task InsertVectorAsync(string id, float[] vector, Dictionary<string, object> metadata, string collectionName = null)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"💾 插入向量到集合 {targetCollection}: ID={id}, 维度={vector.Length}");
                
                // 确保集合存在
                await CreateCollectionAsync(targetCollection, vector.Length);
                
                // 构建元数据
                var payload = new Dictionary<string, Value>();
                foreach (var (key, value) in metadata)
                {
                    payload[key] = value switch
                    {
                        string s => new Value { StringValue = s },
                        int i => new Value { IntegerValue = i },
                        long l => new Value { IntegerValue = l },
                        double d => new Value { DoubleValue = d },
                        float f => new Value { DoubleValue = f },
                        bool b => new Value { BoolValue = b },
                        _ => new Value { StringValue = value?.ToString() ?? "" }
                    };
                }
                
                // 构建点数据
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = id },
                    Vectors = vector,
                    Payload = { payload }
                };
                
                // 插入向量 - 根据官方文档的 UpsertAsync API
                await _client.UpsertAsync(
                    collectionName: targetCollection,
                    points: new[] { point }
                );
                
                Console.WriteLine($"✅ 向量插入成功: {id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 插入向量失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 批量插入向量
        /// </summary>
        public async Task BatchInsertVectorsAsync(List<(string Id, float[] Vector, Dictionary<string, object> Metadata)> vectorData, string collectionName = null)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"📦 批量插入 {vectorData.Count} 个向量到集合 {targetCollection}");
                
                if (vectorData.Count == 0) return;
                
                // 确保集合存在
                await CreateCollectionAsync(targetCollection, vectorData[0].Vector.Length);
                
                // 构建点数据
                var points = new List<PointStruct>();
                
                foreach (var (id, vector, metadata) in vectorData)
                {
                    var payload = new Dictionary<string, Value>();
                    foreach (var (key, value) in metadata)
                    {
                        payload[key] = value switch
                        {
                            string s => new Value { StringValue = s },
                            int i => new Value { IntegerValue = i },
                            long l => new Value { IntegerValue = l },
                            double d => new Value { DoubleValue = d },
                            float f => new Value { DoubleValue = f },
                            bool b => new Value { BoolValue = b },
                            _ => new Value { StringValue = value?.ToString() ?? "" }
                        };
                    }
                    
                    points.Add(new PointStruct
                    {
                        Id = new PointId { Uuid = id },
                        Vectors = vector,
                        Payload = { payload }
                    });
                }
                
                // 批量插入
                await _client.UpsertAsync(
                    collectionName: targetCollection,
                    points: points
                );
                
                Console.WriteLine($"✅ 批量插入完成: {vectorData.Count} 个向量");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 批量插入失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 搜索相似向量
        /// </summary>
        public async Task<List<(string Id, float Score, Dictionary<string, object> Metadata)>> SearchAsync(
            float[] queryVector, int limit = 10, string collectionName = null)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"🔍 搜索相似向量: 集合={targetCollection}, 限制={limit}, 查询向量维度={queryVector.Length}");
                
                // 根据官方文档的 SearchAsync API
                var searchResult = await _client.SearchAsync(
                    collectionName: targetCollection,
                    vector: queryVector,
                    limit: (ulong)limit,
                    payloadSelector: new WithPayloadSelector { Enable = true }
                );
                
                var results = new List<(string Id, float Score, Dictionary<string, object> Metadata)>();
                
                foreach (var point in searchResult)
                {
                    var metadata = new Dictionary<string, object>();
                    foreach (var kvp in point.Payload)
                    {
                        var key = kvp.Key;
                        var value = kvp.Value;
                        
                        metadata[key] = value.KindCase switch
                        {
                            Value.KindOneofCase.StringValue => value.StringValue,
                            Value.KindOneofCase.IntegerValue => value.IntegerValue,
                            Value.KindOneofCase.DoubleValue => value.DoubleValue,
                            Value.KindOneofCase.BoolValue => value.BoolValue,
                            _ => value.ToString()
                        };
                    }
                    
                    results.Add((point.Id.Uuid, point.Score, metadata));
                }
                
                Console.WriteLine($"✅ 搜索完成: 找到 {results.Count} 个相似结果");
                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 搜索失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 删除集合
        /// </summary>
        public async Task DeleteCollectionAsync(string collectionName = null)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"🗑️ 删除集合: {targetCollection}");
                
                await _client.DeleteCollectionAsync(targetCollection);
                Console.WriteLine($"✅ 集合 {targetCollection} 删除成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 删除集合失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取集合信息
        /// </summary>
        public async Task<CollectionInfo> GetCollectionInfoAsync(string collectionName = null)
        {
            var targetCollection = collectionName ?? _collectionName;
            
            try
            {
                Console.WriteLine($"ℹ️ 获取集合信息: {targetCollection}");
                
                var info = await _client.GetCollectionInfoAsync(targetCollection);
                Console.WriteLine($"✅ 集合信息获取成功: 向量数量={info.PointsCount}");
                
                return info;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取集合信息失败: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}

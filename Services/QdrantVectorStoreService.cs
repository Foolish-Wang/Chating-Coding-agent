using Google.Protobuf.WellKnownTypes; // 需要添加此命名空间
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkAgent.Services
{
    public class QdrantVectorStoreService
    {
        private readonly QdrantClient _client;

        public QdrantVectorStoreService(string host = "localhost", int port = 6334, string apiKey = null)
        {
            var address = $"http://{host}:{port}";
            if (!string.IsNullOrEmpty(apiKey))
            {
                var channel = QdrantChannel.ForAddress(address, new ClientConfiguration
                {
                    ApiKey = apiKey
                });
                var grpcClient = new QdrantGrpcClient(channel);
                _client = new QdrantClient(grpcClient);
            }
            else
            {
                _client = new QdrantClient(host, port);
            }
        }

        /// <summary>
        /// 创建集合
        /// </summary>
        public async Task CreateCollectionAsync(string collectionName, int vectorSize, Distance distance = Distance.Cosine)
        {
            await _client.CreateCollectionAsync(collectionName, new VectorParams
            {
                Size = (ulong)vectorSize, // 强制转换
                Distance = distance
            });
        }

        /// <summary>
        /// 插入向量
        /// </summary>
        public async Task UpsertVectorsAsync(string collectionName, List<float[]> vectors, List<Dictionary<string, object>> payloads = null)
        {
            var points = vectors.Select((v, i) =>
            {
                var point = new PointStruct
                {
                    Id = (ulong)(i + 1),
                    Vectors = v
                };
                if (payloads != null && payloads.Count > i && payloads[i] != null)
                {
                    foreach (var kv in payloads[i])
                    {
                        // 明确指定 Qdrant.Client.Grpc.Value
                        point.Payload.Add(kv.Key, Qdrant.Client.Grpc.Value.For(kv.Value));
                    }
                }
                return point;
            }).ToList();

            await _client.UpsertAsync(collectionName, points);
        }

        /// <summary>
        /// 搜索相似向量
        /// </summary>
        public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(string collectionName, float[] queryVector, int limit = 5)
        {
            var results = await _client.SearchAsync(collectionName, queryVector, limit: (ulong)limit);
            return results;
        }

        /// <summary>
        /// 获取所有集合名
        /// </summary>
        public async Task<IReadOnlyList<string>> ListCollectionsAsync()
        {
            var collections = await _client.ListCollectionsAsync();
            return collections;
        }

        /// <summary>
        /// 删除集合
        /// </summary>
        public async Task DeleteCollectionAsync(string collectionName)
        {
            await _client.DeleteCollectionAsync(collectionName);
        }

        /// <summary>
        /// 获取集合信息
        /// </summary>
        public async Task<object> GetCollectionInfoAsync(string collectionName)
        {
            var info = await _client.GetCollectionInfoAsync(collectionName);
            return info;
        }
    }
}


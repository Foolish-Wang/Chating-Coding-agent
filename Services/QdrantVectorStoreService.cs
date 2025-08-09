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
        private readonly string _collectionName;
        private readonly int _vectorSize;

        public QdrantVectorStoreService(string host = "localhost", int port = 6334, string collectionName = "text_embedding", int vectorSize = 1536)
        {
            _client = new QdrantClient(host, port);
            _collectionName = collectionName;
            _vectorSize = vectorSize;
        }

        public async Task EnsureCollectionAsync()
        {
            var exists = await _client.CollectionExistsAsync(_collectionName);
            if (!exists)
            {
                await _client.CreateCollectionAsync(_collectionName, new VectorParams
                {
                    Size = (uint)_vectorSize,
                    Distance = Distance.Cosine
                });
            }
        }

        public async Task InsertTextsAsync(IEnumerable<(string Category, string Text)> items, Func<string, float[]> embeddingFunc)
        {
            ulong i = 0;
            var points = items.Select(item =>
            {
                return new PointStruct
                {
                    Id = i++,
                    Vectors = embeddingFunc(item.Text),
                    Payload =
                    {
                        ["catg"] = item.Category,
                        ["text"] = item.Text
                    }
                };
            }).ToList();

            await _client.UpsertAsync(_collectionName, points);
        }

        public async Task<List<(double Score, string Category, string Text)>> SearchAsync(string query, Func<string, float[]> embeddingFunc, int limit = 5)
        {
            var queryVector = embeddingFunc(query);
            var results = await _client.SearchAsync(_collectionName, queryVector, limit: (uint)limit);

            return results.Select(p =>
                ((double)p.Score, p.Payload["catg"].StringValue, p.Payload["text"].StringValue)
            ).ToList();
        }

        public async Task<List<(double Score, string Text)>> SearchByCategoryAsync(string query, string category, Func<string, float[]> embeddingFunc, int limit = 3)
        {
            var queryVector = embeddingFunc(query);
            var filter = Conditions.MatchText("catg", category);
            var results = await _client.SearchAsync(_collectionName, queryVector, filter: filter, limit: (uint)limit);

            return results.Select(p =>
                ((double)p.Score, p.Payload["text"].StringValue)
            ).ToList();
        }

        //后续增加的
        public async Task<IReadOnlyList<string>> ListCollectionsAsync()
        {
            return await _client.ListCollectionsAsync();
        }

        public async Task DeleteCollectionAsync(string collectionName)
        {
            await _client.DeleteCollectionAsync(collectionName);
        }
    }
}
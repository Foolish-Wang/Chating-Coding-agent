using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Infini-AI rerank服务：调用云端API获取相关性分数
    /// </summary>
    public class RerankService
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _model;

        public RerankService()
        {
            _endpoint = Environment.GetEnvironmentVariable("DEFAULT_BASE_URL")?.TrimEnd('/') + "/rerank";
            _apiKey = Environment.GetEnvironmentVariable("API_KEY");
            _model = Environment.GetEnvironmentVariable("RERANK_MODEL");
        }

        public async Task<List<(T Document, float Score)>> RerankAsync<T>(string query, List<T> documents) where T : class
        {
            if (documents == null || documents.Count == 0)
                return new List<(T, float)>();

            var docContents = new List<string>();
            foreach (var doc in documents)
            {
                docContents.Add(GetDocumentContent(doc));
            }

            var payload = new
            {
                model = _model,
                query = query,
                documents = docContents
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await client.PostAsync(
                _endpoint,
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            );
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            using var docJson = JsonDocument.Parse(result);
            var scores = new List<float>();
            foreach (var item in docJson.RootElement.GetProperty("results").EnumerateArray())
            {
                scores.Add(item.GetProperty("score").GetSingle());
            }

            var resultList = new List<(T, float)>();
            for (int i = 0; i < Math.Min(documents.Count, scores.Count); i++)
            {
                resultList.Add((documents[i], scores[i]));
            }

            // 按分数降序排序
            resultList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return resultList;
        }

        private string GetDocumentContent<T>(T document)
        {
            var contentProperty = typeof(T).GetProperty("Content");
            if (contentProperty != null)
                return contentProperty.GetValue(document)?.ToString() ?? "";
            return document?.ToString() ?? "";
        }
    }
}
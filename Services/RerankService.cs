using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

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
                documents = docContents,
                return_documents = true
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

            if (!docJson.RootElement.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("Rerank API response: " + result);
                throw new Exception("Rerank API response does not contain 'results' array.");
            }

            // 解析 index 和 relevance_score
            var scoredList = new List<(int Index, float Score)>();
            foreach (var item in resultsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("index", out var idxElem) || !item.TryGetProperty("relevance_score", out var scoreElem))
                {
                    Console.WriteLine("Rerank API item: " + item.ToString());
                    throw new Exception("Rerank API result item does not contain 'index' or 'relevance_score'.");
                }
                int idx = idxElem.GetInt32();
                float score = scoreElem.GetSingle();
                scoredList.Add((idx, score));
            }

            // 按分数降序排序，返回文档和分数
            var resultList = new List<(T, float)>();
            foreach (var pair in scoredList.OrderByDescending(x => x.Score))
            {
                int idx = pair.Index;
                float score = pair.Score;
                if (idx >= 0 && idx < documents.Count)
                    resultList.Add((documents[idx], score));
            }

            // 新增：打印重排后获取的文档块数量及每个文档块的开头和结尾
            int topM = 3; // 或通过参数传入
            Console.WriteLine($"[RerankService] 重排后共获取到 {resultList.Count} 个文档块，实际取前 {topM} 个用于拼接。");
            for (int i = 0; i < Math.Min(topM, resultList.Count); i++)
            {
                var (doc, score) = resultList[i];
                string content = GetDocumentContent(doc);
                string start = content.Length > 30 ? content.Substring(0, 30) : content;
                string end = content.Length > 30 ? content.Substring(content.Length - 30) : content;
                Console.WriteLine($"[{i + 1}] 分数: {score:0.0000} 开头: {start} ... 结尾: {end}");
            }

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
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Ollama rerank服务：用chat/generate接口让模型输出分数
    /// </summary>
    public class OllamaRerankService
    {
        private readonly string _endpoint;
        private readonly string _model;

        public OllamaRerankService()
        {
            _endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
            _model = Environment.GetEnvironmentVariable("OLLAMA_RERANK_MODEL");
        }

        public async Task<List<(T Document, float Score)>> RerankAsync<T>(string query, List<T> documents) where T : class
        {
            if (documents == null || documents.Count == 0)
                return new List<(T, float)>();

            // 构造 prompt，让模型输出每个文档的相关性分数
            var sb = new StringBuilder();
            sb.AppendLine($"请根据查询“{query}”对下列文档相关性打分（0-1之间的小数，1为最相关），输出格式为一行一个分数：");
            for (int i = 0; i < documents.Count; i++)
            {
                sb.AppendLine($"[{i + 1}] {GetDocumentContent(documents[i])}");
            }
            sb.AppendLine("请只输出分数，每行一个。");

            var payload = new
            {
                model = _model,
                prompt = sb.ToString(),
                stream = false
            };

            using var client = new HttpClient();
            var response = await client.PostAsync(
                $"{_endpoint.TrimEnd('/')}/api/generate",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            );
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(result).RootElement;
            var responseText = json.GetProperty("response").GetString();

            // 解析分数
            var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var scores = new List<float>();
            foreach (var line in lines)
            {
                if (float.TryParse(line.Trim(), out var score))
                    scores.Add(score);
            }

            // 关联文档和分数
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
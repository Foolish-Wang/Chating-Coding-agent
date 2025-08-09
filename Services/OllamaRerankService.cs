using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Extensions.AI;
using DotNetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SemanticKernelAgent.Models;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Ollama 重排序服务
    /// </summary>
    public class OllamaRerankService
    {
        private readonly IChatClient _chatClient;
        private readonly string _modelId;
        
        public OllamaRerankService()
        {
            // 加载 .env 文件
            Env.Load();
            
            var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
            _modelId = Environment.GetEnvironmentVariable("OLLAMA_RERANK_MODEL");
            
            try
            {
                // 创建 Kernel 并使用新的 API 添加 Ollama 聊天客户端
                var builder = Kernel.CreateBuilder();
                builder.AddOllamaChatCompletion(_modelId, new Uri(endpoint));
                var kernel = builder.Build();
                
                // 获取聊天客户端
                _chatClient = kernel.GetRequiredService<IChatClient>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ollama 重排序服务初始化失败: {ex.Message}");
                _chatClient = null;
            }
        }

        /// <summary>
        /// 根据查询对文档块进行重排序
        /// </summary>
        /// <param name="query">查询文本，由调用方传入</param>
        /// <param name="documents">待排序的文档块</param>
        /// <returns>按相关性排序的文档块</returns>
        public async Task<List<(string document, double score)>> RerankAsync(string query, List<string> documents)
        {
            if (_chatClient == null || documents?.Any() != true)
                return new List<(string document, double score)>();

            var results = new List<(string document, double score)>();
            foreach (var document in documents)
            {
                var prompt = $@"请根据以下查询和文档的相关性进行评分：

查询: {query}

文档: {document}

请仅返回一个0-1之间的相关性分数，不要包含任何其他文字或解释。";

                var chatHistory = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, prompt)
                };
                
                var response = await _chatClient.CompleteAsync(chatHistory);
                var content = response.Message.Text?.Trim() ?? "0";
                
                if (double.TryParse(content, out double score))
                {
                    score = Math.Max(0, Math.Min(1, score));
                    results.Add((document, score));
                }
                else
                {
                    results.Add((document, 0.0));
                }
            }
            return results.OrderByDescending(r => r.score).ToList();
        }

        /// <summary>
        /// 根据查询对检索结果进行重排序
        /// </summary>
        /// <param name="query">查询文本，由调用方传入</param>
        /// <param name="searchResults">检索结果</param>
        /// <returns>按相关性排序的检索结果</returns>
        public async Task<List<RerankResult>> RerankSearchResultsAsync(string query, List<SearchResult> searchResults)
        {
            if (searchResults?.Any() != true)
                return new List<RerankResult>();

            var documents = searchResults.Select(sr => sr.Content).ToList();
            var reranked = await RerankAsync(query, documents);

            var results = new List<RerankResult>();
            for (int i = 0; i < searchResults.Count; i++)
            {
                var original = searchResults[i];
                var rerankedItem = reranked.FirstOrDefault(r => r.document == original.Content);

                results.Add(new RerankResult
                {
                    ChunkId = original.ChunkId,
                    Content = original.Content,
                    OriginalScore = original.Score,
                    RerankScore = (float)rerankedItem.score,
                    Metadata = original.Metadata
                });
            }
            return results.OrderByDescending(r => r.RerankScore).ToList();
        }
    }
}
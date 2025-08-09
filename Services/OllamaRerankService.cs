using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Extensions.AI;
using DotNetEnv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Ollama 重排序服务
    /// </summary>
    public class OllamaRerankService
    {
        private readonly IChatCompletionService _chatService;
        
        public OllamaRerankService()
        {
            // 加载 .env 文件
            Env.Load();
            
            var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
            var modelId = Environment.GetEnvironmentVariable("OLLAMA_RERANK_MODEL");
            
            // 创建 Kernel 并添加 Ollama 聊天服务
            var builder = Kernel.CreateBuilder();
            builder.AddOllamaChatCompletion(modelId, new Uri(endpoint));
            var kernel = builder.Build();
            
            _chatService = kernel.GetRequiredService<IChatCompletionService>();
        }

        /// <summary>
        /// 对文档块进行重排序
        /// </summary>
        /// <param name="query">查询文本</param>
        /// <param name="documents">待重排序的文档块列表</param>
        /// <returns>重排序后的文档块列表</returns>
        public async Task<List<T>> RerankAsync<T>(string query, List<T> documents) where T : class
        {
            if (documents == null || documents.Count == 0)
            {
                return new List<T>();
            }

            var scoredDocuments = new List<(T Document, double Score)>();
            
            foreach (var doc in documents)
            {
                // 获取文档内容（假设文档对象有Content属性或ToString方法）
                var content = GetDocumentContent(doc);
                
                // 创建重排序提示
                var prompt = $"Query: {query}\nPassage: {content}\nRelevant:";
                
                // 调用模型获取相关性分数
                var response = await _chatService.GetChatMessageContentAsync(prompt);
                var scoreText = response.Content?.Trim();
                
                // 解析分数
                if (double.TryParse(scoreText, out double score))
                {
                    scoredDocuments.Add((doc, score));
                }
                else
                {
                    scoredDocuments.Add((doc, 0.0));
                }
            }
            
            // 按分数降序排序并返回文档列表
            return scoredDocuments.OrderByDescending(x => x.Score)
                                 .Select(x => x.Document)
                                 .ToList();
        }

        /// <summary>
        /// 获取文档内容
        /// </summary>
        private string GetDocumentContent<T>(T document)
        {
            // 尝试通过反射获取Content属性
            var contentProperty = typeof(T).GetProperty("Content");
            if (contentProperty != null)
            {
                return contentProperty.GetValue(document)?.ToString() ?? "";
            }
            
            // 如果没有Content属性，使用ToString方法
            return document?.ToString() ?? "";
        }
    }
}
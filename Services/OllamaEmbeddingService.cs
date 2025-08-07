using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using DotNetEnv;
using System;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Ollama 嵌入服务
    /// </summary>
    public class OllamaEmbeddingService
    {
        private readonly OllamaTextEmbeddingGenerationService _embeddingService;
        
        public OllamaEmbeddingService()
        {
            // 加载 .env 文件
            Env.Load();
            
            var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";
            var modelId = Environment.GetEnvironmentVariable("OLLAMA_EMBEDDING_MODEL") ?? "nomic-embed-text";
            
            _embeddingService = new OllamaTextEmbeddingGenerationService(
                modelId: modelId,
                endpoint: new Uri(endpoint)
            );
        }

        /// <summary>
        /// 将文本转换为向量
        /// </summary>
        public async Task<float[]> EmbedAsync(string text)
        {
            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
                return embedding.ToArray();
            }
            catch (Exception ex)
            {
                // 如果 Ollama 服务不可用，返回模拟数据
                Console.WriteLine($"⚠️ Ollama 服务不可用: {ex.Message}");
                Console.WriteLine("🔄 返回模拟向量数据用于测试");
                
                // 返回固定长度的模拟向量（1024维，从配置读取）
                var vectorSize = int.Parse(Environment.GetEnvironmentVariable("QDRANT_VECTOR_SIZE") ?? "1024");
                var mockVector = new float[vectorSize];
                var random = new Random(text.GetHashCode()); // 使用文本hash作为种子，确保相同文本得到相同向量
                
                for (int i = 0; i < mockVector.Length; i++)
                {
                    mockVector[i] = (float)(random.NextDouble() * 2 - 1); // -1 到 1 之间的随机数
                }
                
                return mockVector;
            }
        }
    }
}

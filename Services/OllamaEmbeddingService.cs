using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.Extensions.AI;
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
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        
        public OllamaEmbeddingService()
        {
            // 加载 .env 文件
            Env.Load();
            
            var endpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
            var modelId = Environment.GetEnvironmentVariable("OLLAMA_EMBEDDING_MODEL");
            
            try
            {
                // 创建 Kernel 并使用新的 API 添加 Ollama 嵌入服务
                var builder = Kernel.CreateBuilder();
                builder.AddOllamaEmbeddingGenerator(modelId, new Uri(endpoint));
                var kernel = builder.Build();
                
                // 获取新的嵌入生成器
                _embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            }
            catch
            {
                // 如果初始化失败，设为 null，后续会使用模拟数据
                _embeddingGenerator = null;
            }
        }

        /// <summary>
        /// 将文本转换为向量
        /// </summary>
        public async Task<float[]> EmbedAsync(string text)
        {
            if (_embeddingGenerator != null)
            {
                try
                {
                    
                    var embedding = await _embeddingGenerator.GenerateAsync(text);
                    return embedding.Vector.ToArray();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ollama 服务调用失败: {ex.Message}");
                }
            }
            
            // 返回模拟数据
            Console.WriteLine("🔄 返回模拟向量数据用于测试");
            
            var vectorSize = int.Parse(Environment.GetEnvironmentVariable("QDRANT_VECTOR_SIZE") ?? "1024");
            var mockVector = new float[vectorSize];
            var random = new Random(text.GetHashCode());
            
            for (int i = 0; i < mockVector.Length; i++)
            {
                mockVector[i] = (float)(random.NextDouble() * 2 - 1);
            }
            
            return mockVector;
        }
    }
}

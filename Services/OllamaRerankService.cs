using System;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// Ollama 重排序服务
    /// </summary>
    public class OllamaRerankService
    {
        private readonly string _endpoint;
        private readonly string _modelId;

        public OllamaRerankService(string endpoint = "http://localhost:11434", string modelId = "qwen2.5-rerank")
        {
            _endpoint = endpoint;
            _modelId = modelId;
        }

        /// <summary>
        /// 重排序文档块
        /// </summary>
        public async Task<float> RerankAsync(string query, string document)
        {
            // TODO: 实现重排序功能
            await Task.Delay(50); // 临时占位
            return 0.8f; // 临时返回
        }
    }
}

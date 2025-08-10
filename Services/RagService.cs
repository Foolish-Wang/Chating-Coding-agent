using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkAgent.Services;

namespace SemanticKernelAgent.Services
{
    public class RagService
    {
        private readonly DocumentProcessor _processor = new DocumentProcessor();
        private readonly DocumentChunker _chunker = new DocumentChunker();
        private readonly OllamaEmbeddingService _embedder = new OllamaEmbeddingService();
        private readonly QdrantVectorStoreService _qdrant;
        private readonly RerankService _reranker = new RerankService();

        public RagService()
        {
            var collectionName = "sk_agent_knowledge_base";
            var vectorSize = 1024;
            _qdrant = new QdrantVectorStoreService("localhost", 6334, collectionName, vectorSize);
        }

        /// <summary>
        /// 执行RAG流程，返回拼接后的大文档块
        /// </summary>
        public async Task<string> RunAsync(string query)
        {
            Console.WriteLine("📄 文档加载 + 分块 + 向量化测试开始");

            await _qdrant.EnsureCollectionAsync();

            var documents = await _processor.LoadKnowledgeBaseDocumentsAsync();
            if (documents == null || documents.Count == 0)
            {
                Console.WriteLine("⚠️ 未找到任何文档，跳过流程");
                return string.Empty;
            }

            Console.WriteLine($"📚 共找到 {documents.Count} 个文档");

            foreach (var doc in documents)
            {
                var chunkResult = _chunker.ChunkDocument(doc);
                if (!chunkResult.Success || chunkResult.Chunks == null || chunkResult.Chunks.Count == 0)
                {
                    Console.WriteLine("⚠️ 分块结果为空，跳过此文档");
                    continue;
                }

                foreach (var chunk in chunkResult.Chunks)
                {
                    var vector = await _embedder.EmbedAsync(chunk.Content);
                }

                try
                {
                    var items = chunkResult.Chunks.Select(chunk => (Category: doc.FileName, Text: chunk.Content));
                    await _qdrant.InsertTextsAsync(items, text => _embedder.EmbedAsync(text).Result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 写入 Qdrant 失败（文档: {doc.FileName}）: {ex.Message}");
                }
            }

            Console.WriteLine("✅ 文档处理与向量化测试完成");

            // 查询 topK
            var topKStr = Environment.GetEnvironmentVariable("SEARCH_TOP_K");
            int topK = 5;
            if (!string.IsNullOrWhiteSpace(topKStr) && int.TryParse(topKStr, out var k))
                topK = k;

            var searchResults = await _qdrant.SearchAsync(query, text => _embedder.EmbedAsync(text).Result, topK);

            // rerank
            var topMStr = Environment.GetEnvironmentVariable("SEARCH_RERANK_TOP_M");
            int topM = 5;
            if (!string.IsNullOrWhiteSpace(topMStr) && int.TryParse(topMStr, out var m))
                topM = m;

            var docBlocks = searchResults
                .Select(r => new { Category = r.Category, Content = r.Text })
                .ToList();

            var rerankResults = await _reranker.RerankAsync(query, docBlocks);

            var mergedContent = "";
            for (int i = 0; i < Math.Min(topM, rerankResults.Count); i++)
            {
                var (block, score) = rerankResults[i];
                mergedContent += block.Content;
            }

            return mergedContent;
        }
    }
}
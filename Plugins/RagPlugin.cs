using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SemanticKernelAgent.Services;
using SkAgent.Services;

namespace SemanticKernelAgent.Plugins
{
    /// <summary>
    /// RAG 插件
    /// </summary>
    public class RagPlugin
    {
        // TODO: 实现 RAG 相关的插件功能

        [KernelFunction, Description("搜索知识库相关内容")]
        public async Task<string> SearchKnowledgeBase(
            [Description("搜索查询")] string query,
            [Description("返回结果数量")] int limit = 5)
        {
            // TODO: 实现知识库搜索功能
            await Task.Delay(100);
            return $"搜索结果: {query}";
        }

        [KernelFunction, Description("向知识库添加文档")]
        public async Task<string> AddDocumentToKnowledgeBase(
            [Description("文档内容")] string content,
            [Description("文档标题")] string title = "")
        {
            // TODO: 实现文档添加功能
            await Task.Delay(100);
            return "文档已添加到知识库";
        }

        public async Task RunAsync()
        {
            Console.WriteLine("📄 文档加载 + 分块 + 向量化测试开始");

            var processor = new DocumentProcessor();
            var chunker = new DocumentChunker();
            var embedder = new OllamaEmbeddingService();
            string query = "夏亚驾驶哪个机动战士踢中了大黄蜂？";

            var collectionName = "sk_agent_knowledge_base";
            var vectorSize = 1024;
            var qdrant = new QdrantVectorStoreService("localhost", 6334, collectionName, vectorSize);

            await qdrant.EnsureCollectionAsync();

            var documents = await processor.LoadKnowledgeBaseDocumentsAsync();
            if (documents == null || documents.Count == 0)
            {
                Console.WriteLine("⚠️ 未找到任何文档，跳过流程");
                return;
            }

            Console.WriteLine($"📚 共找到 {documents.Count} 个文档");

            foreach (var doc in documents)
            {
                var chunkResult = chunker.ChunkDocument(doc);
                if (!chunkResult.Success || chunkResult.Chunks == null || chunkResult.Chunks.Count == 0)
                {
                    Console.WriteLine("⚠️ 分块结果为空，跳过此文档");
                    continue;
                }

                foreach (var chunk in chunkResult.Chunks)
                {
                    var vector = await embedder.EmbedAsync(chunk.Content);
                }

                try
                {
                    var items = chunkResult.Chunks.Select(chunk => (Category: doc.FileName, Text: chunk.Content));
                    await qdrant.InsertTextsAsync(items, text => embedder.EmbedAsync(text).Result);
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

            Console.WriteLine($"🔍 查询：{query}，返回前{topK}个文档块");

            var searchResults = await qdrant.SearchAsync(query, text => embedder.EmbedAsync(text).Result, topK);

            int rank = 1;
            foreach (var (score, category, text) in searchResults)
            {
                Console.WriteLine($"{rank}. 相似度: {score:0.0000} 文档: {category} 内容: {text}");
                rank++;
            }

            // rerank
            var topMStr = Environment.GetEnvironmentVariable("SEARCH_RERANK_TOP_M");
            int topM = 5;
            if (!string.IsNullOrWhiteSpace(topMStr) && int.TryParse(topMStr, out var m))
                topM = m;

            var docBlocks = searchResults
                .Select(r => new { Category = r.Category, Content = r.Text })
                .ToList();

            var reranker = new RerankService();
            var rerankResults = await reranker.RerankAsync(query, docBlocks);

            Console.WriteLine($"🔝 Rerank后Top{topM}文档块：");
            for (int i = 0; i < Math.Min(topM, rerankResults.Count); i++)
            {
                var (block, score) = rerankResults[i];
                Console.WriteLine($"{i + 1}. 分数: {score:0.0000} 文档: {block.Category} 内容: {block.Content}");
            }
        }
    }
}

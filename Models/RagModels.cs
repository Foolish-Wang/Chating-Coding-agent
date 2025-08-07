using System;
using System.Collections.Generic;

namespace SemanticKernelAgent.Models
{
    /// <summary>
    /// RAG 相关数据模型
    /// </summary>
    public class RagModels
    {
        // TODO: 实现 RAG 相关的数据模型
    }

    /// <summary>
    /// 向量化结果
    /// </summary>
    public class EmbeddingResult
    {
        public string ChunkId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float[] Vector { get; set; } = Array.Empty<float>();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 检索结果
    /// </summary>
    public class SearchResult
    {
        public string ChunkId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 重排序结果
    /// </summary>
    public class RerankResult
    {
        public string ChunkId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float RerankScore { get; set; }
        public float OriginalScore { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}

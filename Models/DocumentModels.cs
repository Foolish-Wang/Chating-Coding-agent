using System;
using System.Collections.Generic;

namespace SemanticKernelAgent.Models
{
    /// <summary>
    /// 原始文档信息
    /// </summary>
    public class DocumentInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// 文档块（Chunking结果）
    /// </summary>
    public class DocumentChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public int CharacterCount { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 分块配置
    /// </summary>
    public class ChunkingConfig
    {
        public int ChunkSize { get; set; } = 800;           // 每块字符数
        public int ChunkOverlap { get; set; } = 200;        // 重叠字符数
        public string SplitStrategy { get; set; } = "fixed"; // 分割策略：fixed, sentence, paragraph
        public List<string> SeparatorChars { get; set; } = new() { "\n\n", "\n", ".", "!", "?" };
        public bool PreserveWhitespace { get; set; } = false;
        public int MinChunkSize { get; set; } = 100;        // 最小块大小
    }

    /// <summary>
    /// 分块结果
    /// </summary>
    public class ChunkingResult
    {
        public DocumentInfo SourceDocument { get; set; } = new();
        public List<DocumentChunk> Chunks { get; set; } = new();
        public int TotalChunks { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<string> Warnings { get; set; } = new();
        public bool Success { get; set; }
    }
}
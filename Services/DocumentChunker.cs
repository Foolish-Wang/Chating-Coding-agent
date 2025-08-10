using SemanticKernelAgent.Models;
using DotNetEnv;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// 文档分块服务 - 简化固定大小分块
    /// </summary>
    public class DocumentChunker
    {
        private readonly int _chunkSize;
        private readonly int _chunkOverlap;

        public DocumentChunker()
        {
            // 从环境变量加载配置
            Env.Load();
            
            _chunkSize = int.Parse(Environment.GetEnvironmentVariable("CHUNKING_CHUNK_SIZE"));
            var overlapPercent = int.Parse(Environment.GetEnvironmentVariable("CHUNKING_OVERLAP_PERCENT"));
            _chunkOverlap = _chunkSize * overlapPercent / 100;
            
            Console.WriteLine($"🔪 初始化文档分块器 - 块大小:{_chunkSize}, 重叠:{_chunkOverlap}字符({overlapPercent}%)");
        }

        /// <summary>
        /// 对单个文档进行分块 - 简化版本
        /// </summary>
        public ChunkingResult ChunkDocument(DocumentInfo document)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Console.WriteLine($"🔪 开始分块文档: {document.FileName} ({document.Content.Length} 字符)");

                var chunks = CreateSimpleFixedSizeChunks(document);
                stopwatch.Stop();

                var result = new ChunkingResult
                {
                    SourceDocument = document,
                    Chunks = chunks,
                    TotalChunks = chunks.Count,
                    ProcessingTime = stopwatch.Elapsed,
                    Warnings = new List<string>(),
                    Success = chunks.Count > 0
                };

                Console.WriteLine($"✅ 分块完成: {chunks.Count} 个块, 耗时: {stopwatch.ElapsedMilliseconds}ms");
                
                // // 显示块大小分布
                // for (int i = 0; i < chunks.Count; i++)
                // {
                //     var chunk = chunks[i];
                //     Console.WriteLine($"  块 {i + 1}: 位置 {chunk.StartPosition}-{chunk.EndPosition}, 长度 {chunk.CharacterCount}");
                // }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"❌ 文档分块失败: {ex.Message}");

                return new ChunkingResult
                {
                    SourceDocument = document,
                    Success = false,
                    ProcessingTime = stopwatch.Elapsed,
                    Warnings = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 简单固定大小分块 - 从头到尾，最后一块可以是任意大小
        /// </summary>
        private List<DocumentChunk> CreateSimpleFixedSizeChunks(DocumentInfo document)
        {
            var chunks = new List<DocumentChunk>();
            var content = document.Content;
            var position = 0;
            var chunkIndex = 0;

            while (position < content.Length)
            {
                // 计算当前块的大小
                var remainingLength = content.Length - position;
                var currentChunkSize = Math.Min(_chunkSize, remainingLength);
                
                // 提取块内容
                var chunkContent = content.Substring(position, currentChunkSize);
                
                // 创建块（不设置最小大小限制）
                var chunk = new DocumentChunk
                {
                    Content = chunkContent,
                    ChunkIndex = chunkIndex,
                    StartPosition = position,
                    EndPosition = position + currentChunkSize,
                    CharacterCount = chunkContent.Length,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source_document"] = document.FileName,
                        ["chunk_size_config"] = _chunkSize,
                        ["overlap_size"] = _chunkOverlap,
                        ["is_last_chunk"] = (position + currentChunkSize >= content.Length)
                    }
                };

                chunks.Add(chunk);
                chunkIndex++;

                // 移动到下一个位置
                if (position + currentChunkSize >= content.Length)
                {
                    // 已经到达文档末尾
                    break;
                }
                else
                {
                    // 移动位置，考虑重叠：下一块从 (当前位置 + 块大小 - 重叠大小) 开始
                    position += (_chunkSize - _chunkOverlap);
                }
            }

            return chunks;
        }
    }
}
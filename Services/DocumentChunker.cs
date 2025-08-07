using SemanticKernelAgent.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// 文档分块服务 - 负责将文档切分为小块
    /// </summary>
    public class DocumentChunker
    {
        private readonly ChunkingConfig _config;

        public DocumentChunker(ChunkingConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Console.WriteLine($"🔪 初始化文档分块器 - 块大小:{_config.ChunkSize}, 重叠:{_config.ChunkOverlap}");
        }

        /// <summary>
        /// 对单个文档进行分块
        /// </summary>
        public ChunkingResult ChunkDocument(DocumentInfo document)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Console.WriteLine($"🔪 开始分块文档: {document.FileName}");

                var chunks = new List<DocumentChunk>();
                var warnings = new List<string>();

                switch (_config.SplitStrategy.ToLower())
                {
                    case "fixed":
                    default:
                        chunks = CreateFixedSizeChunks(document, warnings);
                        break;
                }

                stopwatch.Stop();

                var result = new ChunkingResult
                {
                    SourceDocument = document,
                    Chunks = chunks,
                    TotalChunks = chunks.Count,
                    ProcessingTime = stopwatch.Elapsed,
                    Warnings = warnings,
                    Success = chunks.Count > 0
                };

                Console.WriteLine($"✅ 分块完成: {chunks.Count} 个块, 耗时: {stopwatch.ElapsedMilliseconds}ms");

                // 显示分块统计
                if (chunks.Count > 0)
                {
                    var avgSize = chunks.Average(c => c.CharacterCount);
                    var minSize = chunks.Min(c => c.CharacterCount);
                    var maxSize = chunks.Max(c => c.CharacterCount);
                    Console.WriteLine($"📊 块大小统计 - 平均:{avgSize:F0}, 最小:{minSize}, 最大:{maxSize}");
                }

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
        /// 固定大小分块策略
        /// </summary>
        private List<DocumentChunk> CreateFixedSizeChunks(DocumentInfo document, List<string> warnings)
        {
            var chunks = new List<DocumentChunk>();
            var content = document.Content;
            var chunkIndex = 0;
            var position = 0;

            while (position < content.Length)
            {
                var chunkSize = Math.Min(_config.ChunkSize, content.Length - position);
                var endPos = position + chunkSize;

                // 如果不是最后一块，尝试在分隔符处断开
                if (endPos < content.Length)
                {
                    var betterEndPos = FindBestBreakPoint(content, position, endPos);
                    if (betterEndPos > position)
                    {
                        endPos = betterEndPos;
                        chunkSize = endPos - position;
                    }
                }

                var chunkContent = content.Substring(position, chunkSize);
                
                // 清理块内容
                if (!_config.PreserveWhitespace)
                {
                    chunkContent = chunkContent.Trim();
                }

                // 跳过太小的块
                if (chunkContent.Length < _config.MinChunkSize && position + chunkSize < content.Length)
                {
                    warnings.Add($"跳过过小的块 {chunkIndex}: {chunkContent.Length} 字符");
                    position = Math.Max(position + 1, endPos - _config.ChunkOverlap);
                    continue;
                }

                var chunk = new DocumentChunk
                {
                    Content = chunkContent,
                    SourceFile = document.FileName,
                    ChunkIndex = chunkIndex,
                    StartPosition = position,
                    EndPosition = endPos,
                    CharacterCount = chunkContent.Length,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source_document_id"] = document.Id,
                        ["chunk_strategy"] = _config.SplitStrategy,
                        ["chunk_size_config"] = _config.ChunkSize,
                        ["overlap_size"] = _config.ChunkOverlap
                    }
                };

                chunks.Add(chunk);
                chunkIndex++;

                // 计算下一个位置（考虑重叠）
                // 如果是最后一块，结束循环
                if (endPos >= content.Length)
                {
                    break;
                }
                
                // 正确计算下一个位置：当前块结束位置减去重叠大小
                position = Math.Max(endPos - _config.ChunkOverlap, position + 1);
            }

            return chunks;
        }

        /// <summary>
        /// 寻找最佳断点位置
        /// </summary>
        private int FindBestBreakPoint(string content, int start, int preferredEnd)
        {
            var searchStart = Math.Max(start, preferredEnd - 100); // 在首选结束位置前100字符内搜索
            
            foreach (var separator in _config.SeparatorChars)
            {
                var lastIndex = content.LastIndexOf(separator, preferredEnd, preferredEnd - searchStart);
                if (lastIndex > searchStart)
                {
                    return lastIndex + separator.Length;
                }
            }
            
            return preferredEnd;
        }
    }
}
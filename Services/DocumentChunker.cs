using SemanticKernelAgent.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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
                        chunks = CreateFixedSizeChunks(document, warnings);
                        break;
                    case "sentence":
                        chunks = CreateSentenceBasedChunks(document, warnings);
                        break;
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
                position = Math.Max(position + 1, endPos - _config.ChunkOverlap);
            }

            return chunks;
        }

        /// <summary>
        /// 基于句子的分块策略
        /// </summary>
        private List<DocumentChunk> CreateSentenceBasedChunks(DocumentInfo document, List<string> warnings)
        {
            var chunks = new List<DocumentChunk>();
            var sentences = SplitIntoSentences(document.Content);
            
            var currentChunk = new List<string>();
            var currentLength = 0;
            var chunkIndex = 0;
            var startPos = 0;

            foreach (var sentence in sentences)
            {
                if (currentLength + sentence.Length > _config.ChunkSize && currentChunk.Count > 0)
                {
                    // 创建当前块
                    var chunkContent = string.Join(" ", currentChunk);
                    var chunk = CreateChunkFromContent(chunkContent, document, chunkIndex, startPos);
                    chunks.Add(chunk);
                    
                    chunkIndex++;
                    startPos += chunkContent.Length;

                    // 重叠处理：保留最后几个句子
                    var overlapSentences = GetOverlapSentences(currentChunk, _config.ChunkOverlap);
                    currentChunk = overlapSentences;
                    currentLength = overlapSentences.Sum(s => s.Length);
                }

                currentChunk.Add(sentence);
                currentLength += sentence.Length;
            }

            // 处理最后一块
            if (currentChunk.Count > 0)
            {
                var chunkContent = string.Join(" ", currentChunk);
                var chunk = CreateChunkFromContent(chunkContent, document, chunkIndex, startPos);
                chunks.Add(chunk);
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

        /// <summary>
        /// 将文本分割为句子
        /// </summary>
        private List<string> SplitIntoSentences(string content)
        {
            var sentences = new List<string>();
            var sentenceEnders = new[] { '.', '!', '?', '。', '！', '？' };
            
            var currentSentence = "";
            for (int i = 0; i < content.Length; i++)
            {
                currentSentence += content[i];
                
                if (sentenceEnders.Contains(content[i]))
                {
                    sentences.Add(currentSentence.Trim());
                    currentSentence = "";
                }
            }
            
            if (!string.IsNullOrWhiteSpace(currentSentence))
            {
                sentences.Add(currentSentence.Trim());
            }
            
            return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        /// <summary>
        /// 获取重叠句子
        /// </summary>
        private List<string> GetOverlapSentences(List<string> sentences, int overlapSize)
        {
            var overlap = new List<string>();
            var currentLength = 0;
            
            for (int i = sentences.Count - 1; i >= 0 && currentLength < overlapSize; i--)
            {
                overlap.Insert(0, sentences[i]);
                currentLength += sentences[i].Length;
            }
            
            return overlap;
        }

        /// <summary>
        /// 从内容创建文档块
        /// </summary>
        private DocumentChunk CreateChunkFromContent(string content, DocumentInfo document, int chunkIndex, int startPos)
        {
            return new DocumentChunk
            {
                Content = content,
                SourceFile = document.FileName,
                ChunkIndex = chunkIndex,
                StartPosition = startPos,
                EndPosition = startPos + content.Length,
                CharacterCount = content.Length,
                Metadata = new Dictionary<string, object>
                {
                    ["source_document_id"] = document.Id,
                    ["chunk_strategy"] = _config.SplitStrategy,
                    ["chunk_size_config"] = _config.ChunkSize,
                    ["overlap_size"] = _config.ChunkOverlap
                }
            };
        }
    }
}
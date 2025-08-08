using SemanticKernelAgent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// 文档处理服务 - 负责文档加载和预处理
    /// </summary>
    public class DocumentProcessor
    {
        private readonly List<string> _supportedExtensions = new() { ".txt", ".md" };
        private readonly string _knowledgeBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data");

        /// <summary>
        /// 从文件路径加载文档
        /// </summary>
        public async Task<DocumentInfo> LoadDocumentAsync(string filePath)
        {
            try
            {
                Console.WriteLine($"📄 加载文档: {Path.GetFileName(filePath)}");

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"文件不存在: {filePath}");

                var extension = Path.GetExtension(filePath).ToLower();
                if (!_supportedExtensions.Contains(extension))
                    throw new NotSupportedException($"不支持的文件类型: {extension}");

                var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                var fileInfo = new FileInfo(filePath);

                var document = new DocumentInfo
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Content = CleanContent(content),
                    FileSize = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTime,
                    ModifiedAt = fileInfo.LastWriteTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["original_size"] = fileInfo.Length,
                        ["extension"] = extension,
                        ["encoding"] = "utf-8"
                    }
                };

                Console.WriteLine($"✅ 文档加载成功，大小: {document.FileSize} 字节，内容长度: {document.Content.Length} 字符");
                return document;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 文档加载失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 加载知识库中的所有文档（直接从 Data 目录加载）
        /// </summary>
        public async Task<List<DocumentInfo>> LoadKnowledgeBaseDocumentsAsync()
        {
            return await LoadDocumentsFromDirectoryAsync(_knowledgeBasePath);
        }

        /// <summary>
        /// 批量加载目录中的文档
        /// </summary>
        public async Task<List<DocumentInfo>> LoadDocumentsFromDirectoryAsync(string directoryPath = null)
        {
            // 如果没有指定目录，使用默认的知识库目录
            var targetPath = directoryPath ?? _knowledgeBasePath;
            var documents = new List<DocumentInfo>();

            Console.WriteLine($"🔍 当前工作目录: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"🔍 BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"🔍 查找目录: {targetPath}");
            Console.WriteLine($"🔍 目录完整路径: {Path.GetFullPath(targetPath)}");

            if (!Directory.Exists(targetPath))
            {
                Console.WriteLine($"❌ 目录不存在: {targetPath}");
                
                // 尝试其他可能的路径
                var altPath1 = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                var altPath2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data");
                
                Console.WriteLine($"🔍 尝试备用路径1: {altPath1} - 存在: {Directory.Exists(altPath1)}");
                Console.WriteLine($"🔍 尝试备用路径2: {altPath2} - 存在: {Directory.Exists(altPath2)}");
                
                return documents;
            }

            // 列出目录中的所有文件
            var allFiles = Directory.GetFiles(targetPath, "*.*", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"📁 目录中的所有文件 ({allFiles.Length}):");
            foreach (var file in allFiles)
            {
                Console.WriteLine($"  - {Path.GetFileName(file)} (扩展名: {Path.GetExtension(file)})");
            }

            // 只查找当前目录的文件，不递归子目录（因为文件直接在 Data 目录中）
            var files = Directory.GetFiles(targetPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            Console.WriteLine($"📁 在目录 {targetPath} 中找到 {files.Count} 个支持的文档");
            Console.WriteLine($"📁 支持的扩展名: {string.Join(", ", _supportedExtensions)}");
            
            // 打印找到的文件列表
            foreach (var file in files)
            {
                Console.WriteLine($"  - 发现文件: {Path.GetFileName(file)}");
            }

            foreach (var file in files)
            {
                try
                {
                    var document = await LoadDocumentAsync(file);
                    documents.Add(document);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 跳过文件 {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"✅ 成功加载 {documents.Count} 个文档");
            return documents;
        }

        /// <summary>
        /// 清理文档内容
        /// </summary>
        private string CleanContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            // 基础清理：移除多余空白和特殊字符
            var lines = content.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();

            return string.Join('\n', lines);
        }

        /// <summary>
        /// 获取支持的文件扩展名
        /// </summary>
        public List<string> GetSupportedExtensions()
        {
            return new List<string>(_supportedExtensions);
        }
    }
}
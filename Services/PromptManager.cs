using System;
using System.IO;
using System.Threading.Tasks;

namespace SemanticKernelAgent.Services
{
    public class PromptManager
    {
        private readonly string _promptsDirectory;

        public PromptManager(string promptsDirectory = "Prompts")
        {
            _promptsDirectory = promptsDirectory;
        }

        /// <summary>
        /// 加载系统提示文本
        /// </summary>
        /// <param name="promptFileName">提示文件名（不含扩展名）</param>
        /// <returns>系统提示文本</returns>
        public async Task<string> LoadSystemPromptAsync(string promptFileName = "SystemPrompt")
        {
            try
            {
                var filePath = Path.Combine(_promptsDirectory, $"{promptFileName}.md");
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"⚠️ 警告: 系统提示文件 {filePath} 不存在，使用默认提示");
                    return GetDefaultSystemPrompt();
                }

                var content = await File.ReadAllTextAsync(filePath);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"⚠️ 警告: 系统提示文件 {filePath} 为空，使用默认提示");
                    return GetDefaultSystemPrompt();
                }

                Console.WriteLine($"✅ 系统提示已从 {filePath} 加载");
                
                // 在内容前添加前缀，明确角色定位
                return $"我是一个运行在以下环境的AI助手：\n\n{content}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载系统提示失败: {ex.Message}，使用默认提示");
                return GetDefaultSystemPrompt();
            }
        }

        /// <summary>
        /// 加载特定语言的系统提示
        /// </summary>
        /// <param name="language">语言代码，如 "zh-CN", "en-US"</param>
        /// <returns>系统提示文本</returns>
        public async Task<string> LoadSystemPromptByLanguageAsync(string language)
        {
            var promptFileName = $"SystemPrompt_{language}";
            return await LoadSystemPromptAsync(promptFileName);
        }

        /// <summary>
        /// 获取默认系统提示（简化版本）
        /// </summary>
        /// <returns>默认系统提示文本</returns>
        private string GetDefaultSystemPrompt()
        {
            return @"我是一个AI助手，具备以下能力：

## 基本功能：
- 使用WebOperations.SearchAsync进行网络搜索
- 使用WebOperations.DeepSearchAsync进行深度搜索  
- 使用FileOperations进行文件操作
- 使用CliOperations执行命令行操作

## 工作原则：
1. 需要最新信息时，必须先进行网络搜索
2. 基于搜索结果创建内容，而不是使用训练数据
3. 根据操作系统选择合适的命令和工具

## 搜索要求：
- 搜索关键词要具体和相关
- 基于真实搜索结果创建内容
- 如果搜索失败，提供替代建议";
        }

        /// <summary>
        /// 检查系统提示文件是否存在
        /// </summary>
        /// <param name="promptFileName">提示文件名（不含扩展名）</param>
        /// <returns>文件是否存在</returns>
        public bool SystemPromptExists(string promptFileName = "SystemPrompt")
        {
            var filePath = Path.Combine(_promptsDirectory, $"{promptFileName}.md");
            return File.Exists(filePath);
        }

        /// <summary>
        /// 获取所有可用的系统提示文件列表
        /// </summary>
        /// <returns>系统提示文件名列表</returns>
        public string[] GetAvailableSystemPrompts()
        {
            try
            {
                if (!Directory.Exists(_promptsDirectory))
                {
                    return new string[0];
                }

                var files = Directory.GetFiles(_promptsDirectory, "SystemPrompt*.md");
                var promptNames = new string[files.Length];
                
                for (int i = 0; i < files.Length; i++)
                {
                    var fileName = Path.GetFileNameWithoutExtension(files[i]);
                    promptNames[i] = fileName.Replace("SystemPrompt_", "").Replace("SystemPrompt", "default");
                }

                return promptNames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取系统提示列表失败: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>
        /// 动态重新加载系统提示
        /// </summary>
        /// <param name="promptFileName">提示文件名</param>
        /// <returns>新的系统提示文本</returns>
        public async Task<string> ReloadSystemPromptAsync(string promptFileName = "SystemPrompt")
        {
            Console.WriteLine("🔄 重新加载系统提示...");
            return await LoadSystemPromptAsync(promptFileName);
        }
    }
}
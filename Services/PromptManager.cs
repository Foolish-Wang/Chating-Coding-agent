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
            var filePath = Path.Combine(_promptsDirectory, $"{promptFileName}.md");
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"系统提示文件 {filePath} 不存在。请确保文件存在后重试。");
            }

            var content = await File.ReadAllTextAsync(filePath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"系统提示文件 {filePath} 为空。请检查文件内容。");
            }

            Console.WriteLine($"✅ 系统提示已从 {filePath} 加载");
            
            // 在内容前添加前缀，明确角色定位
            return $"我是一个运行在以下环境的AI助手：\n\n{content}";
        }

        /// <summary>
        /// 加载验证Agent的系统提示
        /// </summary>
        /// <param name="promptFileName">提示文件名（不含扩展名）</param>
        /// <returns>验证系统提示文本</returns>
        public async Task<string> LoadValidationPromptAsync(string promptFileName = "ValidationSystemPrompt")
        {
            var filePath = Path.Combine(_promptsDirectory, $"{promptFileName}.md");
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"验证Agent提示文件 {filePath} 不存在。请确保文件存在后重试。");
            }

            var content = await File.ReadAllTextAsync(filePath);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"验证Agent提示文件 {filePath} 为空。请检查文件内容。");
            }

            Console.WriteLine($"✅ 验证Agent提示已从 {filePath} 加载");
            return content;
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

        /// <summary>
        /// 动态重新加载验证Agent提示
        /// </summary>
        /// <param name="promptFileName">提示文件名</param>
        /// <returns>新的验证提示文本</returns>
        public async Task<string> ReloadValidationPromptAsync(string promptFileName = "ValidationSystemPrompt")
        {
            Console.WriteLine("🔄 重新加载验证Agent提示...");
            return await LoadValidationPromptAsync(promptFileName);
        }
    }
}
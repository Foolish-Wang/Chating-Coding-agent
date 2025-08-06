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
        /// 加载验证Agent的系统提示
        /// </summary>
        /// <param name="promptFileName">提示文件名（不含扩展名）</param>
        /// <returns>验证系统提示文本</returns>
        public async Task<string> LoadValidationPromptAsync(string promptFileName = "ValidationSystemPrompt")
        {
            try
            {
                var filePath = Path.Combine(_promptsDirectory, $"{promptFileName}.md");
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"⚠️ 警告: 验证Agent提示文件 {filePath} 不存在，使用默认提示");
                    return GetDefaultValidationPrompt();
                }

                var content = await File.ReadAllTextAsync(filePath);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"⚠️ 警告: 验证Agent提示文件 {filePath} 为空，使用默认提示");
                    return GetDefaultValidationPrompt();
                }

                Console.WriteLine($"✅ 验证Agent提示已从 {filePath} 加载");
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载验证Agent提示失败: {ex.Message}，使用默认提示");
                return GetDefaultValidationPrompt();
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
        /// 获取默认验证Agent系统提示
        /// </summary>
        /// <returns>默认验证系统提示文本</returns>
        private string GetDefaultValidationPrompt()
        {
            return @"你是一个专业的内容验证助手，负责检查主Agent完成的任务结果。

## 你的职责：
1. **完整性检查**：验证任务是否完全完成，没有遗漏的要求
2. **质量评估**：检查内容的准确性、实用性和专业性
3. **格式规范**：检查输出格式是否符合要求
4. **错误识别**：发现技术错误、逻辑错误或信息错误
5. **改进建议**：提供具体、可操作的修改建议

## 输出格式：
请按以下格式提供验证结果：

### 🔍 验证结果
**总体评分**：[1-10分]

### ✅ 完成良好的方面
- [列出做得好的地方]

### ❌ 发现的问题
1. **问题类型**：[描述问题]

### 🔧 改进建议
1. **针对问题1**：[具体的修改建议]

如果任务完成得很好，只有小问题或无问题，请简洁地给出正面评价。";
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
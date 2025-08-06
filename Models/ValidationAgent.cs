using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

#pragma warning disable SKEXP0070

namespace SemanticKernelAgent.Models
{
    public class ValidationAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        private readonly ChatHistory _chatHistory;

        public ValidationAgent(AgentConfig config, ValidationConfig validationConfig = null)
        {
            var kernelBuilder = Kernel.CreateBuilder();

            if (validationConfig != null && validationConfig.UseGemini && !string.IsNullOrEmpty(validationConfig.ApiKey))
            {
                Console.WriteLine("🔧 尝试连接Gemini API (官方连接器)...");
                
                try
                {
                    // 使用官方的 Google AI Gemini 连接器
                    kernelBuilder.AddGoogleAIGeminiChatCompletion(
                        modelId: validationConfig.ModelId,
                        apiKey: validationConfig.ApiKey);
                    
                    Console.WriteLine("✅ Gemini API连接配置完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Gemini API配置失败: {ex.Message}");
                    throw new InvalidOperationException($"无法连接到Gemini API: {ex.Message}", ex);
                }
            }
            else
            {
                throw new InvalidOperationException("ValidationAgent 需要有效的 Gemini API 配置。请确保 GEMINI_API_KEY 已正确设置。");
            }

            _kernel = kernelBuilder.Build();
            _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            _chatHistory = new ChatHistory();

            // 设置验证Agent的系统提示
            var validationSystemPrompt = @"你是一个专业的内容验证助手，负责检查主Agent完成的任务结果。

## 你的职责：
1. **完整性检查**：验证任务是否完全完成，没有遗漏的要求
2. **质量评估**：检查内容的准确性、实用性和专业性
3. **格式规范**：检查输出格式是否符合要求
4. **错误识别**：发现技术错误、逻辑错误或信息错误
5. **改进建议**：提供具体、可操作的修改建议

## 检查标准：
- ✅ 任务要求完成度（是否遗漏功能）
- ✅ 信息准确性（事实是否正确）
- ✅ 格式规范性（HTML、代码、文档格式）
- ✅ 用户体验（交互性、美观性、实用性）
- ✅ 技术可行性（代码是否能正常运行）
- ✅ 链接有效性（外部链接是否正确）

## 输出格式：
请按以下格式提供验证结果：

### 🔍 验证结果
**总体评分**：[1-10分]

### ✅ 完成良好的方面
- [列出做得好的地方]

### ❌ 发现的问题
1. **问题类型**：[描述问题]
   - 具体说明：[详细说明]
   - 影响程度：[高/中/低]

### 🔧 改进建议
1. **针对问题1**：[具体的修改建议]
2. **针对问题2**：[具体的修改建议]

### 💡 额外优化建议
- [可选的改进点]

如果任务完成得很好，只有小问题或无问题，请简洁地给出正面评价。";

            _chatHistory.AddSystemMessage(validationSystemPrompt);
        }

        public async Task<ValidationResult> ValidateTaskResultAsync(string originalTask, string taskResult, string additionalContext = "")
        {
            try
            {
                // 清理聊天历史，避免累积过多消息
                if (_chatHistory.Count > 10)
                {
                    var systemMessage = _chatHistory[0];
                    _chatHistory.Clear();
                    _chatHistory.Add(systemMessage);
                }

                var validationPrompt = $@"请验证以下任务的完成情况：

**原始任务**：
{originalTask}

**任务结果**：
{taskResult}

**额外上下文**：
{additionalContext}

请根据你的检查标准进行全面验证，并提供详细的反馈。";

                _chatHistory.AddUserMessage(validationPrompt);

                // Gemini 使用简化调用，不传递执行设置
                var response = await _chatService.GetChatMessageContentAsync(
                    _chatHistory,
                    kernel: _kernel);

                _chatHistory.AddAssistantMessage(response.Content);

                return new ValidationResult
                {
                    ValidationFeedback = response.Content,
                    HasIssues = ContainsIssues(response.Content),
                    OriginalTask = originalTask,
                    TaskResult = taskResult
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 验证Agent调用失败: {ex.Message}");
                Console.WriteLine($"详细错误: {ex}");
                throw; // 直接抛出异常，不提供回退
            }
        }

        private bool ContainsIssues(string feedback)
        {
            if (string.IsNullOrEmpty(feedback)) return false;
            
            var issueIndicators = new[] { "❌", "问题", "错误", "缺少", "不足", "需要改进", "建议修改" };
            return issueIndicators.Any(indicator => feedback.Contains(indicator));
        }
    }

    public class ValidationResult
    {
        public string ValidationFeedback { get; set; } = "";
        public bool HasIssues { get; set; }
        public string OriginalTask { get; set; } = "";
        public string TaskResult { get; set; } = "";
        public List<string> SuggestedImprovements { get; set; } = new();
    }

    public class ValidationConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public bool UseGemini { get; set; } = true;
    }
}
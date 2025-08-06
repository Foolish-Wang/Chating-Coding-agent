using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Google;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

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

            if (validationConfig != null && validationConfig.UseGemini)
            {
                // 使用Gemini API
                kernelBuilder.AddGoogleAIGeminiChatCompletion(
                    modelId: validationConfig.ModelId,
                    apiKey: validationConfig.ApiKey);
            }
            else
            {
                // 使用默认的DeepSeek API
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: config.ModelId,
                    apiKey: config.ApiKey,
                    endpoint: new Uri(config.Endpoint));
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
            var validationPrompt = $@"请验证以下任务的完成情况：

**原始任务**：
{originalTask}

**任务结果**：
{taskResult}

**额外上下文**：
{additionalContext}

请根据你的检查标准进行全面验证，并提供详细的反馈。";

            _chatHistory.AddUserMessage(validationPrompt);

            var executionSettings = new PromptExecutionSettings()
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["maxTokens"] = 2000,
                    ["temperature"] = 0.3 // 较低温度确保更准确的验证
                }
            };

            var response = await _chatService.GetChatMessageContentAsync(
                _chatHistory,
                executionSettings,
                _kernel);

            _chatHistory.AddAssistantMessage(response.Content);

            return new ValidationResult
            {
                ValidationFeedback = response.Content,
                HasIssues = ContainsIssues(response.Content),
                OriginalTask = originalTask,
                TaskResult = taskResult
            };
        }

        private bool ContainsIssues(string feedback)
        {
            // 简单的问题检测逻辑
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
        public string ApiKey { get; set; } = "";
        public string ModelId { get; set; } = "gemini-pro";
        public bool UseGemini { get; set; } = true;
    }
}
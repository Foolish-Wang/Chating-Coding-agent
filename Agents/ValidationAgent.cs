using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using SemanticKernelAgent.Services;
using SemanticKernelAgent.Models;
using System.Threading.Tasks;
using System.Linq;
using System;

#pragma warning disable SKEXP0070

namespace SemanticKernelAgent.Agents
{
    public class ValidationAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        private readonly ChatHistory _chatHistory;
        private readonly PromptManager _promptManager;

        public ValidationAgent(AgentConfig config, ValidationConfig validationConfig = null)
        {
            var kernelBuilder = Kernel.CreateBuilder();
            _promptManager = new PromptManager();

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

            // 使用 PromptManager 加载验证Agent的系统提示
            InitializeSystemPromptAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeSystemPromptAsync()
        {
            try
            {
                Console.WriteLine("📋 正在加载验证Agent系统提示...");
                var validationSystemPrompt = await _promptManager.LoadValidationPromptAsync();
                _chatHistory.AddSystemMessage(validationSystemPrompt);
                Console.WriteLine("✅ 验证Agent系统提示加载完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载验证Agent系统提示失败: {ex.Message}");
                // 如果加载失败，使用默认提示
                var defaultPrompt = @"你是一个专业的内容验证助手，负责检查主Agent完成的任务结果。请提供详细的验证反馈。";
                _chatHistory.AddSystemMessage(defaultPrompt);
            }
        }

        /// <summary>
        /// 重新加载验证Agent的系统提示
        /// </summary>
        public async Task ReloadSystemPromptAsync()
        {
            try
            {
                var newPrompt = await _promptManager.ReloadValidationPromptAsync();
                
                // 更新聊天历史中的系统消息
                if (_chatHistory.Count > 0 && _chatHistory[0].Role == AuthorRole.System)
                {
                    _chatHistory.RemoveAt(0);
                }
                _chatHistory.Insert(0, new ChatMessageContent(AuthorRole.System, newPrompt));
                
                Console.WriteLine("✅ 验证Agent系统提示已重新加载");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 重新加载验证Agent系统提示失败: {ex.Message}");
            }
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
}
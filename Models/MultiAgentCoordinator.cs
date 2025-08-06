using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Threading.Tasks;
using System;

namespace SemanticKernelAgent.Models
{
    public class MultiAgentCoordinator
    {
        private readonly Kernel _mainKernel;
        private readonly ValidationAgent _validationAgent;
        private readonly IChatCompletionService _mainChatService;
        private readonly ChatHistory _mainChatHistory;

        public MultiAgentCoordinator(Kernel mainKernel, ValidationAgent validationAgent, ChatHistory chatHistory)
        {
            _mainKernel = mainKernel;
            _validationAgent = validationAgent;
            _mainChatService = mainKernel.GetRequiredService<IChatCompletionService>();
            _mainChatHistory = chatHistory;
        }

        public async Task<string> ProcessTaskWithValidationAsync(string userInput, OpenAIPromptExecutionSettings executionSettings)
        {
            Console.WriteLine("🤖 主Agent开始处理任务...");
            
            // 1. 主Agent处理任务
            _mainChatHistory.AddUserMessage(userInput);
            
            var mainResponse = await _mainChatService.GetChatMessageContentAsync(
                _mainChatHistory,
                executionSettings,
                _mainKernel);

            _mainChatHistory.AddAssistantMessage(mainResponse.Content!);

            Console.WriteLine("\n🔍 副Agent开始验证结果...");

            // 2. 验证Agent检查结果
            var validationResult = await _validationAgent.ValidateTaskResultAsync(
                userInput,
                mainResponse.Content!,
                "主Agent刚刚完成了这个任务，请检查是否有需要改进的地方。");

            Console.WriteLine($"\n📋 验证完成，发现问题：{(validationResult.HasIssues ? "是" : "否")}");

            // 3. 如果有问题，让主Agent进行改进
            if (validationResult.HasIssues)
            {
                Console.WriteLine("🔧 主Agent根据反馈进行改进...");

                var improvementPrompt = $@"根据验证反馈，请改进你的上一个回答：

**验证反馈**：
{validationResult.ValidationFeedback}

**原始任务**：
{userInput}

**你之前的回答**：
{mainResponse.Content}

请根据验证反馈进行必要的修正和改进，提供更好的结果。如果需要执行额外的函数调用来完善结果，请执行。";

                _mainChatHistory.AddUserMessage(improvementPrompt);

                var improvedResponse = await _mainChatService.GetChatMessageContentAsync(
                    _mainChatHistory,
                    executionSettings,
                    _mainKernel);

                _mainChatHistory.AddAssistantMessage(improvedResponse.Content!);

                // 返回改进后的结果和验证反馈
                return $"{improvedResponse.Content}\n\n---\n**验证反馈**：\n{validationResult.ValidationFeedback}";
            }
            else
            {
                // 任务完成良好，返回原结果和正面反馈
                return $"{mainResponse.Content}\n\n---\n**验证反馈**：\n{validationResult.ValidationFeedback}";
            }
        }
    }
}
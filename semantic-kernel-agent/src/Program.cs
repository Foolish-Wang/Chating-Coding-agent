using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DotNetEnv;
using SemanticKernelAgent.Models;

namespace SemanticKernelAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 加载环境变量
            Env.Load();

            Console.WriteLine("Initializing Semantic Kernel Agent...");

            // 从环境变量创建配置
            var config = new AgentConfig
            {
                ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "",
                ModelId = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL_ID") ?? "deepseek-chat",
                Endpoint = Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT") ?? "https://api.deepseek.com/"
            };

            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new InvalidOperationException("DEEPSEEK_API_KEY not found in environment variables");
            }

            // 创建内核并添加插件
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: config.ModelId, 
                    apiKey: config.ApiKey, 
                    endpoint: new Uri(config.Endpoint))
                .Build();

            // 添加插件
            kernel.Plugins.AddFromType<FilePlugin>("FileOperations");
            kernel.Plugins.AddFromType<WebPlugin>("WebOperations");
            kernel.Plugins.AddFromType<CliPlugin>("CliOperations");

            Console.WriteLine("Agent is ready with file, web and CLI capabilities! Type 'exit' to quit.");

            // 聊天循环
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();

            // 创建执行设置，启用自动函数调用，添加超时设置
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.7
            };

            while (true)
            {
                Console.Write("User > ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit") 
                {
                    break;
                }
                
                try
                {
                    Console.WriteLine("Processing your request...");
                    chatHistory.AddUserMessage(input);
                    
                    // 添加超时控制
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
                    var responseTask = chatService.GetChatMessageContentAsync(
                        chatHistory, 
                        executionSettings,
                        kernel);

                    var completedTask = await Task.WhenAny(responseTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Console.WriteLine("AI > 请求超时，请稍后再试。");
                        continue;
                    }

                    var response = await responseTask;
                    Console.WriteLine($"AI > {response.Content}");
                    chatHistory.AddAssistantMessage(response.Content!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    
                    // 清除最后一条用户消息，避免重复处理
                    if (chatHistory.Count > 0)
                    {
                        chatHistory.RemoveAt(chatHistory.Count - 1);
                    }
                }
            }

            Console.WriteLine("Agent has been stopped.");
        }
    }
}
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

            // 创建执行设置，启用自动函数调用
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
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
                    chatHistory.AddUserMessage(input);
                    
                    // 使用正确的参数顺序和类型
                    var response = await chatService.GetChatMessageContentAsync(
                        chatHistory, 
                        executionSettings,
                        kernel);
                    
                    Console.WriteLine($"AI > {response.Content}");
                    chatHistory.AddAssistantMessage(response.Content!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            Console.WriteLine("Agent has been stopped.");
        }
    }
}
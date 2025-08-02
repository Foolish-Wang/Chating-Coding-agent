using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DotNetEnv;
using SemanticKernelAgent.Models;
using System.Linq;

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

            // 添加ReAct模式的函数调用监控
            kernel.FunctionInvocationFilters.Add(new ReActLoggingFilter());

            Console.WriteLine("Agent is ready with file, web and CLI capabilities! Type 'exit' to quit.");

            // 聊天循环
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();

            // 创建执行设置，启用自动函数调用
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 1
            };

            // 移除过时的事件处理器，暂时不添加Filter
            // 后续可以按照官方示例添加Filter来监控函数调用

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
                    
                    var response = await chatService.GetChatMessageContentAsync(
                        chatHistory, 
                        executionSettings,
                        kernel);

                    Console.WriteLine($"AI > {response.Content}");
                    chatHistory.AddAssistantMessage(response.Content!);
                    
                    Console.WriteLine("\n--- Task completed. Ready for your next command ---\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    
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

    // ReAct模式的函数调用监控Filter
    public class ReActLoggingFilter : IFunctionInvocationFilter
    {
        private int _stepCounter = 0;

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            _stepCounter++;
            
            // Action: 显示即将执行的操作
            Console.WriteLine($"\n🔧 Action {_stepCounter}: {context.Function.PluginName}.{context.Function.Name}");
            Console.WriteLine($"   Parameters: {string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value?.ToString()?.Substring(0, Math.Min(50, a.Value?.ToString()?.Length ?? 0))}..."))}");
            
            // 执行函数
            await next(context);
            
            // Observation: 显示执行结果
            var result = context.Result?.ToString();
            var truncatedResult = result?.Length > 200 ? result.Substring(0, 200) + "..." : result;
            Console.WriteLine($"✅ Observation {_stepCounter}: {truncatedResult}");
        }
    }
}
using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Google; 
using DotNetEnv;
using SemanticKernelAgent.Models;
using SemanticKernelAgent.Services;
using System.Linq;

#pragma warning disable SKEXP0070

namespace SemanticKernelAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 加载环境变量
            Env.Load();

            Console.WriteLine("正在初始化多Agent系统...");

            // 从环境变量创建主Agent配置
            var config = new AgentConfig
            {
                ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                ModelId = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL_ID"),
                Endpoint = Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT")
            };

            if (string.IsNullOrEmpty(config.ApiKey))
            {
                throw new InvalidOperationException("DEEPSEEK_API_KEY not found in environment variables");
            }

            // 创建验证Agent配置（使用Gemini）
            var validationConfig = new ValidationConfig
            {
                ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
                ModelId = Environment.GetEnvironmentVariable("GEMINI_MODEL_ID"), 
                UseGemini = true
            };

            // 如果未找到GEMINI_API_KEY，则使用DeepSeek API
            if (string.IsNullOrEmpty(validationConfig.ApiKey))
            {
                Console.WriteLine("⚠️ 警告：GEMINI_API_KEY未找到，验证Agent将使用DeepSeek API");
                validationConfig = null;
            }
            else
            {
                Console.WriteLine("✅ 验证Agent将使用Gemini API");
            }

            // 创建主Agent内核并添加插件
            var mainKernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: config.ModelId, 
                    apiKey: config.ApiKey, 
                    endpoint: new Uri(config.Endpoint))
                .Build();

            // 添加插件
            mainKernel.Plugins.AddFromType<FilePlugin>("FileOperations");
            mainKernel.Plugins.AddFromType<WebPlugin>("WebOperations");  
            mainKernel.Plugins.AddFromType<CliPlugin>("CliOperations");
            mainKernel.Plugins.AddFromType<SystemPlugin>("SystemOperations");

            // 添加ReAct模式的函数调用监控
            mainKernel.FunctionInvocationFilters.Add(new ReActLoggingFilter());

            // 创建验证Agent
            var validationAgent = new ValidationAgent(config, validationConfig);

            // 创建主聊天历史
            var chatHistory = new ChatHistory();

            // 创建多Agent协调器
            var coordinator = new MultiAgentCoordinator(mainKernel, validationAgent, chatHistory);

            // 创建执行设置
            var executionSettings = new OpenAIPromptExecutionSettings()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 1
            };

            // 使用PromptManager加载系统提示
            var promptManager = new PromptManager();
            
            // 添加系统上下文
            if (chatHistory.Count == 0)
            {
                // 检查是否有可用的系统提示文件
                var availablePrompts = promptManager.GetAvailableSystemPrompts();
                if (availablePrompts.Length > 0)
                {
                    Console.WriteLine($"📝 发现 {availablePrompts.Length} 个系统提示文件: {string.Join(", ", availablePrompts)}");
                }

                // 加载系统提示
                Console.WriteLine("📋 正在加载系统提示...");
                var systemContext = await promptManager.LoadSystemPromptAsync();
                
                chatHistory.AddSystemMessage(systemContext);
                Console.WriteLine("✅ 系统提示加载完成");
            }

            Console.WriteLine("🤖 多Agent系统已准备就绪！");
            Console.WriteLine("💡 系统包含：主Agent（DeepSeek）+ 副Agent（Gemini验证）");
            Console.WriteLine("📝 输入任务，系统将自动进行验证和改进。输入 'exit' 退出程序。\n");

            // 声明变量（移到这里，在使用之前）
            bool useMultiAgent = false;

            // 添加模式选择
            while (true)
            {
                Console.WriteLine("请选择运行模式：");
                Console.WriteLine("1. 多Agent模式（主Agent + 验证Agent）");
                Console.WriteLine("2. 单Agent模式（仅主Agent）");
                Console.WriteLine("输入 'exit' 退出程序");
                Console.Write("选择模式 (1/2): ");
                
                var modeChoice = Console.ReadLine();
                
                // 检查是否要退出
                if (string.IsNullOrEmpty(modeChoice) || modeChoice.ToLower() == "exit")
                {
                    Console.WriteLine("程序已退出。");
                    return; // 直接退出程序
                }
                
                if (modeChoice == "1")
                {
                    Console.WriteLine("🔄 使用多Agent模式");
                    useMultiAgent = true;
                    break;
                }
                else if (modeChoice == "2")
                {
                    Console.WriteLine("🤖 使用单Agent模式");
                    useMultiAgent = false;
                    break;
                }
                else
                {
                    Console.WriteLine("❌ 无效选择，请输入 1 或 2");
                    continue; // 重新提示用户选择
                }
            }

            Console.WriteLine("\n💡 提示：输入 'reload-prompt' 可重新加载系统提示");
            Console.WriteLine();

            // 聊天循环
            var chatService = mainKernel.GetRequiredService<IChatCompletionService>();

            while (true)
            {
                Console.Write("User > ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit") 
                {
                    break;
                }

                // 添加重新加载系统提示的命令
                if (input.ToLower() == "reload-prompt")
                {
                    try
                    {
                        Console.WriteLine("🔄 重新加载系统提示...");
                        var newSystemContext = await promptManager.ReloadSystemPromptAsync();
                        
                        // 更新聊天历史中的系统消息
                        if (chatHistory.Count > 0 && chatHistory[0].Role == AuthorRole.System)
                        {
                            chatHistory.RemoveAt(0);
                        }
                        chatHistory.Insert(0, new ChatMessageContent(
                            AuthorRole.System, 
                            newSystemContext));
                        
                        // 同时重新加载验证Agent的提示
                        if (useMultiAgent)
                        {
                            await coordinator.ReloadValidationPromptAsync();
                        }
                        
                        Console.WriteLine("✅ 系统提示已重新加载");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 重新加载系统提示失败: {ex.Message}");
                        continue;
                    }
                }
                
                try
                {
                    Console.WriteLine("Processing your request...");
                    
                    string response;
                    if (useMultiAgent)
                    {
                        // 使用多Agent模式
                        response = await coordinator.ProcessTaskWithValidationAsync(input, executionSettings);
                    }
                    else
                    {
                        // 使用单Agent模式（原有逻辑）
                        chatHistory.AddUserMessage(input);
                        var singleResponse = await chatService.GetChatMessageContentAsync(
                            chatHistory, 
                            executionSettings,
                            mainKernel);
                        chatHistory.AddAssistantMessage(singleResponse.Content!);
                        response = singleResponse.Content!;
                    }

                    Console.WriteLine($"AI > {response}");
                    Console.WriteLine("\n--- 任务完成，准备好接受下一个命令 ---\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    
                    // 清除最后一条用户消息，避免重复处理
                    if (chatHistory.Count > 0 && chatHistory.Last().Role != AuthorRole.System)
                    {
                        chatHistory.RemoveAt(chatHistory.Count - 1);
                    }
                }
            }

            Console.WriteLine("多Agent系统已停止。");
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
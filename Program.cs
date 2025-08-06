using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DotNetEnv;
using SemanticKernelAgent.Models;
using SemanticKernelAgent.Services;
using SemanticKernelAgent.Agents;

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

            try
            {
                // 从环境变量创建配置
                var config = new AgentConfig
                {
                    ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                    ModelId = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL_ID"),
                    Endpoint = Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT")
                };

                var validationConfig = new ValidationConfig
                {
                    ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
                    ModelId = Environment.GetEnvironmentVariable("GEMINI_MODEL_ID"), 
                    UseGemini = true
                };

                if (string.IsNullOrEmpty(config.ApiKey))
                    throw new InvalidOperationException("DEEPSEEK_API_KEY not found in environment variables");

                if (string.IsNullOrEmpty(validationConfig.ApiKey))
                {
                    Console.WriteLine("⚠️ 警告：GEMINI_API_KEY未找到，验证Agent将不可用");
                    validationConfig = null;
                }
                else
                {
                    Console.WriteLine("✅ 验证Agent将使用Gemini API");
                }

                // 创建Agent
                var mainAgent = new MainAgent(config);
                ValidationAgent validationAgent = null;
                
                if (validationConfig != null)
                {
                    validationAgent = new ValidationAgent(config, validationConfig);
                }

                // 创建协调器（如果有验证Agent）
                MultiAgentCoordinator coordinator = null;
                if (validationAgent != null)
                {
                    coordinator = new MultiAgentCoordinator(
                        mainAgent.GetKernel(), 
                        validationAgent, 
                        mainAgent.GetChatHistory());
                }

                // 创建执行设置
                var executionSettings = new OpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    MaxTokens = 4000,
                    Temperature = 1
                };

                Console.WriteLine("🤖 多Agent系统已准备就绪！");
                Console.WriteLine($"💡 系统包含：主Agent（DeepSeek）{(validationAgent != null ? "+ 副Agent（Gemini验证）" : "")}");

                // 模式选择
                var useMultiAgent = await SelectModeAsync(validationAgent != null);

                // 聊天循环
                await StartChatLoopAsync(mainAgent, coordinator, executionSettings, useMultiAgent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 系统初始化失败: {ex.Message}");
            }

            Console.WriteLine("多Agent系统已停止。");
        }

        private static Task<bool> SelectModeAsync(bool hasValidationAgent)
        {
            while (true)
            {
                Console.WriteLine("\n请选择运行模式：");
                
                if (hasValidationAgent)
                {
                    Console.WriteLine("1. 多Agent模式（主Agent + 验证Agent）");
                }
                else
                {
                    Console.WriteLine("1. 多Agent模式（不可用 - 缺少验证Agent）");
                }
                
                Console.WriteLine("2. 单Agent模式（仅主Agent）");
                Console.WriteLine("输入 'exit' 退出程序");
                Console.Write("选择模式 (1/2): ");
                
                var choice = Console.ReadLine();
                
                if (string.IsNullOrEmpty(choice) || choice.ToLower() == "exit")
                    Environment.Exit(0);
                
                if (choice == "1" && hasValidationAgent)
                {
                    Console.WriteLine("🔄 使用多Agent模式");
                    return Task.FromResult(true);
                }
                else if (choice == "1" && !hasValidationAgent)
                {
                    Console.WriteLine("❌ 多Agent模式不可用，缺少验证Agent配置");
                    continue;
                }
                else if (choice == "2")
                {
                    Console.WriteLine("🤖 使用单Agent模式");
                    return Task.FromResult(false);
                }
                else
                {
                    Console.WriteLine("❌ 无效选择，请输入 1 或 2");
                }
            }
        }

        private static async Task StartChatLoopAsync(MainAgent mainAgent, MultiAgentCoordinator coordinator, OpenAIPromptExecutionSettings settings, bool useMultiAgent)
        {
            Console.WriteLine("\n💡 提示：输入 'reload-prompt' 重新加载系统提示，输入 'exit' 退出\n");

            while (true)
            {
                Console.Write("User > ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit") 
                    break;

                if (input.ToLower() == "reload-prompt")
                {
                    await ReloadPromptsAsync(mainAgent, coordinator, useMultiAgent);
                    continue;
                }
                
                try
                {
                    Console.WriteLine("Processing your request...");
                    
                    string response = useMultiAgent && coordinator != null
                        ? await coordinator.ProcessTaskWithValidationAsync(input, settings)
                        : await mainAgent.ProcessUserInputAsync(input, settings);

                    Console.WriteLine($"AI > {response}");
                    Console.WriteLine("\n--- 任务完成，准备好接受下一个命令 ---\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private static async Task ReloadPromptsAsync(MainAgent mainAgent, MultiAgentCoordinator coordinator, bool useMultiAgent)
        {
            try
            {
                Console.WriteLine("🔄 重新加载系统提示...");
                await mainAgent.ReloadSystemPromptAsync();
                
                if (useMultiAgent && coordinator != null)
                    await coordinator.ReloadValidationPromptAsync();
                
                Console.WriteLine("✅ 系统提示已重新加载");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 重新加载系统提示失败: {ex.Message}");
            }
        }
    }
}
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

            Console.WriteLine("正在初始化 Semantic Kernel Agent...");

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
            kernel.Plugins.AddFromType<SystemPlugin>("SystemOperations"); // 添加系统插件

            // 添加ReAct模式的函数调用监控
            kernel.FunctionInvocationFilters.Add(new ReActLoggingFilter());

            Console.WriteLine("AI已经准备就绪，可以开始处理任务。输入 'exit' 退出程序。");

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


            // 在第一次用户输入前添加系统上下文
            // 修改系统上下文，强制要求联网搜索
            if (chatHistory.Count == 0)
            {
                var systemContext = @"我是一个运行在以下环境的AI助手：

## 重要规则：
- 当用户要求整理资料、获取资讯或创建内容页面时，必须先进行联网搜索
- 搜索步骤是强制性的，不能跳过
- 基于搜索结果创建内容，而不是使用训练数据

## 图片处理能力：
- 可以使用WebOperations.DownloadFileAsync下载图片
- 可以使用WebOperations.GetImageInfoAsync获取图片信息
- 可以使用CliOperations调用curl/wget下载图片
- 支持常见图片格式：jpg, png, gif, webp等

## 工作流程（严格遵守）：
1. 分析用户需求
2. 如果涉及时间信息，先获取当前日期时间  
3. 如果需要资料信息，必须先调用WebOperations.SearchAsync搜索相关内容
4. 如果需要图片，使用WebOperations.DownloadFileAsync下载
5. 基于搜索结果整理信息
6. 创建文件或页面
7. 使用适当的CLI命令完成任务

## 技术规范：
- 请在执行任何命令前先了解系统环境
- 根据操作系统选择合适的命令和工具
- Windows使用PowerShell或CMD，Unix使用bash
- 执行命令前可以检查程序是否已安装
- 请尽量使用CLI命令来完成任务
- 处理图片时注意文件路径和格式

## 搜索要求：
- 搜索关键词要具体和相关
- 搜索后要提取有用信息
- 基于真实搜索结果而不是想象创建内容";
                
                chatHistory.AddSystemMessage(systemContext);
            }

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

                    Console.WriteLine("\n--- 任务完成，准备好接受下一个命令 ---\n");
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
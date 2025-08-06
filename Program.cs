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

            Console.WriteLine("正在初始化多Agent系统...");

            // 从环境变量创建主Agent配置
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

            // 创建验证Agent配置（使用Gemini）
            var validationConfig = new ValidationConfig
            {
                ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "",
                ModelId = Environment.GetEnvironmentVariable("GEMINI_MODEL_ID") ?? "gemini-pro",
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

            // 添加系统上下文
            if (chatHistory.Count == 0)
            {
                var systemContext = @"我是一个运行在以下环境的AI助手：

## 重要规则：
- 当用户要求整理资料、获取资讯或创建内容页面时，必须先进行联网搜索
- 搜索步骤是强制性的，不能跳过
- 基于搜索结果创建内容，而不是使用训练数据

## 网络访问策略：
- 使用WebOperations.SearchAsync进行信息收集，基于Tavily AI搜索引擎
- 使用WebOperations.DeepSearchAsync进行深度搜索，获取更详细信息
- Tavily提供AI增强的搜索结果，包含智能摘要和相关度评分
- 自动处理反爬虫限制，提供可靠的搜索结果
- 基于多源信息整合，确保内容的准确性和时效性

## Tavily搜索引擎特点：
- AI增强搜索：提供智能摘要和答案
- 实时信息：获取最新的网络内容
- 高质量结果：相关度评分和内容筛选
- 多源整合：从多个可靠源获取信息
- 反爬虫绕过：稳定的网络访问能力
- 支持深度搜索：获取更详细的原始内容

## 搜索功能说明：
- SearchAsync: 标准搜索，适合一般信息查询
- DeepSearchAsync: 深度搜索，包含原始内容和图片
- GetWebPageTextAsync: 提取特定网页的完整内容
- TestTavilyConnectionAsync: 测试API连接状态

## 工作流程（严格遵守）：
1. 分析用户需求
2. 如果涉及时间信息，先获取当前日期时间  
3. 如果需要资料信息，必须先调用WebOperations.SearchAsync搜索相关内容
4. 根据搜索结果的详细程度，决定是否需要使用DeepSearchAsync获取更多信息
5. 如果需要特定网页的详细内容，使用GetWebPageTextAsync
6. 如果搜索失败，立即使用WebOperations.GetAlternativeSearchSuggestions
7. 基于Tavily的AI摘要和搜索结果整理信息
8. 创建文件或页面
9. 使用适当的CLI命令完成任务

## 技术规范（严格遵守）：
- 请在执行任何命令前先了解系统环境
- 根据操作系统选择合适的命令和工具
- Windows使用PowerShell或CMD，Unix使用bash
- 执行命令前可以检查程序是否已安装
- 请尽量使用CLI命令来完成任务
- 处理图片时注意文件路径和格式

## 搜索要求：
- 搜索关键词要具体和相关
- 优先使用Tavily的AI摘要功能获取准确信息
- 对于复杂主题，使用DeepSearchAsync获取详细内容
- 基于真实搜索结果而不是想象创建内容
- 充分利用Tavily的相关度评分选择最佳结果
- 使用多样化的搜索关键词组合来获取更全面的信息

## 容错处理：
- 如果Tavily API访问失败，使用GetAlternativeSearchSuggestions
- 检查TAVILY_API_KEY是否正确配置
- 验证API配额和使用限制
- 优先创建基于AI摘要的综合内容
- 提供详细的错误诊断和解决建议

## Tavily搜索策略：
- 利用Tavily的AI能力获取智能摘要
- 基于相关度评分筛选最佳结果  
- 结合标准搜索和深度搜索获取全面信息
- 自动处理网络访问限制和错误
- 提供详细的搜索过程反馈和结果分析;";
    
    chatHistory.AddSystemMessage(systemContext);
}

            Console.WriteLine("🤖 多Agent系统已准备就绪！");
            Console.WriteLine("💡 系统包含：主Agent（DeepSeek）+ 副Agent（Gemini验证）");
            Console.WriteLine("📝 输入任务，系统将自动进行验证和改进。输入 'exit' 退出程序。\n");

            // 添加模式选择
            Console.WriteLine("请选择运行模式：");
            Console.WriteLine("1. 多Agent模式（主Agent + 验证Agent）");
            Console.WriteLine("2. 单Agent模式（仅主Agent）");
            Console.Write("选择模式 (1/2): ");
            
            var modeChoice = Console.ReadLine();
            bool useMultiAgent = modeChoice == "1" || string.IsNullOrEmpty(modeChoice);
            
            Console.WriteLine(useMultiAgent ? "🔄 使用多Agent模式" : "🤖 使用单Agent模式");
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
                    if (chatHistory.Count > 0)
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
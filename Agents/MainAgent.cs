using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelAgent.Services;
using SemanticKernelAgent.Models;
using System.Threading.Tasks;
using System;

namespace SemanticKernelAgent.Agents
{
    public class MainAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatService;
        private readonly ChatHistory _chatHistory;
        private readonly PromptManager _promptManager;

        public MainAgent(AgentConfig config)
        {
            var kernelBuilder = Kernel.CreateBuilder();
            _promptManager = new PromptManager();

            Console.WriteLine("🔧 初始化主Agent (DeepSeek API)...");
            
            try
            {
                // 创建主Agent内核
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: config.ModelId,
                    apiKey: config.ApiKey,
                    endpoint: new Uri(config.Endpoint));

                _kernel = kernelBuilder.Build();

                // 添加插件
                _kernel.Plugins.AddFromType<FilePlugin>("FileOperations");
                _kernel.Plugins.AddFromType<WebPlugin>("WebOperations");
                _kernel.Plugins.AddFromType<CliPlugin>("CliOperations");
                _kernel.Plugins.AddFromType<SystemPlugin>("SystemOperations");

                // 添加ReAct模式的函数调用监控
                _kernel.FunctionInvocationFilters.Add(new ReActLoggingFilter());

                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
                _chatHistory = new ChatHistory();

                Console.WriteLine("✅ 主Agent初始化完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 主Agent初始化失败: {ex.Message}");
                throw new InvalidOperationException($"无法初始化主Agent: {ex.Message}", ex);
            }

            // 加载系统提示
            InitializeSystemPromptAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeSystemPromptAsync()
        {
            try
            {
                // 检查是否有可用的系统提示文件
                var availablePrompts = _promptManager.GetAvailableSystemPrompts();
                if (availablePrompts.Length > 0)
                {
                    Console.WriteLine($"📝 发现 {availablePrompts.Length} 个系统提示文件: {string.Join(", ", availablePrompts)}");
                }

                Console.WriteLine("📋 正在加载系统提示...");
                var systemPrompt = await _promptManager.LoadSystemPromptAsync();
                _chatHistory.AddSystemMessage(systemPrompt);
                Console.WriteLine("✅ 系统提示加载完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载主Agent系统提示失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 处理用户输入并返回响应
        /// </summary>
        public async Task<string> ProcessUserInputAsync(string userInput, OpenAIPromptExecutionSettings executionSettings)
        {
            try
            {
                _chatHistory.AddUserMessage(userInput);

                var response = await _chatService.GetChatMessageContentAsync(
                    _chatHistory,
                    executionSettings,
                    _kernel);

                _chatHistory.AddAssistantMessage(response.Content!);
                return response.Content!;
            }
            catch (Exception ex)
            {
                // 出错时移除最后添加的用户消息
                if (_chatHistory.Count > 0 && _chatHistory[^1].Role == AuthorRole.User)
                {
                    _chatHistory.RemoveAt(_chatHistory.Count - 1);
                }
                throw new InvalidOperationException($"主Agent处理失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重新加载系统提示
        /// </summary>
        public async Task ReloadSystemPromptAsync()
        {
            try
            {
                var newPrompt = await _promptManager.ReloadSystemPromptAsync();
                
                // 更新聊天历史中的系统消息
                if (_chatHistory.Count > 0 && _chatHistory[0].Role == AuthorRole.System)
                {
                    _chatHistory.RemoveAt(0);
                }
                _chatHistory.Insert(0, new ChatMessageContent(AuthorRole.System, newPrompt));
                
                Console.WriteLine("✅ 主Agent系统提示已重新加载");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 重新加载主Agent系统提示失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取聊天历史（用于协调器）
        /// </summary>
        public ChatHistory GetChatHistory() => _chatHistory;

        /// <summary>
        /// 获取内核（用于协调器）
        /// </summary>
        public Kernel GetKernel() => _kernel;
    }
}

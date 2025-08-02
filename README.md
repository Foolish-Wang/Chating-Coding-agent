# Semantic Kernel Agent

一个基于 Microsoft Semantic Kernel 构建的智能 AI 代理，具备文件操作、网络搜索、命令行执行等多种能力，采用 ReAct 模式进行推理和执行。

## 🌟 特性

- **ReAct 模式推理**：可视化的思考-行动-观察循环
- **多插件支持**：文件操作、网络搜索、CLI 执行、系统信息获取
- **智能搜索**：强制联网获取最新信息，避免基于过时训练数据的回答
- **图片处理**：支持图片下载和信息获取
- **跨平台 CLI 支持**：智能选择 Windows PowerShell 或 Unix Shell
- **实时监控**：详细的函数调用日志和执行过程追踪

## 🚀 快速开始

### 环境要求

- .NET 8.0 或更高版本
- DeepSeek API 密钥（或其他兼容 OpenAI 的 API）

### 安装

1. 克隆项目

```bash
git clone <repository-url>
cd sk-agent
```

2. 配置环境变量
   创建 `.env` 文件：

```env
DEEPSEEK_API_KEY=your_api_key_here
DEEPSEEK_MODEL_ID=deepseek-chat
DEEPSEEK_ENDPOINT=https://api.deepseek.com/
```

3. 运行项目

```bash
dotnet run
```

## 🔧 核心功能

### 插件系统

#### FilePlugin - 文件操作

- `CreateFile` - 创建文件
- `ReadFile` - 读取文件内容
- `WriteFile` - 写入文件
- `DeleteFile` - 删除文件
- `CreateDirectory` - 创建目录
- `ListFiles` - 列出文件

#### WebPlugin - 网络操作

- `SearchAsync` - 网络搜索
- `GetWebPageAsync` - 获取网页内容
- `GetWebPageTextAsync` - 提取网页文本
- `DownloadFileAsync` - 下载文件/图片
- `GetImageInfoAsync` - 获取图片信息

#### CliPlugin - 命令行执行

- `ExecuteCommandAsync` - 执行系统命令
- `ExecutePowerShellAsync` - 执行 PowerShell 命令
- `SmartExecuteAsync` - 智能选择执行方式

#### SystemPlugin - 系统信息

- `GetOperatingSystem` - 获取操作系统信息
- `GetCurrentDirectory` - 获取当前工作目录
- `CheckProgramInstalled` - 检查程序是否安装
- `GetRecommendedCliTool` - 获取推荐的 CLI 工具

### ReAct 模式

项目采用 ReAct（Reasoning + Acting）模式，每次执行都会显示：

```
🔧 Action 1: WebOperations.SearchAsync
   Parameters: query=Claude Code 相关资讯...
✅ Observation 1: 搜索结果显示...

🔧 Action 2: FileOperations.CreateFile
   Parameters: filePath=output.html, content=...
✅ Observation 2: 文件已创建成功
```

## 💡 使用示例

### 创建网页内容

```
在当前文件夹中建立"资讯"文件夹，写一个美观的html页面，内容是关于任天堂发布会的相关资讯介绍，并且附带相关的新闻图片，完成后请帮我在浏览器中运行
```

### 制作旅行计划

```
帮我拟定一个从明天开始，持续三天的杭州旅行计划，包含景点、酒店以及餐馆，以及对应的时间，做成一个美观的、可以交互的网页，要包含景点、酒店、餐馆对应的跳转链接，拟定旅行计划的时候必须联网搜索资料并且参考天气信息,制作完成后使用CLI在浏览器中打开这个网页
```

### 代码项目开发

```
在当前文件夹中创建一个新文件夹，在里面写一个简单的贪吃蛇游戏，并在完成后运行这个游戏
```

## 🏗️ 项目结构

```
sk-agent/
├── Program.cs              # 主程序入口
├── Models/
│   └── AgentConfig.cs      # 配置模型
├── Plugins/                # 插件系统
│   ├── FilePlugin.cs       # 文件操作插件
│   ├── WebPlugin.cs        # 网络操作插件
│   ├── CliPlugin.cs        # CLI执行插件
│   ├── SystemPlugin.cs     # 系统信息插件
│   └── SamplePlugin.cs     # 示例插件
├── Agent/
│   └── AgentService.cs     # 代理服务
├── sk-agent.csproj         # 项目文件
├── .env                    # 环境变量（需要创建）
└── README.md               # 项目说明
```

## ⚙️ 配置说明

### 环境变量

| 变量名              | 描述              | 默认值                      |
| ------------------- | ----------------- | --------------------------- |
| `DEEPSEEK_API_KEY`  | DeepSeek API 密钥 | 必需                        |
| `DEEPSEEK_MODEL_ID` | 使用的模型 ID     | `deepseek-chat`             |
| `DEEPSEEK_ENDPOINT` | API 端点          | `https://api.deepseek.com/` |

### 执行设置

```csharp
var executionSettings = new OpenAIPromptExecutionSettings()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
    MaxTokens = 4000,
    Temperature = 1
};
```

请自行参照 Deepseek 官方的 API 文档正确设置 Temperature 等参数。

## 🤖 AI 行为规则

Agent 严格遵循以下规则：

1. **强制联网搜索**：涉及资讯、新闻、最新信息时必须先搜索
2. **基于真实数据**：不基于训练数据编造内容
3. **系统感知**：根据操作系统选择合适的命令
4. **图片处理**：支持图片下载和集成到内容中
5. **完整工作流**：分析 → 搜索 → 创建 → 验证 → 输出

## 🔍 调试和监控

项目内置了详细的函数调用监控：

- **Function Call Logging**：显示每个插件函数的调用过程
- **Parameter Tracking**：记录传入参数
- **Result Monitoring**：显示执行结果
- **Error Handling**：完善的错误处理和恢复机制

## 🛠️ 开发

### 添加新插件

1. 在 `Plugins/` 目录创建新的插件类
2. 使用 `[KernelFunction]` 属性标记方法
3. 在 `Program.cs` 中注册插件：

```csharp
kernel.Plugins.AddFromType<YourPlugin>("YourPluginName");
```

### 自定义 ReAct 行为

修改 `ReActLoggingFilter` 类来自定义推理过程的显示：

```csharp
public class ReActLoggingFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        // 自定义前置处理
        await next(context);
        // 自定义后置处理
    }
}
```

## 📦 依赖项

- **Microsoft.SemanticKernel** (1.61.0) - 核心框架
- **DotNetEnv** (3.1.1) - 环境变量管理
- **Microsoft.Extensions.DependencyInjection** (8.0.1) - 依赖注入
- **Microsoft.Extensions.Configuration** (8.0.0) - 配置管理
- **Microsoft.Extensions.Hosting** (8.0.0) - 主机服务

## 🤝 贡献

欢迎提交 Issue 和 Pull Request 来改进这个项目！

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) - 核心 AI 框架
- [DeepSeek](https://www.deepseek.com/) - AI 模型服务提供商

---

**注意**：使用前请确保已正确配置 API 密钥，并遵守相应的使用条款和限制。

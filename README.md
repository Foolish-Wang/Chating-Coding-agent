# Semantic Kernel Multi-Agent System

一个基于 Microsoft Semantic Kernel 构建的智能多 Agent 系统，具备文件操作、网络搜索、命令行执行等多种能力，采用 ReAct 模式进行推理和执行，并集成了 Gemini 验证 Agent 进行结果质量保证。

## 🌟 特性

- **多 Agent 架构**：主 Agent（DeepSeek）+ 验证 Agent（Gemini）协作模式
- **智能验证系统**：自动验证任务完成质量并提供改进建议
- **ReAct 模式推理**：可视化的思考-行动-观察循环
- **Tavily 智能搜索**：基于 AI 增强的搜索引擎，获取最新准确信息
- **多插件支持**：文件操作、网络搜索、CLI 执行、系统信息获取
- **动态提示管理**：支持运行时重新加载系统提示
- **跨平台 CLI 支持**：智能选择 Windows PowerShell 或 Unix Shell
- **实时监控**：详细的函数调用日志和执行过程追踪

## 🏗️ 架构设计

### 多 Agent 协作模式

```
用户输入 → 主Agent处理 → 验证Agent检查 → 主Agent改进（如需要） → 最终输出
```

- **主 Agent (DeepSeek)**：负责任务执行、函数调用、内容生成
- **验证 Agent (Gemini)**：负责质量检查、错误识别、改进建议

### 文件结构

```
sk-agent/
├── Program.cs                      # 主程序入口
├── Models/                         # 数据模型
│   ├── AgentConfig.cs             # Agent配置模型
│   └── ValidationModels.cs        # 验证相关数据模型
├── Agents/                         # Agent实现
│   ├── MainAgent.cs               # 主Agent实现
│   └── ValidationAgent.cs         # 验证Agent实现
├── Services/                       # 服务层
│   ├── MultiAgentCoordinator.cs   # 多Agent协调服务
│   ├── PromptManager.cs           # 提示管理服务
│   └── ReActLoggingFilter.cs      # ReAct日志过滤器
├── Plugins/                        # 插件系统
│   ├── FilePlugin.cs              # 文件操作插件
│   ├── WebPlugin.cs               # Tavily搜索插件
│   ├── CliPlugin.cs               # CLI执行插件
│   └── SystemPlugin.cs            # 系统信息插件
├── Prompts/                        # 系统提示
│   ├── SystemPrompt.md            # 主Agent系统提示
│   └── ValidationSystemPrompt.md  # 验证Agent系统提示
├── .env                           # 环境变量配置
└── README.md                      # 项目说明
```

## 🚀 快速开始

### 环境要求

- .NET 8.0 或更高版本
- DeepSeek API 密钥（主 Agent）
- Google Gemini API 密钥（验证 Agent）
- Tavily API 密钥（智能搜索）

### 安装

1. 克隆项目

```powershell
git clone <repository-url>
cd sk-agent
```

2. 安装依赖

```powershell
dotnet restore
```

3. 配置环境变量
   创建 `.env` 文件：

```env
# 主Agent配置 (DeepSeek)
DEEPSEEK_API_KEY=your_deepseek_api_key_here
DEEPSEEK_MODEL_ID=deepseek-chat
DEEPSEEK_ENDPOINT=https://api.deepseek.com/

# 验证Agent配置 (Google Gemini)
GEMINI_API_KEY=your_gemini_api_key_here
GEMINI_MODEL_ID=gemini-2.5-flash

# 搜索服务配置 (Tavily)
TAVILY_API_KEY=your_tavily_api_key_here
```

4. 运行项目

```powershell
dotnet run
```

## 🔧 核心功能

### 多 Agent 系统

#### 运行模式选择

- **多 Agent 模式**：主 Agent + 验证 Agent 协作，提供质量保证
- **单 Agent 模式**：仅使用主 Agent，快速执行任务

#### 验证流程

1. 主 Agent 完成任务
2. 验证 Agent 检查结果质量
3. 如发现问题，主 Agent 根据反馈改进
4. 输出最终结果和验证反馈

### 插件系统

#### FilePlugin - 文件操作

```csharp
CreateFileAsync(filePath, content)    // 创建文件
ReadFileAsync(filePath)               // 读取文件内容
WriteFileAsync(filePath, content)     // 写入文件
DeleteFile(filePath)                  // 删除文件
CreateDirectory(directoryPath)        // 创建目录
ListFiles(directoryPath)              // 列出文件
ReplaceInFileAsync(filePath, old, new) // 替换文件内容
AppendToFileAsync(filePath, content)   // 追加文件内容
```

#### WebPlugin - Tavily 智能搜索

```csharp
SearchAsync(query)                    // 标准智能搜索
DeepSearchAsync(query)                // 深度搜索，包含原始内容
GetWebPageTextAsync(url)              // 提取网页内容
GetAlternativeSearchSuggestions(query) // 备用搜索策略
TestTavilyConnectionAsync()           // 测试API连接
GetCurrentDateTime()                  // 获取当前时间
```

#### CliPlugin - 命令行执行

```csharp
ExecuteCommandAsync(command)          // 执行系统命令
ExecutePowerShellAsync(command)       // 执行PowerShell命令
SmartExecuteAsync(command)            // 智能选择执行方式
```

#### SystemPlugin - 系统信息

```csharp
GetOperatingSystem()                  // 获取操作系统信息
GetEnvironmentVariable(name)          // 获取环境变量
CheckProgramInstalled(program)        // 检查程序安装状态
GetRecommendedCliTool()              // 获取推荐CLI工具
```

### Tavily 智能搜索

集成了 Tavily AI 搜索引擎，提供：

- **AI 增强搜索**：智能摘要和答案生成
- **实时信息获取**：最新网络内容
- **高质量结果**：相关度评分和内容筛选
- **多源整合**：从多个可靠源获取信息
- **深度搜索**：包含原始网页内容和图片

### 动态提示管理

- 支持 Markdown 格式的系统提示文件
- 运行时重新加载提示（使用 `reload-prompt` 命令）
- 分离的主 Agent 和验证 Agent 提示管理
- 多语言提示支持

## 💡 使用示例

### 创建智能网页

```
帮我联网查阅相关资料，拟定一个从明天开始，持续三天的杭州西湖区旅行计划，包含具体的景点、酒店以及餐馆的选择，以及对应的时间安排。将旅行计划做成一个美观的、可以交互的网页，并且加上中国境内可以用的餐饮、住宿平台的跳转链接，拟定旅行计划的时候必须参考天气信息，并且将天气以及天气数据来源显示在网页中。制作网页完成后使用CLI在浏览器中打开这个网页。
```

### 资讯收集和整理

```
在当前文件夹中建立"资讯"文件夹，写一个美观的html页面，内容是关于最新科技新闻的相关资讯介绍，并且附带相关的新闻图片，完成后请帮我在浏览器中运行
```

### ReAct 执行示例

当需要函数调用时，系统会显示详细的推理过程：

```
🔧 Action 1: WebOperations.GetCurrentDateTime
   Parameters:
✅ Observation 1: 2025年08月06日 16:37:40 星期三

🔧 Action 2: WebOperations.SearchAsync
   Parameters: query=杭州西湖区未来三天天气预报...
✅ Observation 2: 🎯 Tavily搜索结果: 天气情况为晴到多云，温度26-34°C...

🔧 Action 3: FileOperations.CreateFile
   Parameters: filePath=travel_plan.html, content=<!DOCTYPE html>...
✅ Observation 3: 文件已创建: travel_plan.html
```

## ⚙️ 配置说明

### API 配置

| 环境变量            | 描述                   | 必需 | 说明                   |
| ------------------- | ---------------------- | ---- | ---------------------- |
| `DEEPSEEK_API_KEY`  | DeepSeek API 密钥      | ✅   | -                      |
| `DEEPSEEK_MODEL_ID` | DeepSeek 模型 ID       | ✅   | -                      |
| `DEEPSEEK_ENDPOINT` | DeepSeek API 端点      | ✅   | -                      |
| `GEMINI_API_KEY`    | Google Gemini API 密钥 | ❌   | - (无则禁用验证 Agent) |
| `GEMINI_MODEL_ID`   | Gemini 模型 ID         | ❌   | -                      |
| `TAVILY_API_KEY`    | Tavily 搜索 API 密钥   | ❌   | - (无则搜索功能受限)   |

### 执行设置

```csharp
var executionSettings = new OpenAIPromptExecutionSettings()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
    MaxTokens = 4000,
    Temperature = 1
};
```

## 🤖 AI 行为规则

### 主 Agent 规则

1. **强制联网搜索**：涉及资讯、新闻、最新信息时必须先搜索
2. **基于真实数据**：优先使用 Tavily 搜索结果而非训练数据
3. **系统感知**：根据操作系统选择合适的命令
4. **完整工作流**：分析 → 搜索 → 创建 → 执行 → 验证

### 验证 Agent 规则

1. **完整性检查**：验证任务是否完全完成
2. **质量评估**：检查准确性、实用性和专业性
3. **格式规范**：检查 HTML、代码、文档格式
4. **错误识别**：发现技术、逻辑或信息错误
5. **改进建议**：提供具体可操作的修改建议

## 🔍 调试和监控

### ReAct 日志系统

- **Action 显示**：每个函数调用的详细参数
- **Observation 记录**：执行结果和返回值
- **执行计数**：跟踪推理步骤数量
- **参数截断**：避免过长参数影响可读性

### 验证反馈格式

```
🔍 验证结果
**总体评分**：8/10

✅ 完成良好的方面
- 任务完成度高
- 信息准确性好

❌ 发现的问题
1. **问题类型**：信息缺失
   - 具体说明：缺少时间安排
   - 影响程度：中

🔧 改进建议
1. 补充详细的时间安排
2. 增加交通方式说明
```

## 🛠️ 开发指南

### 添加新 Agent

1. 在 `Agents/` 目录创建新的 Agent 类
2. 实现必要的接口和方法
3. 在 `Program.cs` 中注册 Agent

### 自定义验证规则

修改 `Prompts/ValidationSystemPrompt.md`：

```markdown
## 检查标准：

- ✅ 任务要求完成度
- ✅ 信息准确性
- ✅ 格式规范性
- ✅ 用户体验
- ✅ 技术可行性
```

### 扩展插件功能

```csharp
[KernelFunction]
[Description("你的函数描述")]
public async Task<string> YourFunctionAsync(string parameter)
{
    // 实现你的功能
    return "执行结果";
}
```

## 📦 依赖项

- **Microsoft.SemanticKernel** (1.61.0) - 核心 AI 框架
- **Microsoft.SemanticKernel.Connectors.Google** (1.0.1) - Gemini 连接器
- **DotNetEnv** (3.1.1) - 环境变量管理
- **System.Text.Json** (8.0.0) - JSON 序列化

## ⚠️ 注意事项

1. **API 配额管理**：注意各 API 服务的调用限制
2. **网络依赖**：Tavily 搜索需要稳定的网络连接
3. **模型选择**：根据任务复杂度选择合适的模型
4. **错误处理**：系统具备完善的错误恢复机制

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 贡献指南

1. Fork 项目
2. 创建特性分支
3. 提交更改
4. 发起 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) - 核心 AI 框架
- [DeepSeek](https://www.deepseek.com/) - 主 Agent 模型服务
- [Google Gemini](https://ai.google.dev/) - 验证 Agent 服务
- [Tavily](https://tavily.com/) - AI 增强搜索服务

---

**注意**：使用前请确保已正确配置所需的 API 密钥，并遵守相应的使用条款和限制。多 Agent 模式需要同时配置 DeepSeek 和 Gemini API 密钥。

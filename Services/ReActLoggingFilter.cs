using Microsoft.SemanticKernel;
using System;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable SKEXP0070

namespace SemanticKernelAgent.Services
{
    /// <summary>
    /// ReAct模式的函数调用监控Filter
    /// 用于显示AI推理过程中的Action和Observation步骤
    /// </summary>
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

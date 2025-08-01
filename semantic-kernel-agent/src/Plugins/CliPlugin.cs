using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

public class CliPlugin
{
    [KernelFunction]
    [Description("执行命令行命令")]
    public async Task<string> ExecuteCommandAsync(string command, string workingDirectory = "")
    {
        try
        {
            var processInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory
            };

            using var process = Process.Start(processInfo);
            if (process == null) return "无法启动进程";

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = $"退出代码: {process.ExitCode}\n";
            if (!string.IsNullOrEmpty(output)) result += $"输出:\n{output}\n";
            if (!string.IsNullOrEmpty(error)) result += $"错误:\n{error}";

            return result;
        }
        catch (Exception ex)
        {
            return $"执行命令失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("执行PowerShell命令")]
    public async Task<string> ExecutePowerShellAsync(string command, string workingDirectory = "")
    {
        try
        {
            var processInfo = new ProcessStartInfo("powershell.exe", $"-Command \"{command}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory
            };

            using var process = Process.Start(processInfo);
            if (process == null) return "无法启动PowerShell进程";

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = $"退出代码: {process.ExitCode}\n";
            if (!string.IsNullOrEmpty(output)) result += $"输出:\n{output}\n";
            if (!string.IsNullOrEmpty(error)) result += $"错误:\n{error}";

            return result;
        }
        catch (Exception ex)
        {
            return $"执行PowerShell命令失败: {ex.Message}";
        }
    }
}
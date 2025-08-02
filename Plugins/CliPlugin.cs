using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;

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

    [KernelFunction]
    [Description("智能执行系统命令，自动处理错误")]
    public async Task<string> SmartExecuteAsync(string command)
    {
        try
        {
            // Windows系统使用更干净的执行方式
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 使用 -NoProfile 避免配置文件错误
                var cleanCommand = $"powershell -NoProfile -Command \"{command}\"";

                var processInfo = new ProcessStartInfo("cmd", $"/c {cleanCommand}")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    // 设置编码避免乱码
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(processInfo);
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                var result = $"退出代码: {process.ExitCode}";
                if (!string.IsNullOrEmpty(output))
                    result += $"\n输出:\n{output}";
                if (!string.IsNullOrEmpty(error))
                    result += $"\n警告:\n{error}";

                return result;
            }

            // Unix系统处理...
            return await ExecuteUnixCommand(command);
        }
        catch (Exception ex)
        {
            return $"执行失败: {ex.Message}";
        }
    }

    private async Task<string> ExecuteUnixCommand(string command)
    {
        var processInfo = new ProcessStartInfo("/bin/bash", $"-c \"{command}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
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
}
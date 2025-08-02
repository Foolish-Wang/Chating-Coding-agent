using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;

public class SystemPlugin
{
    [KernelFunction]
    [Description("获取当前操作系统信息")]
    public string GetOperatingSystem()
    {
        var os = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            os = "Windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            os = "Linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            os = "macOS";
        else
            os = "Unknown";

        return $"操作系统: {os}\n" +
               $"架构: {RuntimeInformation.OSArchitecture}\n" +
               $".NET版本: {Environment.Version}\n" +
               $"机器名: {Environment.MachineName}";
    }

    [KernelFunction]
    [Description("获取系统环境变量")]
    public string GetEnvironmentVariable(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrEmpty(value) ? 
            $"环境变量 {variableName} 未设置" : 
            $"{variableName} = {value}";
    }

    [KernelFunction]
    [Description("获取当前工作目录")]
    public string GetCurrentDirectory()
    {
        return $"当前工作目录: {Directory.GetCurrentDirectory()}";
    }

    [KernelFunction]
    [Description("检查程序是否已安装")]
    public string CheckProgramInstalled(string programName)
    {
        try
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
                return $"无法检查 {programName}：PATH 环境变量为空";

            var paths = pathVariable.Split(Path.PathSeparator);
            var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? new[] { ".exe", ".cmd", ".bat", ".com" }
                : new[] { "" };

            foreach (var path in paths)
            {
                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(path, programName + ext);
                    if (File.Exists(fullPath))
                    {
                        return $"✅ {programName} 已安装: {fullPath}";
                    }
                }
            }

            return $"❌ {programName} 未找到";
        }
        catch (Exception ex)
        {
            return $"检查 {programName} 时出错: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("获取推荐的命令行工具")]
    public string GetRecommendedCliTool()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "推荐使用: PowerShell (ExecutePowerShellAsync) 或 CMD (ExecuteCommandAsync)\n" +
                   "浏览器打开: start <url>\n" +
                   "文件操作: dir, type, copy, del";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "推荐使用: ExecuteCommandAsync\n" +
                   "浏览器打开: xdg-open <url>\n" +
                   "文件操作: ls, cat, cp, rm";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "推荐使用: ExecuteCommandAsync\n" +
                   "浏览器打开: open <url>\n" +
                   "文件操作: ls, cat, cp, rm";
        }
        else
        {
            return "未知操作系统，请使用 ExecuteCommandAsync";
        }
    }

    [KernelFunction]
    [Description("获取系统详细信息")]
    public string GetSystemInfo()
    {
        return $"操作系统: {Environment.OSVersion}\n" +
               $"处理器数量: {Environment.ProcessorCount}\n" +
               $"系统目录: {Environment.SystemDirectory}\n" +
               $"用户名: {Environment.UserName}\n" +
               $"用户域: {Environment.UserDomainName}\n" +
               $"工作目录: {Directory.GetCurrentDirectory()}";
    }
}
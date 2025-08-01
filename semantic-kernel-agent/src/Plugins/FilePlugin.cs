using Microsoft.SemanticKernel;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

public class FilePlugin
{
    [KernelFunction]
    [Description("读取文件内容")]
    public async Task<string> ReadFileAsync(string filePath)
    {
        try
        {
            return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            return $"读取文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("写入文件内容")]
    public async Task<string> WriteFileAsync(string filePath, string content)
    {
        try
        {
            await File.WriteAllTextAsync(filePath, content);
            return $"文件已写入: {filePath}";
        }
        catch (Exception ex)
        {
            return $"写入文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("列出目录中的文件")]
    public string ListFiles(string directoryPath)
    {
        try
        {
            var files = Directory.GetFiles(directoryPath);
            return string.Join("\n", files);
        }
        catch (Exception ex)
        {
            return $"列出文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("在指定路径创建文件")]
    public async Task<string> CreateFileAsync(string filePath, string content = "")
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(filePath, content);
            return $"文件已创建: {filePath}";
        }
        catch (Exception ex)
        {
            return $"创建文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("创建目录")]
    public string CreateDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            return $"目录已创建: {directoryPath}";
        }
        catch (Exception ex)
        {
            return $"创建目录失败: {ex.Message}";
        }
    }
}
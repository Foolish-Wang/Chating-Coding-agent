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
}
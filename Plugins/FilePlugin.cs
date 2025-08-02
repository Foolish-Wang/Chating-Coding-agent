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

    [KernelFunction]
    [Description("删除文件")]
    public string DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return $"文件已删除: {filePath}";
            }
            else
            {
                return $"文件不存在: {filePath}";
            }
        }
        catch (Exception ex)
        {
            return $"删除文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("删除目录及其所有内容")]
    public string DeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true); // true表示递归删除所有内容
                return $"目录已删除: {directoryPath}";
            }
            else
            {
                return $"目录不存在: {directoryPath}";
            }
        }
        catch (Exception ex)
        {
            return $"删除目录失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("检查文件是否存在")]
    public string FileExists(string filePath)
    {
        return File.Exists(filePath) ? $"文件存在: {filePath}" : $"文件不存在: {filePath}";
    }

    [KernelFunction]
    [Description("检查目录是否存在")]
    public string DirectoryExists(string directoryPath)
    {
        return Directory.Exists(directoryPath) ? $"目录存在: {directoryPath}" : $"目录不存在: {directoryPath}";
    }

    [KernelFunction]
    [Description("在文件末尾追加内容")]
    public async Task<string> AppendToFileAsync(string filePath, string content)
    {
        try
        {
            await File.AppendAllTextAsync(filePath, content);
            return $"内容已追加到文件: {filePath}";
        }
        catch (Exception ex)
        {
            return $"追加文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("替换文件中的特定文本")]
    public async Task<string> ReplaceInFileAsync(string filePath, string oldText, string newText)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"文件不存在: {filePath}";
            }
            
            string content = await File.ReadAllTextAsync(filePath);
            string updatedContent = content.Replace(oldText, newText);
            await File.WriteAllTextAsync(filePath, updatedContent);
            
            return $"文件内容已更新: {filePath}";
        }
        catch (Exception ex)
        {
            return $"替换文件内容失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("获取文件信息")]
    public string GetFileInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return $"文件不存在: {filePath}";
            }
            
            var fileInfo = new FileInfo(filePath);
            return $"文件: {filePath}\n大小: {fileInfo.Length} 字节\n创建时间: {fileInfo.CreationTime}\n修改时间: {fileInfo.LastWriteTime}";
        }
        catch (Exception ex)
        {
            return $"获取文件信息失败: {ex.Message}";
        }
    }
}
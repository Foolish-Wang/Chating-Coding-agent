using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;

public class WebPlugin
{
    private readonly HttpClient _httpClient;

    public WebPlugin()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    [KernelFunction]
    [Description("获取网页内容（完整版本）")]
    public async Task<string> GetWebPageAsync(string url, int maxLength = 10000)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            return response.Length > maxLength ? response.Substring(0, maxLength) + "..." : response;
        }
        catch (Exception ex)
        {
            return $"获取网页失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("提取网页文本内容（去除HTML标签）")]
    public async Task<string> GetWebPageTextAsync(string url)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url);
            
            // 移除脚本和样式标签
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            // 移除所有HTML标签
            var text = Regex.Replace(html, @"<[^>]+>", " ");
            
            // 清理多余空白
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            return text.Length > 8000 ? text.Substring(0, 8000) + "..." : text;
        }
        catch (Exception ex)
        {
            return $"提取网页文本失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("搜索网络信息（使用DuckDuckGo）")]
    public async Task<string> SearchAsync(string query)
    {
        try
        {
            var searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            var html = await _httpClient.GetStringAsync(searchUrl);
            
            // 简单提取搜索结果链接
            var links = Regex.Matches(html, @"<a[^>]+href=""([^""]+)""[^>]*>([^<]+)</a>")
                .Cast<Match>()
                .Take(5)
                .Select(m => $"链接: {m.Groups[2].Value.Trim()}\nURL: {m.Groups[1].Value}")
                .ToArray();
            
            return links.Length > 0 ? string.Join("\n\n", links) : "未找到搜索结果";
        }
        catch (Exception ex)
        {
            return $"搜索失败: {ex.Message}，请尝试提供具体的URL";
        }
    }

    [KernelFunction]
    [Description("获取当前日期时间")]
    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss");
    }
}
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public class WebPlugin
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _userAgents;
    private int _currentUserAgentIndex = 0;

    public WebPlugin()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        // 多个User-Agent轮换使用
        _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        };
        
        SetUserAgent();
    }

    private void SetUserAgent()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgents[_currentUserAgentIndex]);
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    [KernelFunction]
    [Description("智能搜索网络信息，自动尝试多个搜索引擎和备用方案")]
    public async Task<string> SearchAsync(string query)
    {
        var searchEngines = new[]
        {
            new { Name = "DuckDuckGo", Url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}" },
            new { Name = "Bing", Url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}" },
            new { Name = "百度", Url = $"https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}" }
        };

        var results = new List<string>();
        
        foreach (var engine in searchEngines)
        {
            try
            {
                Console.WriteLine($"🔍 尝试使用 {engine.Name} 搜索...");
                
                // 每次尝试都换一个User-Agent
                _currentUserAgentIndex = (_currentUserAgentIndex + 1) % _userAgents.Count;
                SetUserAgent();
                
                await Task.Delay(1000); // 延迟避免频率限制
                
                var html = await _httpClient.GetStringAsync(engine.Url);
                
                if (engine.Name == "DuckDuckGo")
                {
                    var links = ExtractDuckDuckGoResults(html);
                    if (links.Any())
                    {
                        results.Add($"=== {engine.Name} 搜索结果 ===\n" + string.Join("\n\n", links));
                        break; // 成功就不再尝试其他搜索引擎
                    }
                }
                else if (engine.Name == "Bing")
                {
                    var links = ExtractBingResults(html);
                    if (links.Any())
                    {
                        results.Add($"=== {engine.Name} 搜索结果 ===\n" + string.Join("\n\n", links));
                        break;
                    }
                }
                else if (engine.Name == "百度")
                {
                    var links = ExtractBaiduResults(html);
                    if (links.Any())
                    {
                        results.Add($"=== {engine.Name} 搜索结果 ===\n" + string.Join("\n\n", links));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {engine.Name} 搜索失败: {ex.Message}");
                continue;
            }
        }

        if (results.Any())
        {
            return string.Join("\n\n", results);
        }
        
        // 如果所有搜索引擎都失败，返回建议
        return $"所有搜索引擎都无法访问。建议：\n" +
               $"1. 检查网络连接\n" +
               $"2. 尝试具体的关键词：{query}\n" +
               $"3. 可以直接提供相关网站URL";
    }

    [KernelFunction]
    [Description("智能获取网页内容，自动重试和备用方案")]
    public async Task<string> GetWebPageTextAsync(string url)
    {
        var maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // 每次重试都更换User-Agent
                _currentUserAgentIndex = (_currentUserAgentIndex + 1) % _userAgents.Count;
                SetUserAgent();
                
                if (attempt > 0)
                {
                    Console.WriteLine($"🔄 第 {attempt + 1} 次尝试访问: {url}");
                    await Task.Delay(2000 * attempt); // 递增延迟
                }
                
                var html = await _httpClient.GetStringAsync(url);
                
                // 移除脚本和样式标签
                html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                // 移除所有HTML标签
                var text = Regex.Replace(html, @"<[^>]+>", " ");
                
                // 清理多余空白
                text = Regex.Replace(text, @"\s+", " ").Trim();
                
                var result = text.Length > 8000 ? text.Substring(0, 8000) + "..." : text;
                
                if (string.IsNullOrWhiteSpace(result) || result.Length < 100)
                {
                    throw new Exception("页面内容过少或为空");
                }
                
                return result;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("403"))
            {
                Console.WriteLine($"❌ 访问被拒绝 (403): {url}");
                if (attempt == maxRetries - 1)
                {
                    return await TryAlternativeAccess(url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 第 {attempt + 1} 次尝试失败: {ex.Message}");
                if (attempt == maxRetries - 1)
                {
                    return await TryAlternativeAccess(url);
                }
            }
        }
        
        return $"无法访问网页: {url}";
    }

    [KernelFunction]
    [Description("当网页访问失败时，提供替代搜索建议")]
    public async Task<string> GetAlternativeSearchSuggestions(string originalUrl, string searchQuery)
    {
        var suggestions = new List<string>();
        
        // 分析URL，提取关键信息
        var domain = ExtractDomain(originalUrl);
        var keywords = ExtractKeywordsFromUrl(originalUrl);
        
        suggestions.Add($"原始网页 {originalUrl} 访问失败，建议替代方案：");
        suggestions.Add("");
        
        // 基于域名的替代建议
        if (domain.Contains("zhihu"))
        {
            suggestions.Add("1. 尝试搜索知乎相关内容：");
            await Task.Delay(500);
            var zhihuSearch = await SearchAsync($"{searchQuery} site:zhihu.com");
            suggestions.Add(zhihuSearch);
        }
        else if (domain.Contains("baidu") || domain.Contains("google"))
        {
            suggestions.Add("1. 搜索引擎结果页面访问失败，尝试直接搜索：");
            var directSearch = await SearchAsync(searchQuery);
            suggestions.Add(directSearch);
        }
        else
        {
            suggestions.Add($"1. 尝试搜索相关主题：{searchQuery}");
            var relatedSearch = await SearchAsync($"{searchQuery} {keywords}");
            suggestions.Add(relatedSearch);
        }
        
        suggestions.Add("");
        suggestions.Add("2. 建议手动搜索关键词:");
        suggestions.Add($"   - {searchQuery}");
        suggestions.Add($"   - {keywords}");
        suggestions.Add($"   - {domain} {searchQuery}");
        
        return string.Join("\n", suggestions);
    }

    // 私有辅助方法
    private async Task<string> TryAlternativeAccess(string url)
    {
        try
        {
            // 尝试使用Web Archive (Wayback Machine)
            var archiveUrl = $"https://web.archive.org/web/{url}";
            Console.WriteLine($"🔄 尝试访问存档版本: {archiveUrl}");
            
            var html = await _httpClient.GetStringAsync(archiveUrl);
            var text = Regex.Replace(html, @"<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            if (text.Length > 100)
            {
                return $"[存档版本] {text.Substring(0, Math.Min(5000, text.Length))}...";
            }
        }
        catch
        {
            // 存档也失败了
        }
        
        return $"❌ 网页 {url} 访问失败 (403 Forbidden)。\n" +
               $"可能原因：\n" +
               $"1. 网站有反爬虫保护\n" +
               $"2. 需要登录或特殊权限\n" +
               $"3. 地区访问限制\n\n" +
               $"建议：提供其他相关网站或具体搜索关键词";
    }

    private string[] ExtractDuckDuckGoResults(string html)
    {
        return Regex.Matches(html, @"<a[^>]+href=""([^""]+)""[^>]*class=""result__a""[^>]*>([^<]+)</a>")
            .Cast<Match>()
            .Take(5)
            .Select(m => $"标题: {m.Groups[2].Value.Trim()}\nURL: {m.Groups[1].Value}")
            .ToArray();
    }

    private string[] ExtractBingResults(string html)
    {
        return Regex.Matches(html, @"<h2><a[^>]+href=""([^""]+)""[^>]*>([^<]+)</a></h2>")
            .Cast<Match>()
            .Take(5)
            .Select(m => $"标题: {m.Groups[2].Value.Trim()}\nURL: {m.Groups[1].Value}")
            .ToArray();
    }

    private string[] ExtractBaiduResults(string html)
    {
        return Regex.Matches(html, @"<h3[^>]*><a[^>]+href=""([^""]+)""[^>]*>([^<]+)</a></h3>")
            .Cast<Match>()
            .Take(5)
            .Select(m => $"标题: {m.Groups[2].Value.Trim()}\nURL: {m.Groups[1].Value}")
            .ToArray();
    }

    private string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return "";
        }
    }

    private string ExtractKeywordsFromUrl(string url)
    {
        // 简单提取URL中的关键词
        var keywords = Regex.Matches(url, @"[a-zA-Z\u4e00-\u9fa5]{3,}")
            .Cast<Match>()
            .Select(m => m.Value)
            .Take(3)
            .ToArray();
        
        return string.Join(" ", keywords);
    }

    // 保留原有的其他方法...
    [KernelFunction]
    [Description("获取当前日期时间")]
    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss");
    }

    [KernelFunction]
    [Description("下载图片或文件到本地")]
    public async Task<string> DownloadFileAsync(string url, string localPath)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await using var fileStream = File.Create(localPath);
            await response.Content.CopyToAsync(fileStream);
            
            var fileInfo = new FileInfo(localPath);
            return $"文件下载成功: {localPath}\n大小: {fileInfo.Length} 字节";
        }
        catch (Exception ex)
        {
            return $"下载文件失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("获取图片信息")]
    public async Task<string> GetImageInfoAsync(string imageUrl)
    {
        try
        {
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUrl));
            response.EnsureSuccessStatusCode();
            
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "未知";
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            
            return $"图片URL: {imageUrl}\n" +
                   $"内容类型: {contentType}\n" +
                   $"文件大小: {contentLength} 字节\n" +
                   $"状态: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"获取图片信息失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("获取图片下载的CLI命令参考（当WebPlugin方法失败时的备选方案）")]
    public string GetImageDownloadCommands()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return @"Windows图片下载命令：
- PowerShell: Invoke-WebRequest -Uri 'https://example.com/image.jpg' -OutFile 'local_image.jpg'
- curl: curl -o local_image.jpg https://example.com/image.jpg";
        }
        else
        {
            return @"Unix/Linux图片下载命令：
- wget: wget https://example.com/image.jpg -O local_image.jpg
- curl: curl -o local_image.jpg https://example.com/image.jpg";
        }
    }

    [KernelFunction]
    [Description("测试网络连接和搜索引擎可访问性")]
    public async Task<string> TestNetworkConnectionAsync()
    {
        var testUrls = new[]
        {
            "https://httpbin.org/get", // 测试基本HTTP连接
            "https://www.google.com",
            "https://www.bing.com", 
            "https://html.duckduckgo.com",
            "https://www.baidu.com"
        };

        var results = new List<string>();
        results.Add("=== 网络连接测试 ===");

        foreach (var url in testUrls)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url);
                results.Add($"✅ {url}: {response.StatusCode} ({response.ReasonPhrase})");
            }
            catch (Exception ex)
            {
                results.Add($"❌ {url}: {ex.Message}");
            }
        }

        return string.Join("\n", results);
    }

    [KernelFunction]
    [Description("诊断网络连接问题")]
    public async Task<string> DiagnoseNetworkIssuesAsync()
    {
        var results = new List<string>();
        results.Add("=== 网络诊断报告 ===\n");
        
        // 1. 测试基本HTTP连接
        try
        {
            var response = await _httpClient.GetAsync("https://httpbin.org/get");
            results.Add($"✅ 基本HTTP连接: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            results.Add($"❌ 基本HTTP连接失败: {ex.Message}");
            results.Add("可能原因：代理设置、防火墙或DNS问题\n");
            return string.Join("\n", results);
        }
        
        // 2. 测试DNS解析
        var testDomains = new[] { "www.baidu.com", "www.bing.com", "duckduckgo.com" };
        foreach (var domain in testDomains)
        {
            try
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
                results.Add($"✅ DNS解析 {domain}: {addresses.Length} 个地址");
            }
            catch (Exception ex)
            {
                results.Add($"❌ DNS解析 {domain} 失败: {ex.Message}");
            }
        }
        
        // 3. 检查User-Agent和请求头
        results.Add($"\n当前User-Agent: {_userAgents[_currentUserAgentIndex]}");
        
        // 4. 建议使用本地搜索数据
        results.Add("\n=== 建议解决方案 ===");
        results.Add("1. 使用本地旅游数据库（推荐）");
        results.Add("2. 配置HTTP代理");
        results.Add("3. 使用API而非网页爬取");
        results.Add("4. 手动提供数据源");
        
        return string.Join("\n", results);
    }
}
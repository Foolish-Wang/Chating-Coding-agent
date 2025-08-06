using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class WebPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _tavilyApiKey;
    private readonly string _tavilyBaseUrl = "https://api.tavily.com";

    public WebPlugin()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        
        // 从环境变量获取Tavily API Key
        _tavilyApiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY") ?? "";
        
        if (string.IsNullOrEmpty(_tavilyApiKey))
        {
            Console.WriteLine("⚠️ 警告: TAVILY_API_KEY 未设置，部分功能可能无法使用");
        }
    }

    [KernelFunction]
    [Description("使用Tavily搜索引擎进行智能搜索，获取最新的网络信息")]
    public async Task<string> SearchAsync(string query)
    {
        if (string.IsNullOrEmpty(_tavilyApiKey))
        {
            return "❌ Tavily API Key 未配置，请在 .env 文件中设置 TAVILY_API_KEY";
        }

        try
        {
            Console.WriteLine($"🔍 使用Tavily搜索: {query}");

            var searchRequest = new TavilySearchRequest
            {
                ApiKey = _tavilyApiKey,
                Query = query,
                SearchDepth = "advanced",
                IncludeAnswer = true,
                IncludeImages = false,
                IncludeRawContent = false,
                MaxResults = 8,
                IncludeDomains = null,
                ExcludeDomains = null
            };

            var jsonContent = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_tavilyBaseUrl}/search", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<TavilySearchResponse>(responseContent, 
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        PropertyNameCaseInsensitive = true
                    });

                return FormatTavilyResults(searchResponse, query);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Tavily搜索失败: {response.StatusCode} - {errorContent}");
                return GetFallbackSearchResults(query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Tavily搜索异常: {ex.Message}");
            return GetFallbackSearchResults(query);
        }
    }

    [KernelFunction]
    [Description("使用Tavily获取特定网页的详细内容")]
    public async Task<string> GetWebPageTextAsync(string url)
    {
        if (string.IsNullOrEmpty(_tavilyApiKey))
        {
            return "❌ Tavily API Key 未配置，请在 .env 文件中设置 TAVILY_API_KEY";
        }

        try
        {
            Console.WriteLine($"🌐 使用Tavily获取网页内容: {url}");

            var extractRequest = new TavilyExtractRequest
            {
                ApiKey = _tavilyApiKey,
                Urls = new[] { url }
            };

            var jsonContent = JsonSerializer.Serialize(extractRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_tavilyBaseUrl}/extract", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var extractResponse = JsonSerializer.Deserialize<TavilyExtractResponse>(responseContent,
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        PropertyNameCaseInsensitive = true
                    });

                if (extractResponse?.Results?.Any() == true)
                {
                    var result = extractResponse.Results.First();
                    var cleanContent = CleanExtractedContent(result.RawContent ?? "");
                    
                    Console.WriteLine($"✅ 网页内容提取成功，长度: {cleanContent.Length} 字符");
                    
                    return $"🌐 网页标题: {result.Title}\n" +
                           $"📍 URL: {result.Url}\n" +
                           $"📄 内容:\n{cleanContent.Substring(0, Math.Min(cleanContent.Length, 8000))}";
                }
                else
                {
                    return $"⚠️ 无法提取网页内容: {url}";
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ 网页提取失败: {response.StatusCode} - {errorContent}");
                return HandleFailedWebAccess(url);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 网页提取异常: {ex.Message}");
            return HandleFailedWebAccess(url);
        }
    }

    [KernelFunction]
    [Description("使用Tavily进行深度搜索，获取更详细的信息")]
    public async Task<string> DeepSearchAsync(string query)
    {
        if (string.IsNullOrEmpty(_tavilyApiKey))
        {
            return "❌ Tavily API Key 未配置，请在 .env 文件中设置 TAVILY_API_KEY";
        }

        try
        {
            Console.WriteLine($"🔍 Tavily深度搜索: {query}");

            var searchRequest = new TavilySearchRequest
            {
                ApiKey = _tavilyApiKey,
                Query = query,
                SearchDepth = "advanced",
                IncludeAnswer = true,
                IncludeImages = true,
                IncludeRawContent = true,
                MaxResults = 10,
                IncludeDomains = null,
                ExcludeDomains = null
            };

            var jsonContent = JsonSerializer.Serialize(searchRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            });

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_tavilyBaseUrl}/search", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<TavilySearchResponse>(responseContent,
                    new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        PropertyNameCaseInsensitive = true
                    });

                return FormatTavilyDeepResults(searchResponse, query);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Tavily深度搜索失败: {response.StatusCode} - {errorContent}");
                return GetFallbackSearchResults(query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Tavily深度搜索异常: {ex.Message}");
            return GetFallbackSearchResults(query);
        }
    }

    [KernelFunction]
    [Description("当主要搜索方法失败时，提供替代搜索策略")]
    public async Task<string> GetAlternativeSearchSuggestions(string originalQuery)
    {
        var suggestions = new List<string>
        {
            $"原始搜索 '{originalQuery}' 遇到问题，尝试以下替代方案：\n"
        };

        // 生成不同的关键词组合
        var alternativeQueries = GenerateAlternativeQueries(originalQuery);
        
        suggestions.Add("=== 建议的替代搜索词 ===");
        for (int i = 0; i < alternativeQueries.Length && i < 5; i++)
        {
            suggestions.Add($"{i + 1}. {alternativeQueries[i]}");
        }
        
        suggestions.Add("\n=== 手动搜索建议 ===");
        suggestions.Add("1. 检查网络连接状态");
        suggestions.Add("2. 验证Tavily API Key是否有效");
        suggestions.Add("3. 尝试更简单的搜索关键词");
        suggestions.Add("4. 等待一段时间后重试");
        
        // 尝试使用一个简化的搜索
        try
        {
            Console.WriteLine("🔄 尝试简化搜索...");
            var simpleQuery = ExtractKeyWords(originalQuery);
            if (!string.IsNullOrEmpty(simpleQuery) && simpleQuery != originalQuery)
            {
                var fallbackResult = await SearchAsync(simpleQuery);
                if (!string.IsNullOrEmpty(fallbackResult) && !fallbackResult.Contains("❌"))
                {
                    suggestions.Add("\n=== 简化搜索结果 ===");
                    suggestions.Add(fallbackResult);
                }
            }
        }
        catch (Exception ex)
        {
            suggestions.Add($"\n⚠️ 简化搜索也失败了: {ex.Message}");
        }
        
        return string.Join("\n", suggestions);
    }

    [KernelFunction]
    [Description("获取当前日期时间")]
    public string GetCurrentDateTime()
    {
        return DateTime.Now.ToString("yyyy年MM月dd日 HH:mm:ss dddd");
    }

    [KernelFunction]
    [Description("测试Tavily API连接状态")]
    public async Task<string> TestTavilyConnectionAsync()
    {
        if (string.IsNullOrEmpty(_tavilyApiKey))
        {
            return "❌ Tavily API Key 未配置，请在 .env 文件中设置 TAVILY_API_KEY";
        }

        try
        {
            Console.WriteLine("🔍 测试Tavily API连接...");
            
            var testResult = await SearchAsync("hello world");
            
            if (testResult.Contains("❌"))
            {
                return $"❌ Tavily API 连接失败:\n{testResult}";
            }
            else
            {
                return "✅ Tavily API 连接正常，可以正常使用搜索功能";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Tavily API 测试异常: {ex.Message}";
        }
    }

    // 私有辅助方法
    private string FormatTavilyResults(TavilySearchResponse response, string query)
    {
        if (response?.Results == null || !response.Results.Any())
        {
            return $"🔍 未找到关于 '{query}' 的搜索结果";
        }

        var formatted = new List<string>
        {
            $"🎯 Tavily搜索结果 ('{query}'):\n"
        };

        // 添加AI生成的答案（如果有）
        if (!string.IsNullOrEmpty(response.Answer))
        {
            formatted.Add("🤖 AI 生成摘要:");
            formatted.Add(response.Answer);
            formatted.Add("");
        }

        // 添加搜索结果
        formatted.Add("📄 搜索结果:");
        for (int i = 0; i < Math.Min(response.Results.Length, 8); i++)
        {
            var result = response.Results[i];
            formatted.Add($"\n{i + 1}. 📄 {result.Title}");
            formatted.Add($"   🔗 {result.Url}");
            if (!string.IsNullOrEmpty(result.Content))
            {
                var content = result.Content.Length > 200 ? result.Content.Substring(0, 200) + "..." : result.Content;
                formatted.Add($"   📝 {content}");
            }
            if (result.Score.HasValue)
            {
                formatted.Add($"   ⭐ 相关度: {result.Score:F2}");
            }
        }

        Console.WriteLine($"✅ Tavily搜索成功，返回 {response.Results.Length} 个结果");
        
        return string.Join("\n", formatted);
    }

    private string FormatTavilyDeepResults(TavilySearchResponse response, string query)
    {
        if (response?.Results == null || !response.Results.Any())
        {
            return $"🔍 深度搜索未找到关于 '{query}' 的结果";
        }

        var formatted = new List<string>
        {
            $"🎯 Tavily深度搜索结果 ('{query}'):\n"
        };

        // 添加AI生成的答案（如果有）
        if (!string.IsNullOrEmpty(response.Answer))
        {
            formatted.Add("🤖 AI 详细分析:");
            formatted.Add(response.Answer);
            formatted.Add("");
        }

        // 添加图片（如果有）
        if (response.Images?.Any() == true)
        {
            formatted.Add("🖼️ 相关图片:");
            foreach (var image in response.Images.Take(3))
            {
                formatted.Add($"   📸 {image}");
            }
            formatted.Add("");
        }

        // 添加详细搜索结果
        formatted.Add("📄 详细搜索结果:");
        for (int i = 0; i < Math.Min(response.Results.Length, 10); i++)
        {
            var result = response.Results[i];
            formatted.Add($"\n{i + 1}. 📄 {result.Title}");
            formatted.Add($"   🔗 {result.Url}");
            
            if (!string.IsNullOrEmpty(result.Content))
            {
                formatted.Add($"   📝 摘要: {result.Content}");
            }
            
            if (!string.IsNullOrEmpty(result.RawContent))
            {
                var rawContent = CleanExtractedContent(result.RawContent);
                var truncatedContent = rawContent.Length > 300 ? rawContent.Substring(0, 300) + "..." : rawContent;
                formatted.Add($"   📋 详细内容: {truncatedContent}");
            }
            
            if (result.Score.HasValue)
            {
                formatted.Add($"   ⭐ 相关度: {result.Score:F2}");
            }
        }

        Console.WriteLine($"✅ Tavily深度搜索成功，返回 {response.Results.Length} 个详细结果");
        
        return string.Join("\n", formatted);
    }

    private string CleanExtractedContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        
        // 清理多余的空白字符
        content = Regex.Replace(content, @"\s+", " ");
        content = Regex.Replace(content, @"\n\s*\n", "\n");
        
        return content.Trim();
    }

    private string GetFallbackSearchResults(string query)
    {
        var fallbackSuggestions = new List<string>
        {
            $"⚠️ Tavily搜索暂时不可用，建议：",
            "",
            $"1. 检查TAVILY_API_KEY是否正确配置",
            $"2. 验证网络连接状态",
            $"3. 检查Tavily API配额是否充足",
            $"4. 尝试简化搜索关键词: {query}",
            $"5. 稍后重试搜索",
            "",
            "=== 手动搜索建议 ===",
            $"可以在浏览器中手动搜索: {query}",
            $"- Google: https://www.google.com/search?q={Uri.EscapeDataString(query)}",
            $"- Bing: https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
            $"- 百度: https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}"
        };
        
        return string.Join("\n", fallbackSuggestions);
    }

    private string HandleFailedWebAccess(string url)
    {
        return $"❌ 无法访问网页: {url}\n\n" +
               $"可能原因:\n" +
               $"• 网站有反爬虫保护\n" +
               $"• 需要登录验证\n" +
               $"• 地理位置限制\n" +
               $"• Tavily API限制\n\n" +
               $"建议:\n" +
               $"• 在浏览器中手动访问\n" +
               $"• 检查Tavily API配额\n" +
               $"• 尝试其他相关搜索";
    }

    private string[] GenerateAlternativeQueries(string originalQuery)
    {
        var alternatives = new List<string> { originalQuery };
        
        // 添加一些变体
        alternatives.Add($"{originalQuery} 2024");
        alternatives.Add($"{originalQuery} 最新");
        alternatives.Add($"{originalQuery} 详细信息");
        alternatives.Add($"{originalQuery} 介绍");
        alternatives.Add($"{originalQuery} 概述");
        
        // 如果包含中文，添加英文变体
        if (Regex.IsMatch(originalQuery, @"[\u4e00-\u9fa5]"))
        {
            alternatives.Add($"{originalQuery} china");
            alternatives.Add($"{originalQuery} chinese");
        }
        
        return alternatives.ToArray();
    }

    private string ExtractKeyWords(string query)
    {
        // 提取关键词，去除停用词
        var keywords = Regex.Matches(query, @"[\w\u4e00-\u9fa5]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .Where(w => w.Length > 1 && !IsStopWord(w))
            .Take(3);
            
        return string.Join(" ", keywords);
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new[] { 
            "的", "了", "在", "是", "和", "与", "或", "但", "而", "因为", "所以", "如果", "那么", "这", "那", "什么", "怎么", "哪里", "怎样",
            "the", "is", "are", "and", "or", "but", "if", "then", "what", "how", "where", "when", "why", "who", "which"
        };
        return stopWords.Contains(word.ToLower());
    }

    // Tavily API 数据模型
    private class TavilySearchRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("search_depth")]
        public string SearchDepth { get; set; } = "basic";

        [JsonPropertyName("include_answer")]
        public bool IncludeAnswer { get; set; } = true;

        [JsonPropertyName("include_images")]
        public bool IncludeImages { get; set; } = false;

        [JsonPropertyName("include_raw_content")]
        public bool IncludeRawContent { get; set; } = false;

        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 5;

        [JsonPropertyName("include_domains")]
        public string[] IncludeDomains { get; set; }

        [JsonPropertyName("exclude_domains")]
        public string[] ExcludeDomains { get; set; }
    }

    private class TavilySearchResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; }

        [JsonPropertyName("query")]
        public string Query { get; set; }

        [JsonPropertyName("response_time")]
        public double ResponseTime { get; set; }

        [JsonPropertyName("images")]
        public string[] Images { get; set; }

        [JsonPropertyName("results")]
        public TavilyResult[] Results { get; set; }
    }

    private class TavilyResult
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("raw_content")]
        public string RawContent { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }
    }

    private class TavilyExtractRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("urls")]
        public string[] Urls { get; set; }
    }

    private class TavilyExtractResponse
    {
        [JsonPropertyName("results")]
        public TavilyExtractResult[] Results { get; set; }
    }

    private class TavilyExtractResult
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("raw_content")]
        public string RawContent { get; set; }
    }
}
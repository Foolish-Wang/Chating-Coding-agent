using Microsoft.SemanticKernel;
using System.ComponentModel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;

public class WebPlugin
{
    private readonly HttpClient _httpClient;

    public WebPlugin()
    {
        _httpClient = new HttpClient();
    }

    [KernelFunction]
    [Description("获取网页内容")]
    public async Task<string> GetWebPageAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            return response.Length > 2000 ? response.Substring(0, 2000) + "..." : response;
        }
        catch (Exception ex)
        {
            return $"获取网页失败: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("发送HTTP请求")]
    public async Task<string> HttpRequestAsync(string url, string method = "GET")
    {
        try
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            return $"状态码: {response.StatusCode}\n内容: {content}";
        }
        catch (Exception ex)
        {
            return $"HTTP请求失败: {ex.Message}";
        }
    }
}
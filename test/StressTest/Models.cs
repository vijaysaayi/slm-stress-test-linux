using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StressTest;

public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "smollm-135m";

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_new_tokens")]
    public int MaxNewTokens { get; set; } = 250;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public TokenUsage Usage { get; set; } = new();
}

public class ChatChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = string.Empty;
}

public class TokenUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class RequestResult
{
    public DateTime Timestamp { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public TokenUsage? Usage { get; set; }
    public string ResponseContent { get; set; } = string.Empty;
    
    // Additional properties for HTML report
    public bool IsSuccess => Success;
    public int TokensGenerated => Usage?.CompletionTokens ?? 0;
    public object? RequestData { get; set; }
    public object? ResponseData { get; set; }
    public double? ContainerCpuUsage { get; set; }
    public double? ContainerMemoryUsage { get; set; }
    public double? VmCpuUsage { get; set; }
    public double? VmMemoryUsage { get; set; }
}

public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long DiskUsageMB { get; set; }
    public double VmCpuUsagePercent { get; set; }
    public long VmMemoryUsageMB { get; set; }
    public long VmDiskUsageMB { get; set; }
}

public class TestConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ContainerName { get; set; } = "tux-ai-service";
    public int TotalRequests { get; set; } = 10;
    public int ConcurrentRequests { get; set; } = 1;
    public int MaxTokens { get; set; } = 250;
    public bool MonitorContainer { get; set; } = true;
    public bool MonitorVM { get; set; } = true;
    public string OutputDirectory { get; set; } = "./results";
    
    // Legacy properties for backward compatibility with existing reports
    [Obsolete("Use TotalRequests instead")]
    public int RequestsPerMinute { get; set; } = 10;
    [Obsolete("Use batch-based approach instead")]
    public int DurationMinutes { get; set; } = 30;
}

public class BaselineMetrics
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double AvgContainerCpuUsage { get; set; }
    public long AvgContainerMemoryUsage { get; set; }
    public double AvgVmCpuUsage { get; set; }
    public long AvgVmMemoryUsage { get; set; }
    public double MaxContainerCpuUsage { get; set; }
    public long MaxContainerMemoryUsage { get; set; }
    public double MaxVmCpuUsage { get; set; }
    public long MaxVmMemoryUsage { get; set; }
    public List<PerformanceMetrics> Samples { get; set; } = new();
}

public class TestResult
{
    public TestConfiguration TestConfiguration { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<RequestResult> RequestResults { get; set; } = new();
    public List<PerformanceMetrics> Metrics { get; set; } = new();
    public TestStatistics Statistics { get; set; } = new();
    
    // Baseline monitoring data
    public BaselineMetrics? PreTestBaseline { get; set; }
    public BaselineMetrics? PostTestBaseline { get; set; }
    
    // Legacy properties for backward compatibility
    public int RequestsPerMinute { get; set; }
    public int DurationMinutes { get; set; }
    public List<RequestResult> Requests { get; set; } = new();
}

public class TestStatistics
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan MinResponseTime { get; set; }
    public TimeSpan MaxResponseTime { get; set; }
    public double RequestsPerSecond { get; set; }
    public double AverageCpuUsage { get; set; }
    public double MaxCpuUsage { get; set; }
    public long AverageMemoryUsage { get; set; }
    public long MaxMemoryUsage { get; set; }
    public int TotalTokensGenerated { get; set; }
    public double TokensPerSecond { get; set; }
    public double ActualRequestsPerMinute { get; set; }
    
    // Additional VM and Container metrics
    public double MaxVmCpuUsage { get; set; }
    public long MaxVmMemoryUsage { get; set; }
    public double AvgContainerCpuUsage { get; set; }
    public double AvgVmCpuUsage { get; set; }
    public long AvgContainerMemoryUsage { get; set; }
    public long AvgVmMemoryUsage { get; set; }
}

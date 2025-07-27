using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StressTest;

public class LoadTester
{
    private readonly HttpClient _httpClient;
    private readonly TestConfiguration _config;
    private readonly PerformanceMonitor _monitor;
    private readonly Random _random;

    private readonly string[] _testPrompts = {
        "How many moons does Mars have?",
        "Explain the process of photosynthesis in simple terms.",
        "What is the capital of France and what are some famous landmarks there?",
        "Describe the water cycle and its importance to life on Earth.",
        "What are the primary colors and how do they combine to make other colors?",
        "Explain what artificial intelligence is and give some examples of its use.",
        "How does the human digestive system work?",
        "What causes the seasons to change throughout the year?",
        "Describe the differences between renewable and non-renewable energy sources.",
        "What is gravity and how does it affect objects on Earth?"
    };

    public LoadTester(TestConfiguration config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        
        // Auto-detect container name if not provided or if default is used
        if (string.IsNullOrEmpty(_config.ContainerName) || _config.ContainerName == "tux-ai-service")
        {
            var detectedContainer = PerformanceMonitor.DetectAIServiceContainerAsync(_config.BaseUrl).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(detectedContainer))
            {
                _config.ContainerName = detectedContainer;
                Console.WriteLine($"🎯 Using detected container: {_config.ContainerName}");
            }
            else
            {
                Console.WriteLine($"⚠️  Could not auto-detect container. Using default: {_config.ContainerName}");
                Console.WriteLine($"   If container metrics show 0, check container name with 'docker ps'");
            }
        }
        
        _monitor = new PerformanceMonitor(_config.ContainerName, _config.MonitorContainer, _config.MonitorVM);
        _random = new Random();
    }

    public async Task<TestResult> RunTestAsync()
    {
        var result = new TestResult
        {
            TestConfiguration = _config,
            RequestsPerMinute = _config.RequestsPerMinute,
            DurationMinutes = _config.DurationMinutes,
            StartTime = DateTime.UtcNow
        };

        Console.WriteLine($"🚀 Starting stress test with baseline monitoring");
        Console.WriteLine($"   Requests per minute: {_config.RequestsPerMinute}");
        Console.WriteLine($"   Duration: {_config.DurationMinutes} minutes");
        Console.WriteLine($"   Target URL: {_config.BaseUrl}");
        Console.WriteLine($"   Max tokens per request: {_config.MaxTokens}");
        Console.WriteLine();

        // Ensure output directory exists
        Directory.CreateDirectory(_config.OutputDirectory);

        // Phase 1: Collect baseline metrics for 1 minute
        Console.WriteLine("📊 Phase 1: Collecting pre-test baseline metrics (1 minute)...");
        result.PreTestBaseline = await CollectBaselineMetricsAsync("Pre-Test Baseline", TimeSpan.FromMinutes(1));
        Console.WriteLine($"✅ Pre-test baseline collected - Container CPU: {result.PreTestBaseline.AvgContainerCpuUsage:F1}%, VM CPU: {result.PreTestBaseline.AvgVmCpuUsage:F1}%");
        Console.WriteLine();

        // Phase 2: Run actual stress test
        Console.WriteLine("🎯 Phase 2: Running stress test...");
        await RunActualStressTestAsync(result);
        Console.WriteLine("✅ Stress test completed");
        Console.WriteLine();

        // Phase 3: Collect post-test metrics for 1 minute
        Console.WriteLine("📊 Phase 3: Collecting post-test baseline metrics (1 minute)...");
        result.PostTestBaseline = await CollectBaselineMetricsAsync("Post-Test Baseline", TimeSpan.FromMinutes(1));
        Console.WriteLine($"✅ Post-test baseline collected - Container CPU: {result.PostTestBaseline.AvgContainerCpuUsage:F1}%, VM CPU: {result.PostTestBaseline.AvgVmCpuUsage:F1}%");
        Console.WriteLine();

        result.EndTime = DateTime.UtcNow;
        result.Statistics = CalculateStatistics(result);

        // Save results
        await SaveResultsAsync(result);
        
        // Generate HTML report
        HtmlReportGenerator.GenerateReport(result, _config.OutputDirectory);
        
        // Display results with baseline comparison
        DisplayResultsWithBaseline(result);

        return result;
    }

    private async Task<BaselineMetrics> CollectBaselineMetricsAsync(string phase, TimeSpan duration)
    {
        var baseline = new BaselineMetrics
        {
            StartTime = DateTime.UtcNow
        };

        var cancellationTokenSource = new CancellationTokenSource(duration);
        var samples = new List<PerformanceMetrics>();

        Console.WriteLine($"   🔍 Collecting {phase} metrics for {duration.TotalMinutes:F0} minute(s)...");
        
        // Start progress indicator
        var progressTask = Task.Run(async () =>
        {
            var startTime = DateTime.UtcNow;
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - startTime;
                var progress = (elapsed.TotalSeconds / duration.TotalSeconds) * 100;
                Console.Write($"\r   Progress: {progress:F0}% ({elapsed.TotalSeconds:F0}s/{duration.TotalSeconds:F0}s)");
                
                await Task.Delay(1000, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            Console.WriteLine(); // New line after progress
        }, cancellationTokenSource.Token);

        // Collect metrics every 5 seconds
        try
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var metric = await _monitor.GetMetricsAsync();
                samples.Add(metric);
                
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException) { }

        try
        {
            await progressTask;
        }
        catch (OperationCanceledException) { }

        baseline.EndTime = DateTime.UtcNow;
        baseline.Samples = samples;

        if (samples.Any())
        {
            baseline.AvgContainerCpuUsage = samples.Average(s => s.CpuUsagePercent);
            baseline.AvgContainerMemoryUsage = (long)samples.Average(s => s.MemoryUsageMB);
            baseline.AvgVmCpuUsage = samples.Average(s => s.VmCpuUsagePercent);
            baseline.AvgVmMemoryUsage = (long)samples.Average(s => s.VmMemoryUsageMB);
            
            baseline.MaxContainerCpuUsage = samples.Max(s => s.CpuUsagePercent);
            baseline.MaxContainerMemoryUsage = samples.Max(s => s.MemoryUsageMB);
            baseline.MaxVmCpuUsage = samples.Max(s => s.VmCpuUsagePercent);
            baseline.MaxVmMemoryUsage = samples.Max(s => s.VmMemoryUsageMB);
        }

        return baseline;
    }

    private async Task RunActualStressTestAsync(TestResult result)
    {
        var testDuration = TimeSpan.FromMinutes(_config.DurationMinutes);
        var totalRequests = _config.RequestsPerMinute * _config.DurationMinutes;
        var requestInterval = testDuration.TotalMilliseconds / totalRequests;

        Console.WriteLine($"   Total requests planned: {totalRequests}");
        Console.WriteLine($"   Request interval: {requestInterval:F0}ms");
        Console.WriteLine();

        var cancellationTokenSource = new CancellationTokenSource(testDuration);
        var requestTasks = new List<Task>();
        var requestResults = new List<RequestResult>();
        var performanceMetrics = new List<PerformanceMetrics>();

        // Start performance monitoring
        var monitoringTask = MonitorPerformanceAsync(performanceMetrics, cancellationTokenSource.Token);

        // Schedule requests
        for (int i = 0; i < totalRequests; i++)
        {
            var requestIndex = i;
            var delay = TimeSpan.FromMilliseconds(requestInterval * i);
            
            var requestTask = Task.Run(async () =>
            {
                await Task.Delay(delay, cancellationTokenSource.Token);
                
                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var requestResult = await MakeRequestAsync(requestIndex + 1);
                    lock (requestResults)
                    {
                        requestResults.Add(requestResult);
                    }
                }
            }, cancellationTokenSource.Token);

            requestTasks.Add(requestTask);
        }

        // Wait for all requests to complete or timeout
        try
        {
            await Task.WhenAll(requestTasks);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⚠️  Some requests were cancelled due to timeout");
        }

        // Stop monitoring
        cancellationTokenSource.Cancel();
        try
        {
            await monitoringTask;
        }
        catch (OperationCanceledException) { }

        result.RequestResults = requestResults.OrderBy(r => r.Timestamp).ToList();
        result.Requests = result.RequestResults; // Backward compatibility
        result.Metrics = performanceMetrics.OrderBy(m => m.Timestamp).ToList();
    }

    private async Task<RequestResult> MakeRequestAsync(int requestIndex)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new RequestResult
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var request = new ChatCompletionRequest
            {
                Model = "smollm-135m",
                MaxNewTokens = _config.MaxTokens,
                Temperature = 0.7,
                Messages = new List<ChatMessage>
                {
                    new() { Role = "system", Content = "You are a helpful assistant that provides clear and concise answers." },
                    new() { Role = "user", Content = GetRandomPrompt() }
                }
            };

            // Store request data for HTML report
            result.RequestData = request;

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Console.WriteLine($"   📤 Request {requestIndex}: {request.Messages.Last().Content[..Math.Min(50, request.Messages.Last().Content.Length)]}...");

            // Get current resource usage before request
            var currentMetrics = await GetCurrentPerformanceAsync();
            result.ContainerCpuUsage = currentMetrics?.CpuUsagePercent;
            result.ContainerMemoryUsage = currentMetrics?.MemoryUsageMB;
            result.VmCpuUsage = currentMetrics?.VmCpuUsagePercent;
            result.VmMemoryUsage = currentMetrics?.VmMemoryUsageMB;

            var response = await _httpClient.PostAsync($"{_config.BaseUrl}/v1/chat/completions", content);
            
            result.StatusCode = (int)response.StatusCode;
            result.ResponseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObj = JsonSerializer.Deserialize<ChatCompletionResponse>(result.ResponseContent);
                result.Usage = responseObj?.Usage;
                result.Success = true;
                result.ResponseData = responseObj; // Store full response for HTML report
                
                Console.WriteLine($"   ✅ Request {requestIndex} completed ({response.StatusCode}) - {result.Usage?.TotalTokens} tokens");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"HTTP {response.StatusCode}: {result.ResponseContent}";
                Console.WriteLine($"   ❌ Request {requestIndex} failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Console.WriteLine($"   ❌ Request {requestIndex} exception: {ex.Message}");
        }

        stopwatch.Stop();
        result.ResponseTime = stopwatch.Elapsed;

        return result;
    }

    private async Task MonitorPerformanceAsync(List<PerformanceMetrics> metrics, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var metric = await _monitor.GetMetricsAsync();
                lock (metrics)
                {
                    metrics.Add(metric);
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<PerformanceMetrics?> GetCurrentPerformanceAsync()
    {
        try
        {
            return await _monitor.GetMetricsAsync();
        }
        catch
        {
            return null;
        }
    }

    private string GetRandomPrompt()
    {
        return _testPrompts[_random.Next(_testPrompts.Length)];
    }

    private TestStatistics CalculateStatistics(TestResult testResult)
    {
        var stats = new TestStatistics
        {
            TotalRequests = testResult.Requests.Count,
            SuccessfulRequests = testResult.Requests.Count(r => r.Success),
            FailedRequests = testResult.Requests.Count(r => !r.Success)
        };

        if (stats.TotalRequests > 0)
        {
            var successfulRequests = testResult.Requests.Where(r => r.Success).ToList();
            
            if (successfulRequests.Any())
            {
                stats.AverageResponseTime = TimeSpan.FromTicks((long)successfulRequests.Average(r => r.ResponseTime.Ticks));
                stats.MinResponseTime = successfulRequests.Min(r => r.ResponseTime);
                stats.MaxResponseTime = successfulRequests.Max(r => r.ResponseTime);
                
                var duration = testResult.EndTime - testResult.StartTime;
                stats.RequestsPerSecond = stats.SuccessfulRequests / duration.TotalSeconds;
                stats.ActualRequestsPerMinute = stats.SuccessfulRequests / duration.TotalMinutes;

                stats.TotalTokensGenerated = successfulRequests.Sum(r => r.Usage?.CompletionTokens ?? 0);
                stats.TokensPerSecond = stats.TotalTokensGenerated / duration.TotalSeconds;
            }
        }

        if (testResult.Metrics.Any())
        {
            stats.AverageCpuUsage = testResult.Metrics.Average(m => m.CpuUsagePercent);
            stats.MaxCpuUsage = testResult.Metrics.Max(m => m.CpuUsagePercent);
            stats.AverageMemoryUsage = (long)testResult.Metrics.Average(m => m.MemoryUsageMB);
            stats.MaxMemoryUsage = testResult.Metrics.Max(m => m.MemoryUsageMB);
            
            // VM metrics from continuous monitoring
            stats.AvgVmCpuUsage = testResult.Metrics.Average(m => m.VmCpuUsagePercent);
            stats.MaxVmCpuUsage = testResult.Metrics.Max(m => m.VmCpuUsagePercent);
            stats.AvgVmMemoryUsage = (long)testResult.Metrics.Average(m => m.VmMemoryUsageMB);
            stats.MaxVmMemoryUsage = testResult.Metrics.Max(m => m.VmMemoryUsageMB);
        }

        // Container and VM metrics from per-request data
        var requestsWithContainerData = testResult.RequestResults.Where(r => r.ContainerCpuUsage.HasValue).ToList();
        var requestsWithVmData = testResult.RequestResults.Where(r => r.VmCpuUsage.HasValue).ToList();
        
        if (requestsWithContainerData.Any())
        {
            stats.AvgContainerCpuUsage = requestsWithContainerData.Average(r => r.ContainerCpuUsage!.Value);
            stats.AvgContainerMemoryUsage = (long)requestsWithContainerData.Where(r => r.ContainerMemoryUsage.HasValue).Average(r => r.ContainerMemoryUsage!.Value);
            
            // Update max values if per-request data shows higher values
            var maxContainerCpu = requestsWithContainerData.Max(r => r.ContainerCpuUsage!.Value);
            var maxContainerMemory = (long)requestsWithContainerData.Where(r => r.ContainerMemoryUsage.HasValue).Max(r => r.ContainerMemoryUsage!.Value);
            
            if (maxContainerCpu > stats.MaxCpuUsage) stats.MaxCpuUsage = maxContainerCpu;
            if (maxContainerMemory > stats.MaxMemoryUsage) stats.MaxMemoryUsage = maxContainerMemory;
        }
        
        if (requestsWithVmData.Any())
        {
            var avgVmCpuFromRequests = requestsWithVmData.Average(r => r.VmCpuUsage!.Value);
            var avgVmMemoryFromRequests = (long)requestsWithVmData.Where(r => r.VmMemoryUsage.HasValue).Average(r => r.VmMemoryUsage!.Value);
            
            // Use the higher of continuous monitoring or per-request averages
            if (avgVmCpuFromRequests > stats.AvgVmCpuUsage) stats.AvgVmCpuUsage = avgVmCpuFromRequests;
            if (avgVmMemoryFromRequests > stats.AvgVmMemoryUsage) stats.AvgVmMemoryUsage = avgVmMemoryFromRequests;
            
            // Update max values if per-request data shows higher values
            var maxVmCpu = requestsWithVmData.Max(r => r.VmCpuUsage!.Value);
            var maxVmMemory = (long)requestsWithVmData.Where(r => r.VmMemoryUsage.HasValue).Max(r => r.VmMemoryUsage!.Value);
            
            if (maxVmCpu > stats.MaxVmCpuUsage) stats.MaxVmCpuUsage = maxVmCpu;
            if (maxVmMemory > stats.MaxVmMemoryUsage) stats.MaxVmMemoryUsage = maxVmMemory;
        }

        return stats;
    }

    private async Task SaveResultsAsync(TestResult result)
    {
        var fileName = Path.Combine(_config.OutputDirectory, $"stress_test_{result.RequestsPerMinute}rpm_{result.DurationMinutes}min_{result.StartTime:yyyyMMdd_HHmmss}.json");
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(fileName, json);
        
        // Also save as CSV for easy analysis
        await SaveCsvSummaryAsync(result);
        
        Console.WriteLine($"💾 Results saved to: {fileName}");
    }

    private async Task SaveCsvSummaryAsync(TestResult result)
    {
        var csvContent = new StringBuilder();
        csvContent.AppendLine("RequestsPerMinute,DurationMinutes,StartTime,EndTime,TotalRequests,SuccessfulRequests,FailedRequests,SuccessRate,AvgResponseTimeMs,MinResponseTimeMs,MaxResponseTimeMs,RequestsPerSecond,ActualRequestsPerMinute,TokensPerSecond,AvgCpuUsage,MaxCpuUsage,AvgMemoryUsageMB,MaxMemoryUsageMB,TotalTokensGenerated");

        csvContent.AppendLine($"{result.RequestsPerMinute}," +
            $"{result.DurationMinutes}," +
            $"{result.StartTime:yyyy-MM-dd HH:mm:ss}," +
            $"{result.EndTime:yyyy-MM-dd HH:mm:ss}," +
            $"{result.Statistics.TotalRequests}," +
            $"{result.Statistics.SuccessfulRequests}," +
            $"{result.Statistics.FailedRequests}," +
            $"{result.Statistics.SuccessRate:F2}," +
            $"{result.Statistics.AverageResponseTime.TotalMilliseconds:F0}," +
            $"{result.Statistics.MinResponseTime.TotalMilliseconds:F0}," +
            $"{result.Statistics.MaxResponseTime.TotalMilliseconds:F0}," +
            $"{result.Statistics.RequestsPerSecond:F2}," +
            $"{result.Statistics.ActualRequestsPerMinute:F2}," +
            $"{result.Statistics.TokensPerSecond:F2}," +
            $"{result.Statistics.AverageCpuUsage:F2}," +
            $"{result.Statistics.MaxCpuUsage:F2}," +
            $"{result.Statistics.AverageMemoryUsage}," +
            $"{result.Statistics.MaxMemoryUsage}," +
            $"{result.Statistics.TotalTokensGenerated}");

        var fileName = Path.Combine(_config.OutputDirectory, $"stress_test_{result.RequestsPerMinute}rpm_{result.DurationMinutes}min_{result.StartTime:yyyyMMdd_HHmmss}.csv");
        await File.WriteAllTextAsync(fileName, csvContent.ToString());
        Console.WriteLine($"� CSV summary saved to: {fileName}");
    }

    private void DisplayResults(TestResult result)
    {
        Console.WriteLine();
        Console.WriteLine("📊 TEST RESULTS SUMMARY");
        Console.WriteLine("=======================");
        Console.WriteLine();

        Console.WriteLine($"Test Configuration:");
        Console.WriteLine($"  Target: {result.RequestsPerMinute} requests/minute for {result.DurationMinutes} minutes");
        Console.WriteLine($"  Duration: {(result.EndTime - result.StartTime).TotalMinutes:F1} minutes");
        Console.WriteLine();

        Console.WriteLine($"Request Statistics:");
        Console.WriteLine($"  Total Requests: {result.Statistics.TotalRequests:N0}");
        Console.WriteLine($"  Successful: {result.Statistics.SuccessfulRequests:N0} ({result.Statistics.SuccessRate:F1}%)");
        Console.WriteLine($"  Failed: {result.Statistics.FailedRequests:N0}");
        Console.WriteLine($"  Actual Rate: {result.Statistics.ActualRequestsPerMinute:F1} requests/minute");
        Console.WriteLine();

        Console.WriteLine($"Performance Metrics:");
        Console.WriteLine($"  Average Response Time: {result.Statistics.AverageResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Min Response Time: {result.Statistics.MinResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Max Response Time: {result.Statistics.MaxResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Throughput: {result.Statistics.RequestsPerSecond:F2} requests/second");
        Console.WriteLine($"  Token Generation: {result.Statistics.TokensPerSecond:F1} tokens/second");
        Console.WriteLine();

        Console.WriteLine($"Resource Usage:");
        Console.WriteLine($"  Average CPU: {result.Statistics.AverageCpuUsage:F1}%");
        Console.WriteLine($"  Peak CPU: {result.Statistics.MaxCpuUsage:F1}%");
        Console.WriteLine($"  Average Memory: {result.Statistics.AverageMemoryUsage:N0} MB");
        Console.WriteLine($"  Peak Memory: {result.Statistics.MaxMemoryUsage:N0} MB");
        Console.WriteLine();

        Console.WriteLine($"Token Statistics:");
        Console.WriteLine($"  Total Tokens Generated: {result.Statistics.TotalTokensGenerated:N0}");
        Console.WriteLine();

        // Performance insights
        if (result.Statistics.SuccessRate < 95)
        {
            Console.WriteLine($"⚠️  Warning: Success rate is below 95% ({result.Statistics.SuccessRate:F1}%)");
        }

        if (result.Statistics.MaxCpuUsage > 85)
        {
            Console.WriteLine($"⚠️  Warning: Peak CPU usage is high ({result.Statistics.MaxCpuUsage:F1}%)");
        }

        if (result.Statistics.ActualRequestsPerMinute < result.RequestsPerMinute * 0.9)
        {
            Console.WriteLine($"⚠️  Warning: Actual request rate is significantly lower than target");
        }

        Console.WriteLine("🎯 Test completed successfully!");
    }

    private void DisplayResultsWithBaseline(TestResult result)
    {
        Console.WriteLine();
        Console.WriteLine("📊 TEST RESULTS SUMMARY WITH BASELINE COMPARISON");
        Console.WriteLine("================================================");
        Console.WriteLine();

        Console.WriteLine($"Test Configuration:");
        Console.WriteLine($"  Target: {result.RequestsPerMinute} requests/minute for {result.DurationMinutes} minutes");
        Console.WriteLine($"  Total duration (including baselines): {(result.EndTime - result.StartTime).TotalMinutes:F1} minutes");
        Console.WriteLine();

        Console.WriteLine($"Request Statistics:");
        Console.WriteLine($"  Total Requests: {result.Statistics.TotalRequests:N0}");
        Console.WriteLine($"  Successful: {result.Statistics.SuccessfulRequests:N0} ({result.Statistics.SuccessRate:F1}%)");
        Console.WriteLine($"  Failed: {result.Statistics.FailedRequests:N0}");
        Console.WriteLine($"  Actual Rate: {result.Statistics.ActualRequestsPerMinute:F1} requests/minute");
        Console.WriteLine();

        Console.WriteLine($"Performance Metrics:");
        Console.WriteLine($"  Average Response Time: {result.Statistics.AverageResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Min Response Time: {result.Statistics.MinResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Max Response Time: {result.Statistics.MaxResponseTime.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Throughput: {result.Statistics.RequestsPerSecond:F2} requests/second");
        Console.WriteLine($"  Token Generation: {result.Statistics.TokensPerSecond:F1} tokens/second");
        Console.WriteLine();

        // Baseline comparison
        Console.WriteLine("🔍 BASELINE RESOURCE COMPARISON:");
        Console.WriteLine("================================");
        
        if (result.PreTestBaseline != null && result.PostTestBaseline != null)
        {
            Console.WriteLine($"📌 Pre-Test Baseline (1 min idle monitoring):");
            Console.WriteLine($"  Container CPU: {result.PreTestBaseline.AvgContainerCpuUsage:F1}% avg, {result.PreTestBaseline.MaxContainerCpuUsage:F1}% max");
            Console.WriteLine($"  Container Memory: {result.PreTestBaseline.AvgContainerMemoryUsage:N0} MB avg, {result.PreTestBaseline.MaxContainerMemoryUsage:N0} MB max");
            Console.WriteLine($"  VM CPU: {result.PreTestBaseline.AvgVmCpuUsage:F1}% avg, {result.PreTestBaseline.MaxVmCpuUsage:F1}% max");
            Console.WriteLine($"  VM Memory: {result.PreTestBaseline.AvgVmMemoryUsage:N0} MB avg, {result.PreTestBaseline.MaxVmMemoryUsage:N0} MB max");
            Console.WriteLine();

            Console.WriteLine($"🎯 During Stress Test:");
            Console.WriteLine($"  Container CPU: {result.Statistics.AverageCpuUsage:F1}% avg, {result.Statistics.MaxCpuUsage:F1}% max");
            Console.WriteLine($"  Container Memory: {result.Statistics.AverageMemoryUsage:N0} MB avg, {result.Statistics.MaxMemoryUsage:N0} MB max");
            Console.WriteLine($"  VM CPU: {result.Statistics.AvgVmCpuUsage:F1}% avg, {result.Statistics.MaxVmCpuUsage:F1}% max");
            Console.WriteLine($"  VM Memory: {result.Statistics.AvgVmMemoryUsage:N0} MB avg, {result.Statistics.MaxVmMemoryUsage:N0} MB max");
            Console.WriteLine();

            Console.WriteLine($"📌 Post-Test Baseline (1 min cool-down monitoring):");
            Console.WriteLine($"  Container CPU: {result.PostTestBaseline.AvgContainerCpuUsage:F1}% avg, {result.PostTestBaseline.MaxContainerCpuUsage:F1}% max");
            Console.WriteLine($"  Container Memory: {result.PostTestBaseline.AvgContainerMemoryUsage:N0} MB avg, {result.PostTestBaseline.MaxContainerMemoryUsage:N0} MB max");
            Console.WriteLine($"  VM CPU: {result.PostTestBaseline.AvgVmCpuUsage:F1}% avg, {result.PostTestBaseline.MaxVmCpuUsage:F1}% max");
            Console.WriteLine($"  VM Memory: {result.PostTestBaseline.AvgVmMemoryUsage:N0} MB avg, {result.PostTestBaseline.MaxVmMemoryUsage:N0} MB max");
            Console.WriteLine();

            // Calculate deltas
            var containerCpuDelta = result.Statistics.AverageCpuUsage - result.PreTestBaseline.AvgContainerCpuUsage;
            var containerMemoryDelta = result.Statistics.AverageMemoryUsage - result.PreTestBaseline.AvgContainerMemoryUsage;
            var vmCpuDelta = result.Statistics.AvgVmCpuUsage - result.PreTestBaseline.AvgVmCpuUsage;
            var vmMemoryDelta = result.Statistics.AvgVmMemoryUsage - result.PreTestBaseline.AvgVmMemoryUsage;

            Console.WriteLine($"📈 RESOURCE IMPACT ANALYSIS:");
            Console.WriteLine($"============================");
            Console.WriteLine($"Container CPU increase: {containerCpuDelta:+F1;-F1;0}% ({GetResourceImpactDescription(containerCpuDelta, "CPU")})");
            Console.WriteLine($"Container Memory increase: {containerMemoryDelta:+N0;-N0;0} MB ({GetResourceImpactDescription(containerMemoryDelta, "Memory")})");
            Console.WriteLine($"VM CPU increase: {vmCpuDelta:+F1;-F1;0}% ({GetResourceImpactDescription(vmCpuDelta, "CPU")})");
            Console.WriteLine($"VM Memory increase: {vmMemoryDelta:+N0;-N0;0} MB ({GetResourceImpactDescription(vmMemoryDelta, "Memory")})");
            Console.WriteLine();

            // Recovery analysis
            var cpuRecovered = Math.Abs(result.PostTestBaseline.AvgContainerCpuUsage - result.PreTestBaseline.AvgContainerCpuUsage) < 2.0;
            var memoryRecovered = Math.Abs(result.PostTestBaseline.AvgContainerMemoryUsage - result.PreTestBaseline.AvgContainerMemoryUsage) < 50;
            
            Console.WriteLine($"🔄 RECOVERY ANALYSIS:");
            Console.WriteLine($"====================");
            Console.WriteLine($"Container CPU recovery: {(cpuRecovered ? "✅ Good" : "⚠️ Slow")} (post-test: {result.PostTestBaseline.AvgContainerCpuUsage:F1}% vs pre-test: {result.PreTestBaseline.AvgContainerCpuUsage:F1}%)");
            Console.WriteLine($"Container Memory recovery: {(memoryRecovered ? "✅ Good" : "⚠️ Slow")} (post-test: {result.PostTestBaseline.AvgContainerMemoryUsage:N0} MB vs pre-test: {result.PreTestBaseline.AvgContainerMemoryUsage:N0} MB)");
        }
        else
        {
            Console.WriteLine("❌ Baseline data not available");
        }

        Console.WriteLine();

        // Performance insights
        if (result.Statistics.SuccessRate < 95)
        {
            Console.WriteLine($"⚠️  Warning: Success rate is below 95% ({result.Statistics.SuccessRate:F1}%)");
        }

        if (result.Statistics.MaxVmCpuUsage > 85)
        {
            Console.WriteLine($"⚠️  Warning: Peak VM CPU usage is high ({result.Statistics.MaxVmCpuUsage:F1}%)");
        }

        if (result.Statistics.ActualRequestsPerMinute < result.RequestsPerMinute * 0.9)
        {
            Console.WriteLine($"⚠️  Warning: Actual request rate is significantly lower than target");
        }

        Console.WriteLine("🎯 Stress test with baseline monitoring completed successfully!");
    }

    private string GetResourceImpactDescription(double delta, string resourceType)
    {
        return resourceType switch
        {
            "CPU" => delta switch
            {
                > 50 => "High impact",
                > 20 => "Moderate impact", 
                > 5 => "Low impact",
                _ => "Minimal impact"
            },
            "Memory" => delta switch
            {
                > 500 => "High impact",
                > 200 => "Moderate impact",
                > 50 => "Low impact", 
                _ => "Minimal impact"
            },
            _ => "Unknown"
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

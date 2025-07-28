using System.Text.Json;
using StressTest;

Console.WriteLine("🧪 TuxAI Service Stress Testing Tool");
Console.WriteLine("=====================================");
Console.WriteLine();

var config = ParseCommandLineArgs(args);

// Display configuration
DisplayConfiguration(config);

// Verify service is accessible
if (!await VerifyServiceAsync(config.BaseUrl))
{
    Console.WriteLine("❌ Service is not accessible. Please ensure the container is running.");
    Environment.Exit(1);
}

Console.WriteLine("✅ Service is accessible. Starting stress test...");
Console.WriteLine();

var loadTester = new LoadTester(config);

try
{
    var result = await loadTester.RunTestAsync();
    Console.WriteLine();
    Console.WriteLine("🎯 Stress test completed successfully!");
    Console.WriteLine($"📁 Results saved in: {Path.GetFullPath(config.OutputDirectory)}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Test failed with error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex}");
    Environment.Exit(1);
}
finally
{
    loadTester.Dispose();
}

static TestConfiguration ParseCommandLineArgs(string[] args)
    {
        var config = new TestConfiguration();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--url":
                case "-u":
                    if (i + 1 < args.Length) config.BaseUrl = args[++i];
                    break;
                case "--container":
                case "-c":
                    if (i + 1 < args.Length) config.ContainerName = args[++i];
                    break;
                case "--requests":
                case "-r":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var totalRequests))
                        config.TotalRequests = totalRequests;
                    break;
                case "--concurrent":
                case "-n":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var concurrent))
                        config.ConcurrentRequests = concurrent;
                    break;
                case "--duration":
                case "-d":
                    // Keep for backward compatibility but convert to total requests
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var minutes))
                    {
                        config.DurationMinutes = minutes;
                        // Convert legacy format: if using old format, calculate total requests
                        if (config.TotalRequests == 10) // default value, likely not user-set
                            config.TotalRequests = config.RequestsPerMinute * minutes;
                    }
                    break;
                case "--output":
                case "-o":
                    if (i + 1 < args.Length) config.OutputDirectory = args[++i];
                    break;
                case "--tokens":
                case "-t":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var tokens))
                        config.MaxTokens = tokens;
                    break;
                case "--no-container":
                    config.MonitorContainer = false;
                    break;
                case "--no-vm":
                    config.MonitorVM = false;
                    break;
                case "--help":
                case "-h":
                    DisplayHelp();
                    Environment.Exit(0);
                    break;
            }
        }

    return config;
}

static void DisplayHelp()
    {
        Console.WriteLine("TuxAI Service Stress Testing Tool");
        Console.WriteLine("Usage: StressTest [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -u, --url <url>           Service URL (default: http://localhost:11434)");
        Console.WriteLine("  -c, --container <name>    Container name for monitoring (default: tux-ai-service)");
        Console.WriteLine("  -r, --requests <count>    Total number of requests to send (default: 10)");
        Console.WriteLine("  -n, --concurrent <count>  Number of concurrent requests per batch (default: 1)");
        Console.WriteLine("  -d, --duration <minutes>  Test duration in minutes (default: 30)");
        Console.WriteLine("  -o, --output <directory>  Output directory for results (default: ./results)");
        Console.WriteLine("  -t, --tokens <count>      Max tokens per request (default: 250)");
        Console.WriteLine("  --no-container            Disable container monitoring");
        Console.WriteLine("  --no-vm                   Disable VM monitoring");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  StressTest                          # 10 sequential requests");
        Console.WriteLine("  StressTest -r 100 -n 5             # 100 requests in batches of 5 concurrent");
        Console.WriteLine("  StressTest -r 50 -n 1              # 50 sequential requests");
        Console.WriteLine("  StressTest -r 20 -n 2 -t 500       # 20 requests, 2 at a time, 500 tokens each");
    Console.WriteLine("  StressTest -u http://192.168.1.100:11434 -r 20 -d 10");
}

static void DisplayConfiguration(TestConfiguration config)
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Service URL: {config.BaseUrl}");
        Console.WriteLine($"  Container: {config.ContainerName}");
        Console.WriteLine($"  Total requests: {config.TotalRequests}");
        Console.WriteLine($"  Concurrent requests per batch: {config.ConcurrentRequests}");
        Console.WriteLine($"  Total batches: {Math.Ceiling((double)config.TotalRequests / config.ConcurrentRequests)}");
        Console.WriteLine($"  Max tokens per request: {config.MaxTokens}");
        Console.WriteLine($"  Output directory: {config.OutputDirectory}");
        Console.WriteLine($"  Monitor container: {config.MonitorContainer}");
        Console.WriteLine($"  Monitor VM: {config.MonitorVM}");
    Console.WriteLine();
}

static async Task<bool> VerifyServiceAsync(string baseUrl)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await client.GetAsync($"{baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
        return false;
    }
}

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
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var requests))
                        config.RequestsPerMinute = requests;
                    break;
                case "--duration":
                case "-d":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var minutes))
                        config.DurationMinutes = minutes;
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
        Console.WriteLine("  -r, --requests <count>    Requests per minute (default: 10)");
        Console.WriteLine("  -d, --duration <minutes>  Test duration in minutes (default: 30)");
        Console.WriteLine("  -o, --output <directory>  Output directory for results (default: ./results)");
        Console.WriteLine("  -t, --tokens <count>      Max tokens per request (default: 250)");
        Console.WriteLine("  --no-container            Disable container monitoring");
        Console.WriteLine("  --no-vm                   Disable VM monitoring");
        Console.WriteLine("  -h, --help                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  StressTest                          # 10 requests/min for 30 minutes");
        Console.WriteLine("  StressTest -r 30 -d 15              # 30 requests/min for 15 minutes");
        Console.WriteLine("  StressTest -r 5 -d 60 -t 500        # 5 requests/min for 1 hour, 500 tokens each");
    Console.WriteLine("  StressTest -u http://192.168.1.100:11434 -r 20 -d 10");
}

static void DisplayConfiguration(TestConfiguration config)
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Service URL: {config.BaseUrl}");
        Console.WriteLine($"  Container: {config.ContainerName}");
        Console.WriteLine($"  Requests per minute: {config.RequestsPerMinute}");
        Console.WriteLine($"  Test duration: {config.DurationMinutes} minutes");
        Console.WriteLine($"  Total requests planned: {config.RequestsPerMinute * config.DurationMinutes}");
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

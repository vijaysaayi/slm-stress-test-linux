using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace StressTest;

public class PerformanceMonitor
{
    private readonly string _containerName;
    private readonly bool _monitorContainer;
    private readonly bool _monitorVM;
    private static bool _hasLoggedContainerSuccess = false; // Only log container success once

    public static async Task<string?> DetectAIServiceContainerAsync(string baseUrl = "http://localhost:11434")
    {
        try
        {
            // Get all running containers
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "ps --format \"{{.Names}}\\t{{.Ports}}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Look for containers with port 11434 (or the port from baseUrl)
                var uri = new Uri(baseUrl);
                var targetPort = uri.Port.ToString();
                
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        var containerName = parts[0].Trim();
                        var ports = parts[1].Trim();
                        
                        if (ports.Contains($":{targetPort}->") || ports.Contains($"{targetPort}/tcp"))
                        {
                            Console.WriteLine($"🔍 Auto-detected AI service container: {containerName} (ports: {ports})");
                            return containerName;
                        }
                    }
                }
                
                // Fallback: look for common AI service container names
                foreach (var line in lines)
                {
                    var containerName = line.Split('\t')[0].Trim();
                    if (containerName.Contains("smol") || containerName.Contains("tux") || 
                        containerName.Contains("ai") || containerName.Contains("llm"))
                    {
                        Console.WriteLine($"🔍 Found potential AI service container: {containerName}");
                        return containerName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not auto-detect container: {ex.Message}");
        }
        
        return null;
    }

    public PerformanceMonitor(string containerName, bool monitorContainer = true, bool monitorVM = true)
    {
        _containerName = containerName;
        _monitorContainer = monitorContainer;
        _monitorVM = monitorVM;
        
        // Verify container exists if monitoring is enabled
        if (_monitorContainer && !string.IsNullOrEmpty(_containerName))
        {
            Task.Run(async () => await VerifyContainerExistsAsync());
        }
    }

    private async Task VerifyContainerExistsAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"ps --filter name={_containerName} --format \"{{{{.Names}}}}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrEmpty(output.Trim()))
            {
                Console.WriteLine($"⚠️  Warning: Container '{_containerName}' not found or not running.");
                Console.WriteLine($"   Use 'docker ps' to see running containers.");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"   Docker error: {error.Trim()}");
                }
            }
            else
            {
                Console.WriteLine($"✅ Found container: {output.Trim()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not verify container '{_containerName}': {ex.Message}");
        }
    }

    public async Task<PerformanceMetrics> GetMetricsAsync()
    {
        var metrics = new PerformanceMetrics
        {
            Timestamp = DateTime.UtcNow
        };

        var tasks = new List<Task>();

        if (_monitorContainer)
        {
            tasks.Add(Task.Run(async () =>
            {
                var containerMetrics = await GetContainerMetricsAsync();
                metrics.CpuUsagePercent = containerMetrics.CpuUsage;
                metrics.MemoryUsageMB = containerMetrics.MemoryUsage;
                metrics.DiskUsageMB = containerMetrics.DiskUsage;
            }));
        }

        if (_monitorVM)
        {
            tasks.Add(Task.Run(async () =>
            {
                var vmMetrics = await GetVMMetricsAsync();
                metrics.VmCpuUsagePercent = vmMetrics.CpuUsage;
                metrics.VmMemoryUsageMB = vmMetrics.MemoryUsage;
                metrics.VmDiskUsageMB = vmMetrics.DiskUsage;
            }));
        }

        await Task.WhenAll(tasks);
        return metrics;
    }

    private async Task<(double CpuUsage, long MemoryUsage, long DiskUsage)> GetContainerMetricsAsync()
    {
        try
        {
            // Get container stats using docker stats command
            var statsProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"stats {_containerName} --no-stream --format \"{{{{.CPUPerc}}}}\\t{{{{.MemUsage}}}}\\t{{{{.BlockIO}}}}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            statsProcess.Start();
            var output = await statsProcess.StandardOutput.ReadToEndAsync();
            var error = await statsProcess.StandardError.ReadToEndAsync();
            await statsProcess.WaitForExitAsync();

            if (statsProcess.ExitCode != 0)
            {
                Console.WriteLine($"⚠️  Docker stats failed for container '{_containerName}' (exit code: {statsProcess.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"   Error: {error.Trim()}");
                }
                return (0, 0, 0);
            }

            if (!string.IsNullOrEmpty(output))
            {
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Handle both tabbed and non-tabbed output
                string? dataLine = null;
                if (lines.Length > 1)
                {
                    dataLine = lines[1].Trim(); // Skip header if present
                }
                else if (lines.Length == 1)
                {
                    dataLine = lines[0].Trim(); // Direct stats output without header
                }
                
                if (!string.IsNullOrEmpty(dataLine))
                {
                    // Try parsing as tab-separated first
                    var parts = dataLine.Split('\t');
                    
                    // If no tabs found, split by multiple spaces (Docker's default format)
                    if (parts.Length < 3)
                    {
                        // Split by multiple spaces and filter out empty entries
                        parts = dataLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    
                    if (parts.Length >= 3)
                    {
                        // Parse CPU percentage (first part)
                        var cpuStr = parts[0].Replace("%", "");
                        double.TryParse(cpuStr, out var cpuUsage);

                        // Parse Memory usage - need to find the memory part (contains '/')
                        var memoryStr = "";
                        var diskStr = "";
                        
                        // Look for memory usage pattern (contains MiB/GiB and '/')
                        for (int i = 1; i < parts.Length; i++)
                        {
                            var part = parts[i];
                            if (part.Contains("/") && (part.Contains("MiB") || part.Contains("GiB") || part.Contains("MB") || part.Contains("GB")))
                            {
                                // This is memory: "631.2MiB" (we want the part before '/')
                                memoryStr = part.Split('/')[0].Trim();
                                
                                // BlockIO is typically the next part that contains '/'
                                if (i + 1 < parts.Length && parts[i + 1].Contains("/"))
                                {
                                    diskStr = parts[i + 1].Split('/')[0].Trim();
                                }
                                else if (i + 2 < parts.Length && (parts[i + 1] + " " + parts[i + 2]).Contains("/"))
                                {
                                    // Handle case where BlockIO is split across parts like "0B" "/" "0B"
                                    diskStr = parts[i + 1];
                                }
                                break;
                            }
                        }

                        var memoryUsage = ParseMemoryString(memoryStr);
                        var diskUsage = ParseMemoryString(diskStr);

                        // Debug output (only first successful read to avoid spam)
                        if (!_hasLoggedContainerSuccess && (cpuUsage > 0 || memoryUsage > 0))
                        {
                            Console.WriteLine($"📊 Container '{_containerName}' metrics working - CPU: {cpuUsage:F1}%, Memory: {memoryUsage}MB");
                            _hasLoggedContainerSuccess = true;
                        }

                        return (cpuUsage, memoryUsage, diskUsage);
                    }
                    else
                    {
                        Console.WriteLine($"⚠️  Could not parse docker stats output: '{dataLine}' (only {parts.Length} parts found)");
                        Console.WriteLine($"   Expected format: CPU% Memory/Total BlockIO");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️  No data line found in docker stats output");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Empty output from docker stats for container '{_containerName}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting container metrics for '{_containerName}': {ex.Message}");
        }

        return (0, 0, 0);
    }

    private async Task<(double CpuUsage, long MemoryUsage, long DiskUsage)> GetVMMetricsAsync()
    {
        try
        {
            var cpuTask = GetCpuUsageAsync();
            var memoryTask = GetMemoryUsageAsync();
            var diskTask = GetDiskUsageAsync();

            await Task.WhenAll(cpuTask, memoryTask, diskTask);

            return (await cpuTask, await memoryTask, await diskTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting VM metrics: {ex.Message}");
            return (0, 0, 0);
        }
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Use vmstat for current CPU usage - more accurate than /proc/stat snapshots
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"vmstat 1 2 | tail -1\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    // vmstat output: procs memory swap io system cpu
                    // Last 3 columns are: us sy id (user, system, idle)
                    var parts = output.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 15)
                    {
                        // vmstat format: r b swpd free buff cache si so bi bo in cs us sy id wa st
                        // We want columns: us (user), sy (system), id (idle)
                        if (int.TryParse(parts[12], out var userCpu) &&  // us - user CPU
                            int.TryParse(parts[13], out var systemCpu) && // sy - system CPU  
                            int.TryParse(parts[14], out var idleCpu))     // id - idle CPU
                        {
                            var totalCpu = userCpu + systemCpu + idleCpu;
                            if (totalCpu > 0)
                            {
                                double cpuUsage = (double)(userCpu + systemCpu) / totalCpu * 100.0;
                                return Math.Max(0, Math.Min(100, cpuUsage));
                            }
                        }
                    }
                }

                // Fallback to top command if vmstat fails
                var topProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                topProcess.Start();
                var topOutput = await topProcess.StandardOutput.ReadToEndAsync();
                await topProcess.WaitForExitAsync();

                if (!string.IsNullOrEmpty(topOutput) && double.TryParse(topOutput.Trim(), out var topCpuUsage))
                {
                    return Math.Max(0, Math.Min(100, topCpuUsage));
                }
            }
            else
            {
                // Fallback for non-Linux systems
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "cpu get loadpercentage /value",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var match = Regex.Match(output, @"LoadPercentage=(\d+)");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var cpuUsage))
                {
                    return cpuUsage;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting CPU usage: {ex.Message}");
        }

        return 0;
    }

    private async Task<long> GetMemoryUsageAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read memory usage from /proc/meminfo - CORRECTED for accurate VM memory calculation
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"free -m | grep '^Mem:'\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    // Parse free output: Mem: total used free shared buffers/cache available
                    var parts = output.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        // parts[0] = "Mem:", parts[1] = total, parts[2] = used
                        if (long.TryParse(parts[2], out var usedMemory))
                        {
                            return usedMemory; // Already in MB from 'free -m'
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting memory usage: {ex.Message}");
        }

        return 0;
    }

    private async Task<long> GetDiskUsageAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read disk usage from df - CORRECTED for accurate VM disk calculation  
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = "-c \"df -m / | tail -1\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    // Parse df output: Filesystem 1M-blocks Used Available Use% Mounted
                    var parts = output.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        // parts[0] = filesystem, parts[1] = total, parts[2] = used
                        if (long.TryParse(parts[2], out var usedDisk))
                        {
                            return usedDisk; // Already in MB from 'df -m'
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting disk usage: {ex.Message}");
        }

        return 0;
    }

    private long ParseMemoryString(string memoryStr)
    {
        if (string.IsNullOrEmpty(memoryStr))
            return 0;
            
        // Handle common Docker memory formats: 631.2MiB, 1.5GB, 512MB, etc.
        var match = Regex.Match(memoryStr, @"([\d.]+)([KMGT]?i?B?)");
        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value, out var value))
            {
                var unit = match.Groups[2].Value.ToUpper();
                return unit switch
                {
                    "B" => (long)Math.Round(value / (1024.0 * 1024.0)), // Bytes to MB
                    "KB" => (long)Math.Round(value / 1024.0), // KB to MB
                    "KIB" => (long)Math.Round(value / 1024.0), // KiB to MB
                    "MB" => (long)Math.Round(value), // MB
                    "MIB" => (long)Math.Round(value), // MiB (approximately same as MB for our purposes)
                    "GB" => (long)Math.Round(value * 1024.0), // GB to MB
                    "GIB" => (long)Math.Round(value * 1024.0), // GiB to MB
                    "TB" => (long)Math.Round(value * 1024.0 * 1024.0), // TB to MB
                    "TIB" => (long)Math.Round(value * 1024.0 * 1024.0), // TiB to MB
                    "" => (long)Math.Round(value), // Assume MB if no unit
                    _ => (long)Math.Round(value) // Default to MB
                };
            }
        }
        
        // Fallback: try to extract just the number and assume MB
        var numberMatch = Regex.Match(memoryStr, @"([\d.]+)");
        if (numberMatch.Success && double.TryParse(numberMatch.Groups[1].Value, out var fallbackValue))
        {
            return (long)Math.Round(fallbackValue);
        }
        
        return 0;
    }
}

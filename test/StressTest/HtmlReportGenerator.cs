using System.Text.Json;
using System.Text.Json.Serialization;

namespace StressTest
{
    public class HtmlReportGenerator
    {
        public static void GenerateReport(TestResult result, string outputPath)
        {
            var htmlContent = GenerateHtmlContent(result);
            var reportPath = Path.Combine(outputPath, "stress_test_report.html");
            File.WriteAllText(reportPath, htmlContent);
            
            Console.WriteLine($"📊 HTML report generated: {reportPath}");
        }

        private static string GenerateHtmlContent(TestResult result)
        {
            // Create a simplified data structure for JavaScript that excludes large response content
            var simplifiedResult = new
            {
                TestConfiguration = result.TestConfiguration,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Statistics = result.Statistics,
                PreTestBaseline = result.PreTestBaseline,
                PostTestBaseline = result.PostTestBaseline,
                RequestResults = result.RequestResults.Select(r => new
                {
                    r.Timestamp,
                    r.ResponseTime,
                    r.Success,
                    r.StatusCode,
                    r.Usage,
                    r.IsSuccess,
                    r.TokensGenerated,
                    r.ContainerCpuUsage,
                    r.ContainerMemoryUsage,
                    r.VmCpuUsage,
                    r.VmMemoryUsage,
                    // Include only a preview of response content to avoid huge JSON
                    ResponseContentPreview = r.ResponseContent?.Length > 500 ? 
                        r.ResponseContent.Substring(0, 500) + "..." : r.ResponseContent,
                    RequestData = r.RequestData,
                    // Keep full response data for modal display but simplified
                    ResponseData = r.ResponseData
                }).ToList()
            };

            var jsonData = JsonSerializer.Serialize(simplifiedResult, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>TuxAI Service - Stress Test Report</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
            text-align: center;
        }}

        .container {{
            max-width: 1400px;
            margin: 0 auto;
            background: white;
            border-radius: 20px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.1);
            overflow: hidden;
        }}

        .header {{
            background: linear-gradient(135deg, #2c3e50 0%, #3498db 100%);
            color: white;
            padding: 40px;
            text-align: center;
        }}

        .header h1 {{
            font-size: 2.5em;
            margin-bottom: 10px;
            font-weight: 300;
        }}

        .header .subtitle {{
            font-size: 1.2em;
            opacity: 0.9;
        }}

        .summary-grid {{
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 20px;
            padding: 40px;
            background: #f8f9fa;
        }}

        .section-header {{
            grid-column: 1 / -1;
            font-size: 1.1em;
            font-weight: bold;
            color: #2c3e50;
            padding: 12px;
            margin: 20px 0 10px 0;
            text-align: center;
            background: linear-gradient(135deg, #ffffff, #e9ecef);
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            border-left: 4px solid #3498db;
        }}

        .metric-card {{
            background: white;
            padding: 30px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            text-align: center;
            transition: transform 0.3s ease;
            position: relative;
        }}

        .metric-card:hover {{
            transform: translateY(-5px);
        }}

        .info-icon {{
            position: absolute;
            top: 15px;
            right: 15px;
            width: 20px;
            height: 20px;
            background: #3498db;
            color: white;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 12px;
            font-weight: bold;
            cursor: pointer;
            transition: background 0.3s ease;
        }}

        .info-icon:hover {{
            background: #2980b9;
        }}

        .tooltip {{
            position: absolute;
            top: -10px;
            right: 45px;
            background: #2c3e50;
            color: white;
            padding: 15px;
            border-radius: 8px;
            font-size: 12px;
            line-height: 1.4;
            width: 300px;
            box-shadow: 0 10px 25px rgba(0,0,0,0.2);
            z-index: 1000;
            display: none;
            text-align: left;
        }}

        .tooltip::after {{
            content: '';
            position: absolute;
            top: 20px;
            right: -8px;
            width: 0;
            height: 0;
            border-left: 8px solid #2c3e50;
            border-top: 8px solid transparent;
            border-bottom: 8px solid transparent;
        }}

        .tooltip.show {{
            display: block;
        }}

        .metric-value {{
            font-size: 2.5em;
            font-weight: bold;
            margin-bottom: 10px;
        }}

        .metric-label {{
            color: #666;
            font-size: 1.1em;
        }}

        .success {{ color: #27ae60; }}
        .warning {{ color: #f39c12; }}
        .danger {{ color: #e74c3c; }}
        .info {{ color: #3498db; }}

        .section {{
            padding: 40px;
            text-align: center;
        }}

        .section h2 {{
            font-size: 2em;
            margin-bottom: 30px;
            color: #2c3e50;
            border-bottom: 3px solid #3498db;
            padding-bottom: 10px;
            text-align: center;
        }}

        .section p {{
            text-align: center;
        }}

        .section-description {{
            text-align: left;
            max-width: 800px;
            margin: 0 auto 30px auto;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 8px;
            border-left: 4px solid #3498db;
            color: #555;
            line-height: 1.6;
        }}

        .section ul {{
            text-align: left;
            display: inline-block;
            margin: 0 auto;
        }}

        .charts-grid {{
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 30px;
            margin-bottom: 40px;
            max-width: 1200px;
            margin: 0 auto 40px auto;
        }}

        .chart-container {{
            background: white;
            padding: 30px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            min-height: 400px;
        }}

        .chart-container h3 {{
            text-align: center;
            margin-bottom: 20px;
            color: #2c3e50;
            font-size: 1.3em;
        }}

        .chart-container canvas {{
            max-height: 350px;
        }}

        .full-width-chart {{
            grid-column: 1 / -1;
            max-width: 1000px;
            margin: 30px auto;
        }}

        .requests-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 20px auto;
            background: white;
            border-radius: 10px;
            overflow: hidden;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
        }}

        .requests-table th {{
            background: #34495e;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 500;
        }}

        .requests-table td {{
            padding: 12px 15px;
            border-bottom: 1px solid #eee;
        }}

        .requests-table tr:hover {{
            background: #f8f9fa;
            cursor: pointer;
        }}

        .status-success {{ 
            color: #27ae60; 
            font-weight: bold;
        }}

        .status-error {{ 
            color: #e74c3c; 
            font-weight: bold;
        }}

        .modal {{
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(0,0,0,0.5);
        }}

        .modal-content {{
            background-color: white;
            margin: 5% auto;
            padding: 30px;
            border-radius: 15px;
            width: 80%;
            max-width: 800px;
            max-height: 80vh;
            overflow-y: auto;
            box-shadow: 0 20px 40px rgba(0,0,0,0.2);
        }}

        .close {{
            color: #aaa;
            float: right;
            font-size: 28px;
            font-weight: bold;
            cursor: pointer;
        }}

        .close:hover {{
            color: black;
        }}

        .performance-indicator {{
            display: inline-block;
            padding: 8px 16px;
            border-radius: 20px;
            font-weight: bold;
            margin: 5px;
        }}

        .excellent {{ background: #2ecc71; color: white; }}
        .good {{ background: #f39c12; color: white; }}
        .poor {{ background: #e74c3c; color: white; }}

        .section-header {{
            grid-column: 1 / -1;
            text-align: center;
            font-size: 1.2em;
            font-weight: bold;
            color: #2c3e50;
            margin: 10px 0;
            padding: 10px;
            background: linear-gradient(135deg, #e8f4f8 0%, #d6eaf8 100%);
            border-radius: 10px;
            border-left: 4px solid #3498db;
        }}

        .baseline-comparison {{
            max-width: 1200px;
            margin: 0 auto;
        }}

        .baseline-grid {{
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 30px;
            margin-bottom: 40px;
        }}

        .baseline-card {{
            background: white;
            padding: 30px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            text-align: left;
        }}

        .baseline-card h3 {{
            text-align: center;
            margin-bottom: 20px;
            color: #2c3e50;
            font-size: 1.3em;
        }}

        .baseline-metrics {{
            font-size: 0.95em;
            line-height: 1.6;
        }}

        .baseline-metrics p {{
            margin: 8px 0;
            text-align: left;
        }}

        .impact-analysis {{
            background: white;
            padding: 30px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            margin-top: 20px;
        }}

        .impact-analysis h3 {{
            color: #2c3e50;
            margin-bottom: 20px;
            text-align: center;
        }}

        .impact-grid {{
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 20px;
            margin-bottom: 20px;
        }}

        .impact-metric {{
            padding: 15px;
            border-radius: 10px;
            text-align: center;
        }}

        .impact-high {{ background: #ffebee; color: #c62828; }}
        .impact-moderate {{ background: #fff3e0; color: #ef6c00; }}
        .impact-low {{ background: #f3e5f5; color: #7b1fa2; }}
        .impact-minimal {{ background: #e8f5e8; color: #2e7d32; }}

        @media (max-width: 768px) {{
            .baseline-grid {{
                grid-template-columns: 1fr;
                gap: 20px;
            }}
            
            .impact-grid {{
                grid-template-columns: 1fr;
                gap: 15px;
            }}
        }}

        @media (max-width: 768px) {{
            .charts-grid {{
                grid-template-columns: 1fr;
                gap: 20px;
                margin: 0 auto 30px auto;
                max-width: 100%;
            }}
            
            .chart-container {{
                min-height: 350px;
                padding: 20px;
            }}
            
            .summary-grid {{
                grid-template-columns: repeat(2, 1fr);
                gap: 15px;
                padding: 20px;
            }}
            
            .container {{
                margin: 10px;
                border-radius: 10px;
            }}
        }}

        @media (max-width: 480px) {{
            .summary-grid {{
                grid-template-columns: 1fr;
                gap: 10px;
                padding: 15px;
            }}
            
            .section-header {{
                margin: 15px 0 8px 0;
                padding: 10px;
                font-size: 1em;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🧪 TuxAI Service Stress Test Report</h1>
            <div class=""subtitle"">Performance Analysis & Executive Summary</div>
            <div style=""margin-top: 20px; font-size: 1em;"">
                Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC
            </div>
        </div>

        <div class=""summary-grid"">
            <!-- Row 1: Core Request Metrics -->
            <div class=""section-header"">📊 Core Request Metrics</div>
            
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Total Requests:</strong> The total number of API requests sent to the service during the test period.<br><br>
                    <strong>Data:</strong> {result.Statistics.TotalRequests:N0} requests sent over {(result.EndTime - result.StartTime).TotalMinutes:F1} minutes
                </div>
                <div class=""metric-value success"">{result.Statistics.TotalRequests:N0}</div>
                <div class=""metric-label"">Total Requests</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Success Rate:</strong> Percentage of requests that completed successfully without errors.<br><br>
                    <strong>Calculation:</strong> ({result.Statistics.SuccessfulRequests} successful ÷ {result.Statistics.TotalRequests} total) × 100<br>
                    <strong>Good:</strong> >95%, <strong>Acceptable:</strong> >90%
                </div>
                <div class=""metric-value {(result.Statistics.SuccessRate >= 95 ? "success" : result.Statistics.SuccessRate >= 90 ? "warning" : "danger")}"">{result.Statistics.SuccessRate:F1}%</div>
                <div class=""metric-label"">Success Rate</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Average Response Time:</strong> Mean time for the AI model to process and respond to requests.<br><br>
                    <strong>Current:</strong> {result.Statistics.AverageResponseTime.TotalSeconds:F1}s per request<br>
                    <strong>Range:</strong> {result.Statistics.MinResponseTime.TotalSeconds:F1}s - {result.Statistics.MaxResponseTime.TotalSeconds:F1}s<br>
                    <strong>Note:</strong> LLM inference is typically slow for complex models
                </div>
                <div class=""metric-value info"">{result.Statistics.AverageResponseTime.TotalSeconds:F1}s</div>
                <div class=""metric-label"">Avg Response Time</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Tokens Per Second:</strong> Rate of text generation by the AI model.<br><br>
                    <strong>Calculation:</strong> {result.Statistics.TotalTokensGenerated} total tokens ÷ {(result.EndTime - result.StartTime).TotalSeconds:F0} seconds<br>
                    <strong>Industry standard:</strong> 15-50 tokens/sec for small models, 5-15 for large models
                </div>
                <div class=""metric-value success"">{result.Statistics.TokensPerSecond:F1}</div>
                <div class=""metric-label"">Tokens/Second</div>
            </div>
            
            <!-- Row 2: Container Level Metrics -->
            <div class=""section-header"">🐳 Container Level Metrics</div>
            
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Average Container CPU:</strong> Mean CPU utilization by the Docker container during the test.<br><br>
                    <strong>Method:</strong> Uses 'docker stats' command to monitor container {result.TestConfiguration?.ContainerName ?? "tux-ai-service"}<br>
                    <strong>Measured:</strong> {result.Statistics.AvgContainerCpuUsage:F1}% average over {(result.EndTime - result.StartTime).TotalMinutes:F1} minutes<br>
                    <strong>Note:</strong> If showing 0%, check container name with 'docker ps'<br>
                    <strong>Optimal:</strong> <50% for room to scale
                </div>
                <div class=""metric-value {(result.Statistics.AvgContainerCpuUsage <= 50 ? "success" : result.Statistics.AvgContainerCpuUsage <= 70 ? "warning" : "danger")}"">{result.Statistics.AvgContainerCpuUsage:F1}%</div>
                <div class=""metric-label"">Avg Container CPU</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Average Container Memory:</strong> Mean RAM consumption by the Docker container.<br><br>
                    <strong>Method:</strong> Uses 'docker stats' command to monitor container {result.TestConfiguration?.ContainerName ?? "tux-ai-service"}<br>
                    <strong>Measured:</strong> {result.Statistics.AvgContainerMemoryUsage:N0} MB average<br>
                    <strong>Peak:</strong> {result.Statistics.MaxMemoryUsage:N0} MB<br>
                    <strong>Includes:</strong> Model weights, tokenizer, and inference buffers
                </div>
                <div class=""metric-value info"">{result.Statistics.AvgContainerMemoryUsage:N0} MB</div>
                <div class=""metric-label"">Avg Container Memory</div>
            </div>
            
            <!-- Row 3: Load Added to VM During SLM Inference -->
            <div class=""section-header"">� Load Added to VM During SLM Inference</div>
            
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>VM CPU Load Increase:</strong> Additional CPU usage added by SLM inference compared to baseline.<br><br>
                    <strong>Calculation:</strong> Peak SLM Usage - Pre-Test Baseline<br>
                    <strong>Baseline VM CPU:</strong> <span id=""baseline-vm-cpu"">N/A</span><br>
                    <strong>Peak During Test:</strong> {result.Statistics.MaxVmCpuUsage:F1}%<br>
                    <strong>SLM Impact:</strong> <span id=""vm-cpu-delta"">Calculating...</span>
                </div>
                <div class=""metric-value"" id=""vm-cpu-impact-value"">Calculating...</div>
                <div class=""metric-label"">VM CPU Load Added</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>VM Memory Load Increase:</strong> Additional memory usage added by SLM inference compared to baseline.<br><br>
                    <strong>Calculation:</strong> Peak SLM Memory - Pre-Test Baseline Memory<br>
                    <strong>Baseline VM Memory:</strong> <span id=""baseline-vm-memory"">N/A</span> MB<br>
                    <strong>Peak During Test:</strong> {result.Statistics.MaxVmMemoryUsage:N0} MB<br>
                    <strong>SLM Impact:</strong> <span id=""vm-memory-delta"">Calculating...</span> MB
                </div>
                <div class=""metric-value"" id=""vm-memory-impact-value"">Calculating...</div>
                <div class=""metric-label"">VM Memory Added</div>
            </div>
            <div class=""metric-card"">
                <div class=""info-icon"" onclick=""toggleTooltip(this)"">i</div>
                <div class=""tooltip"">
                    <strong>Actual Memory Added:</strong> Absolute amount of additional RAM consumed by SLM inference.<br><br>
                    <strong>Calculation:</strong> Peak Test Memory - Baseline Memory<br>
                    <strong>Before SLM:</strong> <span id=""baseline-vm-memory-abs"">N/A</span> MB<br>
                    <strong>During SLM:</strong> {result.Statistics.MaxVmMemoryUsage:N0} MB<br>
                    <strong>Additional Load:</strong> <span id=""vm-memory-added"">Calculating...</span> MB
                </div>
                <div class=""metric-value"" id=""vm-memory-added-value"">Calculating...</div>
                <div class=""metric-label"">Actual Memory Added</div>
            </div>
        </div>

        <!-- Baseline Comparison Section -->
        <div class=""section"" id=""baseline-section"" style=""display: none;"">
            <h2>🔍 Baseline Resource Comparison</h2>
            <div class=""baseline-comparison"">
                <div class=""baseline-grid"">
                    <div class=""baseline-card"">
                        <h3>📌 Pre-Test Baseline</h3>
                        <div class=""baseline-metrics"" id=""pre-test-metrics"">
                            <!-- Pre-test metrics will be populated by JavaScript -->
                        </div>
                    </div>
                    <div class=""baseline-card"">
                        <h3>🎯 During Stress Test</h3>
                        <div class=""baseline-metrics"" id=""stress-test-metrics"">
                            <!-- Stress test metrics will be populated by JavaScript -->
                        </div>
                    </div>
                    <div class=""baseline-card"">
                        <h3>📌 Post-Test Baseline</h3>
                        <div class=""baseline-metrics"" id=""post-test-metrics"">
                            <!-- Post-test metrics will be populated by JavaScript -->
                        </div>
                    </div>
                </div>
                <div class=""impact-analysis"" id=""impact-analysis"">
                    <!-- Impact analysis will be populated by JavaScript -->
                </div>
            </div>
        </div>

        <div class=""section"">
            <h2>🎯 Executive Recommendation</h2>
            <div id=""recommendation-content"">
                <!-- Recommendation content will be generated by JavaScript -->
            </div>
        </div>

        <div class=""section"">
            <h2>📊 Performance Metrics</h2>
            <p class=""section-description"">
                The charts below show individual request performance and system resource correlation. 
                The <strong>Individual Request Performance</strong> chart displays each request's response time and tokens generated, 
                allowing you to identify patterns and outliers. The <strong>Response Time vs System Resource Usage</strong> chart 
                correlates response times with CPU usage to help understand performance bottlenecks.
            </p>
            <div class=""charts-grid"">
                <div class=""chart-container"">
                    <h3>Response Time Distribution</h3>
                    <canvas id=""responseTimeChart""></canvas>
                </div>
                <div class=""chart-container"">
                    <h3>Container Resource Usage</h3>
                    <canvas id=""containerResourceChart""></canvas>
                </div>
            </div>
            <div class=""charts-grid"">
                <div class=""chart-container"">
                    <h3>VM Resource Usage</h3>
                    <canvas id=""vmResourceChart""></canvas>
                </div>
                <div class=""chart-container"">
                    <h3>Individual Request Performance</h3>
                    <canvas id=""timelineChart""></canvas>
                </div>
            </div>
            <div class=""chart-container full-width-chart"">
                <h3>Response Time vs System Resource Usage Over Time</h3>
                <canvas id=""detailedMetricsChart""></canvas>
            </div>
        </div>

        <div class=""section"">
            <h2>📋 Detailed Request Analysis</h2>
            <p>Click on any request to view detailed information including response content and performance metrics.</p>
            <table class=""requests-table"" id=""requestsTable"">
                <thead>
                    <tr>
                        <th>Request #</th>
                        <th>Timestamp</th>
                        <th>Status</th>
                        <th>Response Time</th>
                        <th>Tokens Generated</th>
                        <th>Container CPU</th>
                        <th>Container Memory</th>
                        <th>VM CPU</th>
                        <th>VM Memory</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>

    <!-- Modal for request details -->
    <div id=""requestModal"" class=""modal"">
        <div class=""modal-content"">
            <span class=""close"">&times;</span>
            <div id=""modalContent""></div>
        </div>
    </div>

    <script>
        // Test data
        const testData = {jsonData};

        // Initialize the report
        document.addEventListener('DOMContentLoaded', function() {{
            try {{
                generateRecommendation();
            }} catch (error) {{
                console.error('Error generating recommendation:', error);
            }}
            
            try {{
                populateBaselineComparison();
            }} catch (error) {{
                console.error('Error populating baseline comparison:', error);
            }}
            
            try {{
                populateRequestsTable();
            }} catch (error) {{
                console.error('Error populating requests table:', error);
            }}
            
            try {{
                createCharts();
            }} catch (error) {{
                console.error('Error creating charts:', error);
            }}
            
            try {{
                setupModal();
            }} catch (error) {{
                console.error('Error setting up modal:', error);
            }}
        }});

        function generateRecommendation() {{
            const stats = testData.statistics;
            const baseline = testData.preTestBaseline;
            const content = document.getElementById('recommendation-content');
            
            if (!content) {{
                console.warn('Recommendation content element not found');
                return;
            }}
            
            // Calculate SLM's actual VM impact for co-tenancy assessment
            const slmVmImpact = baseline ? stats.maxVmCpuUsage - baseline.avgVmCpuUsage : stats.maxVmCpuUsage;
            const slmMemoryImpact = baseline ? stats.maxVmMemoryUsage - baseline.avgVmMemoryUsage : 0;
            
            let recommendation = '';
            let coTenancyLevel = '';
            
            // Co-tenancy assessment based on SLM's VM impact
            if (slmVmImpact <= 20 && stats.successRate >= 95) {{
                coTenancyLevel = '🟢 EXCELLENT CO-TENANCY';
                recommendation = 
                    '<div class=""performance-indicator excellent"">✅ SAFE FOR CO-TENANCY</div>' +
                    '<p><strong>SLM Impact Analysis - IDEAL for customer applications:</strong></p>' +
                    '<ul>' +
                        '<li>🎯 <strong>SLM VM Impact: ' + slmVmImpact.toFixed(1) + '%</strong> (Target: &lt;20% ✅)</li>' +
                        '<li>🏠 VM Resources Available: ' + (100 - stats.maxVmCpuUsage).toFixed(1) + '% CPU remaining for customer apps</li>' +
                        '<li>✅ High success rate (' + stats.successRate.toFixed(1) + '%) ensures reliable AI service</li>' +
                        '<li>⚡ Response time: ' + parseTimeSpan(stats.averageResponseTime).toFixed(1) + 's ' + (parseTimeSpan(stats.averageResponseTime) <= 180 ? '(excellent user experience)' : '(acceptable for AI workloads)') + '</li>' +
                        '<li>📊 Memory impact: ' + (slmMemoryImpact > 0 ? '+' + slmMemoryImpact.toFixed(0) + 'MB' : 'minimal') + '</li>' +
                    '</ul>' +
                    '<p><strong>🎯 Business Decision:</strong> <span style=""color: green; font-weight: bold;"">Deploy SLM + customer applications on shared VMs</span></p>' +
                    '<p>💰 <strong>Cost Efficiency:</strong> Maximize VM utilization without resource starvation</p>';
            }} else if (slmVmImpact <= 35 && stats.successRate >= 90) {{
                coTenancyLevel = '🟡 MODERATE CO-TENANCY';
                recommendation = 
                    '<div class=""performance-indicator good"">⚠️ RISKY CO-TENANCY</div>' +
                    '<p><strong>SLM Impact Analysis - MONITOR customer application performance:</strong></p>' +
                    '<ul>' +
                        '<li>⚠️ <strong>SLM VM Impact: ' + slmVmImpact.toFixed(1) + '%</strong> (Target: &lt;20% ❌)</li>' +
                        '<li>🏠 VM Resources Available: ' + (100 - stats.maxVmCpuUsage).toFixed(1) + '% CPU remaining for customer apps</li>' +
                        '<li>' + (stats.successRate >= 95 ? '✅' : '⚠️') + ' Success rate: ' + stats.successRate.toFixed(1) + '%</li>' +
                        '<li>⚡ Response time: ' + parseTimeSpan(stats.averageResponseTime).toFixed(1) + 's ' + (parseTimeSpan(stats.averageResponseTime) <= 180 ? '(healthy)' : '(slow - consider optimization)') + '</li>' +
                        '<li>📊 Memory impact: ' + (slmMemoryImpact > 0 ? '+' + slmMemoryImpact.toFixed(0) + 'MB' : 'minimal') + '</li>' +
                    '</ul>' +
                    '<p><strong>🎯 Business Decision:</strong> <span style=""color: orange; font-weight: bold;"">Careful co-tenancy with performance monitoring</span></p>' +
                    '<p>⚠️ <strong>Risk Assessment:</strong> Customer apps may experience performance degradation during peak SLM usage</p>';
            }} else {{
                coTenancyLevel = '🔴 POOR CO-TENANCY';
                recommendation = 
                    '<div class=""performance-indicator poor"">❌ UNSAFE FOR CO-TENANCY</div>' +
                    '<p><strong>SLM Impact Analysis - WILL STARVE customer applications:</strong></p>' +
                    '<ul>' +
                        '<li>❌ <strong>SLM VM Impact: ' + slmVmImpact.toFixed(1) + '%</strong> (Target: &lt;20% ❌)</li>' +
                        '<li>⚠️ VM Resources Available: ' + (100 - stats.maxVmCpuUsage).toFixed(1) + '% CPU remaining (insufficient)</li>' +
                        '<li>' + (stats.successRate >= 90 ? '✅' : '❌') + ' Success rate: ' + stats.successRate.toFixed(1) + '%</li>' +
                        '<li>⚡ Response time: ' + parseTimeSpan(stats.averageResponseTime).toFixed(1) + 's ' + (parseTimeSpan(stats.averageResponseTime) <= 180 ? '(acceptable)' : '(too slow - requires optimization)') + '</li>' +
                        '<li>📊 Memory impact: ' + (slmMemoryImpact > 0 ? '+' + slmMemoryImpact.toFixed(0) + 'MB' : 'minimal') + '</li>' +
                    '</ul>' +
                    '<p><strong>🎯 Business Decision:</strong> <span style=""color: red; font-weight: bold;"">Require dedicated SLM VMs</span></p>' +
                    '<p>💸 <strong>Cost Impact:</strong> Higher infrastructure cost but guaranteed customer app performance</p>' +
                    '<p><strong>Recommendation:</strong> ' + (stats.maxVmCpuUsage > 80 ? 'Scale up VM size or reduce SLM load' : 'Optimize SLM model or deployment configuration') + '</p>';
            }}
            
            content.innerHTML = 
                '<div style=""margin-bottom: 20px;"">' +
                    '<h4>' + coTenancyLevel + '</h4>' +
                '</div>' +
                recommendation +
                '<div style=""margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-left: 4px solid #007bff; font-size: 0.9em;"">' +
                    '<strong>📋 Co-Tenancy Guidelines:</strong><br>' +
                    '• <strong>&lt;20% SLM Impact:</strong> Safe for shared VMs with customer applications<br>' +
                    '• <strong>20-35% SLM Impact:</strong> Risky - monitor customer app performance closely<br>' +
                    '• <strong>&gt;35% SLM Impact:</strong> Requires dedicated SLM infrastructure<br>' +
                    '<br>' +
                    '<strong>Performance Thresholds:</strong><br>' +
                    '• <strong>Response Time &lt;3min (180s):</strong> Healthy for AI workloads<br>' +
                    '• <strong>Success Rate &gt;90%:</strong> Acceptable reliability<br>' +
                    '• <strong>Container CPU ' + stats.maxCpuUsage.toFixed(1) + '%:</strong> Normal for AI inference<br>' +
                    '<br>' +
                    '<em>Focus on VM CPU impact for co-tenancy business decisions.</em>' +
                '</div>';
        }}

        function populateBaselineComparison() {{
            // Check if baseline data exists
            if (!testData.preTestBaseline && !testData.postTestBaseline) {{
                // Hide baseline section if no data
                const baselineSection = document.getElementById('baseline-section');
                if (baselineSection) {{
                    baselineSection.style.display = 'none';
                }}
                return;
            }}

            // Show baseline section
            const baselineSection = document.getElementById('baseline-section');
            if (baselineSection) {{
                baselineSection.style.display = 'block';
            }}

            // Populate pre-test baseline
            if (testData.preTestBaseline) {{
                const preTestElement = document.getElementById('pre-test-metrics');
                if (preTestElement) {{
                    preTestElement.innerHTML = `
                        <p><strong>Container CPU:</strong> ${{testData.preTestBaseline.avgContainerCpuUsage.toFixed(1)}}% avg, ${{testData.preTestBaseline.maxContainerCpuUsage.toFixed(1)}}% max</p>
                        <p><strong>Container Memory:</strong> ${{testData.preTestBaseline.avgContainerMemoryUsage.toLocaleString()}} MB avg, ${{testData.preTestBaseline.maxContainerMemoryUsage.toLocaleString()}} MB max</p>
                        <p><strong>VM CPU:</strong> ${{testData.preTestBaseline.avgVmCpuUsage.toFixed(1)}}% avg, ${{testData.preTestBaseline.maxVmCpuUsage.toFixed(1)}}% max</p>
                        <p><strong>VM Memory:</strong> ${{testData.preTestBaseline.avgVmMemoryUsage.toLocaleString()}} MB avg, ${{testData.preTestBaseline.maxVmMemoryUsage.toLocaleString()}} MB max</p>
                        <p style=""font-size: 0.9em; color: #666; margin-top: 10px;"">
                            Duration: ${{((new Date(testData.preTestBaseline.endTime).getTime() - new Date(testData.preTestBaseline.startTime).getTime()) / 1000 / 60).toFixed(1)}} minutes
                        </p>
                    `;
                }}
            }}

            // Populate stress test metrics
            const stressTestElement = document.getElementById('stress-test-metrics');
            if (stressTestElement) {{
                const stats = testData.statistics;
                stressTestElement.innerHTML = `
                    <p><strong>Container CPU:</strong> ${{stats.averageCpuUsage.toFixed(1)}}% avg, ${{stats.maxCpuUsage.toFixed(1)}}% max</p>
                    <p><strong>Container Memory:</strong> ${{stats.averageMemoryUsage.toLocaleString()}} MB avg, ${{stats.maxMemoryUsage.toLocaleString()}} MB max</p>
                    <p><strong>VM CPU:</strong> ${{stats.avgVmCpuUsage.toFixed(1)}}% avg, ${{stats.maxVmCpuUsage.toFixed(1)}}% max</p>
                    <p><strong>VM Memory:</strong> ${{stats.avgVmMemoryUsage.toLocaleString()}} MB avg, ${{stats.maxVmMemoryUsage.toLocaleString()}} MB max</p>
                    <p style=""font-size: 0.9em; color: #666; margin-top: 10px;"">
                        Duration: ${{((new Date(testData.endTime).getTime() - new Date(testData.startTime).getTime()) / 1000 / 60).toFixed(1)}} minutes
                    </p>
                `;
            }}

            // Populate post-test baseline
            if (testData.postTestBaseline) {{
                const postTestElement = document.getElementById('post-test-metrics');
                if (postTestElement) {{
                    postTestElement.innerHTML = `
                        <p><strong>Container CPU:</strong> ${{testData.postTestBaseline.avgContainerCpuUsage.toFixed(1)}}% avg, ${{testData.postTestBaseline.maxContainerCpuUsage.toFixed(1)}}% max</p>
                        <p><strong>Container Memory:</strong> ${{testData.postTestBaseline.avgContainerMemoryUsage.toLocaleString()}} MB avg, ${{testData.postTestBaseline.maxContainerMemoryUsage.toLocaleString()}} MB max</p>
                        <p><strong>VM CPU:</strong> ${{testData.postTestBaseline.avgVmCpuUsage.toFixed(1)}}% avg, ${{testData.postTestBaseline.maxVmCpuUsage.toFixed(1)}}% max</p>
                        <p><strong>VM Memory:</strong> ${{testData.postTestBaseline.avgVmMemoryUsage.toLocaleString()}} MB avg, ${{testData.postTestBaseline.maxVmMemoryUsage.toLocaleString()}} MB max</p>
                        <p style=""font-size: 0.9em; color: #666; margin-top: 10px;"">
                            Duration: ${{((new Date(testData.postTestBaseline.endTime).getTime() - new Date(testData.postTestBaseline.startTime).getTime()) / 1000 / 60).toFixed(1)}} minutes
                        </p>
                    `;
                }}
            }}

            // Populate impact analysis
            if (testData.preTestBaseline && testData.postTestBaseline) {{
                const impactElement = document.getElementById('impact-analysis');
                if (impactElement) {{
                    const stats = testData.statistics;
                    const preTest = testData.preTestBaseline;
                    const postTest = testData.postTestBaseline;
                    
                    const containerCpuDelta = stats.maxCpuUsage - preTest.avgContainerCpuUsage;
                    const containerMemoryDelta = stats.maxMemoryUsage - preTest.avgContainerMemoryUsage;
                    const vmCpuDelta = stats.maxVmCpuUsage - preTest.avgVmCpuUsage;
                    const vmMemoryDelta = stats.maxVmMemoryUsage - preTest.avgVmMemoryUsage;
                    
                    const cpuRecovered = Math.abs(postTest.avgContainerCpuUsage - preTest.avgContainerCpuUsage) < 2.0;
                    const memoryRecovered = Math.abs(postTest.avgContainerMemoryUsage - preTest.avgContainerMemoryUsage) < 50;
                    
                    function getImpactClass(delta, type) {{
                        if (type === 'CPU') {{
                            if (delta > 50) return 'impact-high';
                            if (delta > 20) return 'impact-moderate';
                            if (delta > 5) return 'impact-low';
                            return 'impact-minimal';
                        }} else {{
                            if (delta > 500) return 'impact-high';
                            if (delta > 200) return 'impact-moderate';
                            if (delta > 50) return 'impact-low';
                            return 'impact-minimal';
                        }}
                    }}
                    
                    function getImpactText(delta, type) {{
                        if (type === 'CPU') {{
                            if (delta > 50) return 'High Impact';
                            if (delta > 20) return 'Moderate Impact';
                            if (delta > 5) return 'Low Impact';
                            return 'Minimal Impact';
                        }} else {{
                            if (delta > 500) return 'High Impact';
                            if (delta > 200) return 'Moderate Impact';
                            if (delta > 50) return 'Low Impact';
                            return 'Minimal Impact';
                        }}
                    }}
                    
                    impactElement.innerHTML = `
                        <h3>📈 Resource Impact Analysis</h3>
                        <div class=""impact-grid"">
                            <div class=""impact-metric ${{getImpactClass(containerCpuDelta, 'CPU')}}"">
                                <strong>Container CPU</strong><br>
                                ${{containerCpuDelta > 0 ? '+' : ''}}${{containerCpuDelta.toFixed(1)}}%<br>
                                <small>${{getImpactText(containerCpuDelta, 'CPU')}}</small>
                            </div>
                            <div class=""impact-metric ${{getImpactClass(containerMemoryDelta, 'Memory')}}"">
                                <strong>Container Memory</strong><br>
                                ${{containerMemoryDelta > 0 ? '+' : ''}}${{containerMemoryDelta.toFixed(0)}} MB<br>
                                <small>${{getImpactText(containerMemoryDelta, 'Memory')}}</small>
                            </div>
                            <div class=""impact-metric ${{getImpactClass(vmCpuDelta, 'CPU')}}"">
                                <strong>VM CPU</strong><br>
                                ${{vmCpuDelta > 0 ? '+' : ''}}${{vmCpuDelta.toFixed(1)}}%<br>
                                <small>${{getImpactText(vmCpuDelta, 'CPU')}}</small>
                            </div>
                            <div class=""impact-metric ${{getImpactClass(vmMemoryDelta, 'Memory')}}"">
                                <strong>VM Memory</strong><br>
                                ${{vmMemoryDelta > 0 ? '+' : ''}}${{vmMemoryDelta.toFixed(0)}} MB<br>
                                <small>${{getImpactText(vmMemoryDelta, 'Memory')}}</small>
                            </div>
                        </div>
                        <div style=""background: #f8f9fa; padding: 20px; border-radius: 10px; margin-top: 20px;"">
                            <h4>🔄 Recovery Analysis</h4>
                            <p><strong>Container CPU Recovery:</strong> ${{cpuRecovered ? '✅ Good' : '⚠️ Slow'}} 
                               (post-test: ${{postTest.avgContainerCpuUsage.toFixed(1)}}% vs pre-test: ${{preTest.avgContainerCpuUsage.toFixed(1)}}%)</p>
                            <p><strong>Container Memory Recovery:</strong> ${{memoryRecovered ? '✅ Good' : '⚠️ Slow'}} 
                               (post-test: ${{postTest.avgContainerMemoryUsage.toFixed(0)}} MB vs pre-test: ${{preTest.avgContainerMemoryUsage.toFixed(0)}} MB)</p>
                        </div>
                    `;
                }}
            }}
            
            // Populate VM Impact metrics in the main metrics section
            populateVmImpactMetrics();
        }}

        function populateVmImpactMetrics() {{
            const stats = testData.statistics;
            const preTest = testData.preTestBaseline;
            
            if (preTest) {{
                // Calculate VM CPU impact
                const vmCpuDelta = stats.maxVmCpuUsage - preTest.avgVmCpuUsage;
                const vmCpuAbsoluteIncrease = vmCpuDelta; // Absolute CPU percentage points added
                
                // Calculate VM Memory impact
                const vmMemoryDelta = stats.maxVmMemoryUsage - preTest.avgVmMemoryUsage;
                const vmMemoryAbsoluteIncrease = vmMemoryDelta; // Absolute memory MB added
                
                // Update baseline values in tooltips
                document.getElementById('baseline-vm-cpu').textContent = preTest.avgVmCpuUsage.toFixed(1) + '%';
                document.getElementById('baseline-vm-memory').textContent = preTest.avgVmMemoryUsage.toLocaleString();
                document.getElementById('baseline-vm-memory-abs').textContent = preTest.avgVmMemoryUsage.toLocaleString();
                
                // Update delta values in tooltips
                document.getElementById('vm-cpu-delta').textContent = (vmCpuDelta > 0 ? '+' : '') + vmCpuDelta.toFixed(2) + '% CPU load added';
                document.getElementById('vm-memory-delta').textContent = (vmMemoryDelta > 0 ? '+' : '') + vmMemoryDelta.toFixed(0);
                document.getElementById('vm-memory-added').textContent = (vmMemoryDelta > 0 ? '+' : '') + vmMemoryDelta.toFixed(0);
                
                // Update main metric values
                const cpuImpactElement = document.getElementById('vm-cpu-impact-value');
                const memoryImpactElement = document.getElementById('vm-memory-impact-value');
                const memoryAddedElement = document.getElementById('vm-memory-added-value');
                
                if (cpuImpactElement) {{
                    cpuImpactElement.textContent = (vmCpuAbsoluteIncrease > 0 ? '+' : '') + vmCpuAbsoluteIncrease.toFixed(1) + '%';
                    cpuImpactElement.className = 'metric-value ' + getImpactCssClass(vmCpuAbsoluteIncrease, 'cpu');
                }}
                
                if (memoryImpactElement) {{
                    memoryImpactElement.textContent = (vmMemoryAbsoluteIncrease > 0 ? '+' : '') + vmMemoryAbsoluteIncrease.toFixed(0) + ' MB';
                    memoryImpactElement.className = 'metric-value ' + getImpactCssClass(vmMemoryAbsoluteIncrease, 'memory');
                }}
                
                if (memoryAddedElement) {{
                    memoryAddedElement.textContent = (vmMemoryDelta > 0 ? '+' : '') + vmMemoryDelta.toFixed(0) + ' MB';
                    memoryAddedElement.className = 'metric-value ' + getImpactCssClass(vmMemoryDelta, 'memory');
                }}
            }} else {{
                // No baseline available
                document.getElementById('vm-cpu-impact-value').textContent = 'No Baseline';
                document.getElementById('vm-memory-impact-value').textContent = 'No Baseline';
                document.getElementById('vm-memory-added-value').textContent = 'No Baseline';
            }}
        }}
        
        function getImpactCssClass(value, type) {{
            if (type === 'percentage') {{
                if (value <= 5) return 'success';   // Green for low impact
                if (value <= 15) return 'warning';  // Orange for moderate impact
                return 'danger';                    // Red for high impact
            }} else if (type === 'cpu') {{
                // For absolute CPU percentage points added (e.g., 4.2% means 4.2 percentage points)
                if (value <= 10) return 'success';  // Green for low CPU impact (<10% points)
                if (value <= 25) return 'warning';  // Orange for moderate CPU impact (10-25% points)
                return 'danger';                    // Red for high CPU impact (>25% points)
            }} else if (type === 'memory') {{
                if (value <= 50) return 'success';  // Green for low memory impact
                if (value <= 200) return 'warning'; // Orange for moderate memory impact
                return 'danger';                    // Red for high memory impact
            }}
            return 'info';
        }}

        function populateRequestsTable() {{
            const tbody = document.querySelector('#requestsTable tbody');
            
            testData.requestResults.forEach((request, index) => {{
                const row = document.createElement('tr');
                row.onclick = () => showRequestDetails(request, index + 1);
                
                const status = request.success ? 'Success' : 'Error';
                const statusClass = request.success ? 'status-success' : 'status-error';
                
                // Parse the response time from .NET TimeSpan format to seconds
                const responseTimeSeconds = parseTimeSpan(request.responseTime);
                
                row.innerHTML = `
                    <td>${{index + 1}}</td>
                    <td>${{new Date(request.timestamp).toLocaleTimeString()}}</td>
                    <td class=""${{statusClass}}"">${{status}}</td>
                    <td>${{responseTimeSeconds.toFixed(1)}}s</td>
                    <td>${{request.tokensGenerated || request.usage?.completion_tokens || 'N/A'}}</td>
                    <td>${{request.containerCpuUsage?.toFixed(1) || 'N/A'}}%</td>
                    <td>${{request.containerMemoryUsage?.toFixed(0) || 'N/A'}}MB</td>
                    <td>${{request.vmCpuUsage?.toFixed(1) || 'N/A'}}%</td>
                    <td>${{request.vmMemoryUsage?.toFixed(0) || 'N/A'}}MB</td>
                `;
                
                tbody.appendChild(row);
            }});
        }}

        function parseTimeSpan(timeSpanString) {{
            // Parse .NET TimeSpan format like '00:00:51.6307373' to seconds
            const parts = timeSpanString.split(':');
            const hours = parseInt(parts[0]);
            const minutes = parseInt(parts[1]);
            const secondsParts = parts[2].split('.');
            const seconds = parseInt(secondsParts[0]);
            const milliseconds = secondsParts[1] ? parseInt(secondsParts[1].substring(0, 3)) : 0;
            
            return (hours * 3600 + minutes * 60 + seconds) + (milliseconds / 1000);
        }}

        function toggleTooltip(icon) {{
            const tooltip = icon.nextElementSibling;
            const allTooltips = document.querySelectorAll('.tooltip');
            
            // Hide all other tooltips
            allTooltips.forEach(t => {{
                if (t !== tooltip) {{
                    t.classList.remove('show');
                }}
            }});
            
            // Toggle current tooltip
            tooltip.classList.toggle('show');
        }}

        // Close tooltips when clicking outside
        document.addEventListener('click', function(event) {{
            if (!event.target.closest('.metric-card')) {{
                document.querySelectorAll('.tooltip').forEach(t => {{
                    t.classList.remove('show');
                }});
            }}
        }});

        function showRequestDetails(request, requestNumber) {{
            const modal = document.getElementById('requestModal');
            const content = document.getElementById('modalContent');
            
            const responseTimeSeconds = parseTimeSpan(request.responseTime);
            
            content.innerHTML = `
                <h2>Request #${{requestNumber}} Details</h2>
                <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin: 20px 0;"">
                    <div>
                        <h3>Request Information</h3>
                        <p><strong>Timestamp:</strong> ${{new Date(request.timestamp).toLocaleString()}}</p>
                        <p><strong>Status:</strong> <span class=""${{request.success ? 'status-success' : 'status-error'}}"">${{request.success ? 'Success' : 'Error'}}</span></p>
                        <p><strong>Response Time:</strong> ${{responseTimeSeconds.toFixed(2)}}s</p>
                        <p><strong>Tokens Generated:</strong> ${{request.tokensGenerated || request.usage?.completion_tokens || 'N/A'}}</p>
                        <p><strong>Status Code:</strong> ${{request.statusCode}}</p>
                    </div>
                    <div>
                        <h3>Resource Usage</h3>
                        <p><strong>Container CPU:</strong> ${{request.containerCpuUsage?.toFixed(1) || 'N/A'}}%</p>
                        <p><strong>Container Memory:</strong> ${{request.containerMemoryUsage?.toFixed(0) || 'N/A'}}MB</p>
                        <p><strong>VM CPU:</strong> ${{request.vmCpuUsage?.toFixed(1) || 'N/A'}}%</p>
                        <p><strong>VM Memory:</strong> ${{request.vmMemoryUsage?.toFixed(0) || 'N/A'}}MB</p>
                    </div>
                </div>
                <div>
                    <h3>Request Details</h3>
                    <pre style=""background: #f8f9fa; padding: 15px; border-radius: 8px; overflow-x: auto;"">${{JSON.stringify(request.requestData, null, 2)}}</pre>
                </div>
                ${{request.responseData ? `
                <div>
                    <h3>Response Content</h3>
                    <pre style=""background: #f8f9fa; padding: 15px; border-radius: 8px; overflow-x: auto; max-height: 300px;"">${{JSON.stringify(request.responseData, null, 2)}}</pre>
                </div>
                ` : ''}}
                ${{request.errorMessage ? `
                <div>
                    <h3>Error Details</h3>
                    <div style=""background: #ffebee; color: #c62828; padding: 15px; border-radius: 8px;"">${{request.errorMessage}}</div>
                </div>
                ` : ''}}
            `;
            
            modal.style.display = 'block';
        }}

        function setupModal() {{
            const modal = document.getElementById('requestModal');
            const span = document.getElementsByClassName('close')[0];
            
            span.onclick = function() {{
                modal.style.display = 'none';
            }}
            
            window.onclick = function(event) {{
                if (event.target == modal) {{
                    modal.style.display = 'none';
                }}
            }}
        }}

        function createCharts() {{
            // Check if Chart.js is loaded
            if (typeof Chart === 'undefined') {{
                console.error('Chart.js library not loaded');
                return;
            }}

            // Response Time Distribution Chart
            const responseTimeCanvas = document.getElementById('responseTimeChart');
            if (!responseTimeCanvas) {{
                console.warn('Response time chart canvas not found');
                return;
            }}
            const responseTimesCtx = responseTimeCanvas.getContext('2d');
            const responseTimes = testData.requestResults.map(r => parseTimeSpan(r.responseTime));
            
            // Create bins for histogram (in seconds)
            const bins = [0, 10, 30, 60, 120, 300, Infinity];
            const binLabels = ['0-10s', '10-30s', '30s-1m', '1-2m', '2-5m', '5m+'];
            const binCounts = new Array(bins.length - 1).fill(0);
            
            responseTimes.forEach(time => {{
                for (let i = 0; i < bins.length - 1; i++) {{
                    if (time >= bins[i] && time < bins[i + 1]) {{
                        binCounts[i]++;
                        break;
                    }}
                }}
            }});

            new Chart(responseTimesCtx, {{
                type: 'bar',
                data: {{
                    labels: binLabels,
                    datasets: [{{
                        label: 'Number of Requests',
                        data: binCounts,
                        backgroundColor: 'rgba(52, 152, 219, 0.8)',
                        borderColor: 'rgba(52, 152, 219, 1)',
                        borderWidth: 1
                    }}]
                }},
                options: {{
                    responsive: true,
                    scales: {{
                        y: {{
                            beginAtZero: true,
                            title: {{
                                display: true,
                                text: 'Number of Requests'
                            }}
                        }},
                        x: {{
                            title: {{
                                display: true,
                                text: 'Response Time Range'
                            }}
                        }}
                    }}
                }}
            }});

            // Container Resource Usage Chart
            const containerCanvas = document.getElementById('containerResourceChart');
            if (!containerCanvas) {{
                console.warn('Container resource chart canvas not found');
                return;
            }}
            const containerCtx = containerCanvas.getContext('2d');
            const containerCpuData = testData.requestResults.map(r => r.containerCpuUsage || 0);
            const containerMemoryData = testData.requestResults.map(r => r.containerMemoryUsage || 0);
            const requestLabels = testData.requestResults.map((r, i) => new Date(r.timestamp).toLocaleTimeString());

            new Chart(containerCtx, {{
                type: 'line',
                data: {{
                    labels: requestLabels.length > 50 ? requestLabels.filter((_, i) => i % Math.ceil(requestLabels.length / 50) === 0) : requestLabels,
                    datasets: [{{
                        label: 'Container CPU (%)',
                        data: containerCpuData.length > 50 ? containerCpuData.filter((_, i) => i % Math.ceil(containerCpuData.length / 50) === 0) : containerCpuData,
                        borderColor: 'rgba(231, 76, 60, 1)',
                        backgroundColor: 'rgba(231, 76, 60, 0.1)',
                        tension: 0.4,
                        fill: false
                    }}, {{
                        label: 'Container Memory (MB)',
                        data: containerMemoryData.length > 50 ? containerMemoryData.filter((_, i) => i % Math.ceil(containerMemoryData.length / 50) === 0) : containerMemoryData,
                        borderColor: 'rgba(46, 204, 113, 1)',
                        backgroundColor: 'rgba(46, 204, 113, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y1',
                        fill: false
                    }}]
                }},
                options: {{
                    responsive: true,
                    interaction: {{
                        mode: 'index',
                        intersect: false,
                    }},
                    scales: {{
                        x: {{
                            title: {{
                                display: true,
                                text: 'Time'
                            }}
                        }},
                        y: {{
                            type: 'linear',
                            display: true,
                            position: 'left',
                            title: {{
                                display: true,
                                text: 'CPU Usage (%)'
                            }},
                            min: 0
                        }},
                        y1: {{
                            type: 'linear',
                            display: true,
                            position: 'right',
                            title: {{
                                display: true,
                                text: 'Memory Usage (MB)'
                            }},
                            grid: {{
                                drawOnChartArea: false,
                            }},
                        }}
                    }}
                }}
            }});

            // VM Resource Usage Chart
            const vmCanvas = document.getElementById('vmResourceChart');
            if (!vmCanvas) {{
                console.warn('VM resource chart canvas not found');
                return;
            }}
            const vmCtx = vmCanvas.getContext('2d');
            const vmCpuData = testData.requestResults.map(r => r.vmCpuUsage || 0);
            const vmMemoryData = testData.requestResults.map(r => r.vmMemoryUsage || 0);

            new Chart(vmCtx, {{
                type: 'line',
                data: {{
                    labels: requestLabels.length > 50 ? requestLabels.filter((_, i) => i % Math.ceil(requestLabels.length / 50) === 0) : requestLabels,
                    datasets: [{{
                        label: 'VM CPU (%)',
                        data: vmCpuData.length > 50 ? vmCpuData.filter((_, i) => i % Math.ceil(vmCpuData.length / 50) === 0) : vmCpuData,
                        borderColor: 'rgba(155, 89, 182, 1)',
                        backgroundColor: 'rgba(155, 89, 182, 0.1)',
                        tension: 0.4,
                        fill: false
                    }}, {{
                        label: 'VM Memory (MB)',
                        data: vmMemoryData.length > 50 ? vmMemoryData.filter((_, i) => i % Math.ceil(vmMemoryData.length / 50) === 0) : vmMemoryData,
                        borderColor: 'rgba(243, 156, 18, 1)',
                        backgroundColor: 'rgba(243, 156, 18, 0.1)',
                        tension: 0.4,
                        yAxisID: 'y1',
                        fill: false
                    }}]
                }},
                options: {{
                    responsive: true,
                    interaction: {{
                        mode: 'index',
                        intersect: false,
                    }},
                    scales: {{
                        x: {{
                            title: {{
                                display: true,
                                text: 'Time'
                            }}
                        }},
                        y: {{
                            type: 'linear',
                            display: true,
                            position: 'left',
                            title: {{
                                display: true,
                                text: 'CPU Usage (%)'
                            }},
                            min: 0,
                            max: 1
                        }},
                        y1: {{
                            type: 'linear',
                            display: true,
                            position: 'right',
                            title: {{
                                display: true,
                                text: 'Memory Usage (MB)'
                            }},
                            grid: {{
                                drawOnChartArea: false,
                            }},
                        }}
                    }}
                }}
            }});

            // Individual Request Performance Chart
            const timelineCanvas = document.getElementById('timelineChart');
            if (!timelineCanvas) {{
                console.warn('Timeline chart canvas not found');
                return;
            }}
            const timelineCtx = timelineCanvas.getContext('2d');
            
            // Prepare per-request data
            const requestNumbers = testData.requestResults.map((_, i) => i + 1);
            const individualResponseTimes = testData.requestResults.map(r => parseTimeSpan(r.responseTime));
            const tokensGenerated = testData.requestResults.map(r => r.tokensGenerated || 0);
            
            new Chart(timelineCtx, {{
                type: 'line',
                data: {{
                    labels: requestNumbers.length > 100 ? requestNumbers.filter((_, i) => i % Math.ceil(requestNumbers.length / 100) === 0) : requestNumbers,
                    datasets: [{{
                        label: 'Response Time (seconds)',
                        data: individualResponseTimes.length > 100 ? individualResponseTimes.filter((_, i) => i % Math.ceil(individualResponseTimes.length / 100) === 0) : individualResponseTimes,
                        borderColor: 'rgba(52, 152, 219, 1)',
                        backgroundColor: 'rgba(52, 152, 219, 0.1)',
                        tension: 0.1,
                        fill: false,
                        yAxisID: 'y'
                    }}, {{
                        label: 'Tokens Generated',
                        data: tokensGenerated.length > 100 ? tokensGenerated.filter((_, i) => i % Math.ceil(tokensGenerated.length / 100) === 0) : tokensGenerated,
                        borderColor: 'rgba(46, 204, 113, 1)',
                        backgroundColor: 'rgba(46, 204, 113, 0.1)',
                        tension: 0.1,
                        fill: false,
                        yAxisID: 'y1'
                    }}]
                }},
                options: {{
                    responsive: true,
                    interaction: {{
                        mode: 'index',
                        intersect: false,
                    }},
                    scales: {{
                        x: {{
                            display: true,
                            title: {{
                                display: true,
                                text: 'Request Number'
                            }}
                        }},
                        y: {{
                            type: 'linear',
                            display: true,
                            position: 'left',
                            title: {{
                                display: true,
                                text: 'Response Time (seconds)'
                            }},
                            beginAtZero: true
                        }},
                        y1: {{
                            type: 'linear',
                            display: true,
                            position: 'right',
                            title: {{
                                display: true,
                                text: 'Tokens Generated'
                            }},
                            beginAtZero: true,
                            grid: {{
                                drawOnChartArea: false,
                            }},
                        }}
                    }},
                    plugins: {{
                        tooltip: {{
                            callbacks: {{
                                afterBody: function(context) {{
                                    const index = context[0].dataIndex;
                                    const realIndex = requestNumbers.length > 100 ? 
                                        Math.floor(index * (requestNumbers.length / 100)) : index;
                                    if (realIndex < testData.requestResults.length) {{
                                        const request = testData.requestResults[realIndex];
                                        const responseTime = parseTimeSpan(request.responseTime);
                                        const tokens = request.tokensGenerated || 0;
                                        const success = request.success ? '✅ Success' : '❌ Failed';
                                        return [
                                            `Status: ${{success}}`,
                                            `Response Time: ${{responseTime.toFixed(2)}}s`,
                                            `Tokens Generated: ${{tokens}}`,
                                            `Time: ${{new Date(request.timestamp).toLocaleTimeString()}}`
                                        ];
                                    }}
                                    return [];
                                }}
                            }}
                        }}
                    }}
                }}
            }});

            // Detailed Metrics Chart
            const detailedCanvas = document.getElementById('detailedMetricsChart');
            if (detailedCanvas) {{
                const detailedCtx = detailedCanvas.getContext('2d');
                
                new Chart(detailedCtx, {{
                    type: 'line',
                    data: {{
                        labels: requestLabels,
                        datasets: [{{
                            label: 'Response Time (s)',
                            data: responseTimes,
                            borderColor: 'rgba(52, 152, 219, 1)',
                            backgroundColor: 'rgba(52, 152, 219, 0.1)',
                            tension: 0.4,
                            yAxisID: 'y'
                        }}, {{
                            label: 'Container CPU (%)',
                            data: containerCpuData,
                            borderColor: 'rgba(231, 76, 60, 1)',
                            backgroundColor: 'rgba(231, 76, 60, 0.1)',
                            tension: 0.4,
                            yAxisID: 'y1'
                        }}, {{
                            label: 'VM CPU (%)',
                            data: vmCpuData,
                            borderColor: 'rgba(155, 89, 182, 1)',
                            backgroundColor: 'rgba(155, 89, 182, 0.1)',
                            tension: 0.4,
                            yAxisID: 'y1'
                        }}]
                    }},
                    options: {{
                        responsive: true,
                        interaction: {{
                            mode: 'index',
                            intersect: false,
                        }},
                        scales: {{
                            x: {{
                                title: {{
                                    display: true,
                                    text: 'Time'
                                }}
                            }},
                            y: {{
                                type: 'linear',
                                display: true,
                                position: 'left',
                                title: {{
                                    display: true,
                                    text: 'Response Time (seconds)'
                                }}
                            }},
                            y1: {{
                                type: 'linear',
                                display: true,
                                position: 'right',
                                title: {{
                                    display: true,
                                    text: 'CPU Usage (%)'
                                }},
                                grid: {{
                                    drawOnChartArea: false,
                                }},
                            }}
                        }}
                    }}
                }});
            }} else {{
                console.warn('Detailed metrics chart canvas not found');
            }}
        }}
    </script>
</body>
</html>";
        }
    }
}

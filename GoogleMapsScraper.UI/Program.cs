using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoogleMapsScraper.Core.Interfaces;
using GoogleMapsScraper.Core.Models;
using GoogleMapsScraper.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoogleMapsScraper.UI
{
    class Program
    {
        private static IScraper _scraper;
        private static IDataExporter _exporter;
        private static ScraperSettings _settings;
        private static ILogger<Program> _logger;
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("Google Maps Scraper Tool");
            Console.WriteLine("=======================");
            
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            
            var serviceProvider = services.BuildServiceProvider();
            
            // Get services
            _logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            _exporter = serviceProvider.GetRequiredService<IDataExporter>();
            
            // Register Ctrl+C handler
            Console.CancelKeyPress += (sender, e) => 
            {
                e.Cancel = true;
                _cts.Cancel();
                Console.WriteLine("\nCancellation requested. Stopping scraper...");
            };

            try
            {
                // Configure settings
                _settings = await ConfigureScraperSettingsAsync();
                
                // Initialize scraper
                _logger.LogInformation("Initializing scraper...");
                _scraper = serviceProvider.GetRequiredService<IScraper>();
                
                var initResult = await _scraper.InitializeAsync(_settings);
                if (!initResult)
                {
                    _logger.LogError("Failed to initialize scraper");
                    Console.WriteLine("Failed to initialize scraper. Check logs for details.");
                    return;
                }
                
                // Start scraping
                Console.WriteLine("\nStarting scraper...");
                Console.WriteLine($"Searching for: {_settings.SearchKeyword} in {_settings.GeographicArea}");
                Console.WriteLine("Press Ctrl+C to cancel at any time.\n");
                
                var progress = new Progress<ScrapeProgress>(ReportProgress);
                var businesses = await _scraper.ScrapeAsync(progress, _cts.Token);
                
                // Export results
                if (businesses.Any())
                {
                    await ExportResultsAsync(businesses.ToList());
                }
                else
                {
                    Console.WriteLine("No businesses were found or scraping was cancelled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scraper application");
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                _scraper?.Dispose();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // Add logging
            services.AddLogging(configure => 
            {
                configure.AddConsole();
                configure.AddDebug();
                configure.SetMinimumLevel(LogLevel.Information);
            });
            
            // Add services
            services.AddTransient<IScraper, Core.Services.GoogleMapsScraper>();
            services.AddTransient<IDataExporter, DataExporter>();
            services.AddTransient<ICaptchaSolver, TwoCaptchaSolver>();
        }

        private static async Task<ScraperSettings> ConfigureScraperSettingsAsync()
        {
            var settings = new ScraperSettings();
            
            Console.Write("Enter search keyword (e.g., 'restaurants', 'hotels', etc.): ");
            settings.SearchKeyword = Console.ReadLine();
            
            Console.Write("Enter geographic area (e.g., 'New York, NY', 'Chicago', etc.): ");
            settings.GeographicArea = Console.ReadLine();
            
            Console.Write("Maximum number of results to scrape (default: 100): ");
            var maxResultsInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(maxResultsInput) && int.TryParse(maxResultsInput, out int maxResults))
            {
                settings.MaxResults = maxResults;
            }
            
            Console.Write("Only scrape businesses with phone numbers? (y/n, default: n): ");
            var phoneOnlyInput = Console.ReadLine()?.ToLower();
            settings.OnlyBusinessesWithPhoneNumbers = phoneOnlyInput == "y" || phoneOnlyInput == "yes";
            
            Console.Write("Minimum delay between requests in ms (default: 1000): ");
            var minDelayInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(minDelayInput) && int.TryParse(minDelayInput, out int minDelay))
            {
                settings.MinDelayBetweenRequests = minDelay;
            }
            
            Console.Write("Maximum delay between requests in ms (default: 3000): ");
            var maxDelayInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(maxDelayInput) && int.TryParse(maxDelayInput, out int maxDelay))
            {
                settings.MaxDelayBetweenRequests = maxDelay;
            }
            
            Console.Write("Use proxy? (y/n, default: n): ");
            var useProxyInput = Console.ReadLine()?.ToLower();
            if (useProxyInput == "y" || useProxyInput == "yes")
            {
                settings.UseProxyRotation = true;
                
                Console.WriteLine("Enter proxy details (leave blank to finish):");
                while (true)
                {
                    Console.Write("Proxy host (e.g., '123.45.67.89'): ");
                    var host = Console.ReadLine();
                    if (string.IsNullOrEmpty(host))
                        break;
                    
                    Console.Write("Proxy port (e.g., '8080'): ");
                    var portInput = Console.ReadLine();
                    if (!int.TryParse(portInput, out int port))
                    {
                        Console.WriteLine("Invalid port. Using default port 8080.");
                        port = 8080;
                    }
                    
                    Console.Write("Username (optional): ");
                    var username = Console.ReadLine();
                    
                    Console.Write("Password (optional): ");
                    var password = Console.ReadLine();
                    
                    settings.Proxies.Add(new ProxySettings
                    {
                        Host = host,
                        Port = port,
                        Username = username,
                        Password = password,
                        IsActive = true
                    });
                    
                    Console.WriteLine("Proxy added. Enter another proxy or leave blank to continue.");
                }
            }
            
            Console.Write("Use CAPTCHA solver? (y/n, default: n): ");
            var useCaptchaInput = Console.ReadLine()?.ToLower();
            if (useCaptchaInput == "y" || useCaptchaInput == "yes")
            {
                Console.Write("Enter 2Captcha API key: ");
                settings.CaptchaSolverApiKey = Console.ReadLine();
                settings.CaptchaSolverUrl = "https://2captcha.com/";
            }
            
            return settings;
        }

        private static void ReportProgress(ScrapeProgress progress)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.SetCursorPosition(0, Console.CursorTop);
            
            // Display progress
            if (progress.BusinessesFound > 0)
            {
                Console.Write($"Progress: {progress.BusinessesProcessed}/{progress.BusinessesFound} ({progress.PercentComplete:F1}%) - ");
            }
            
            Console.Write(progress.StatusMessage);
            
            if (progress.CaptchaDetected)
            {
                Console.Write(" [CAPTCHA detected" + (progress.CaptchaSolved ? ", solved]" : ", solving...]"));
            }
            
            if (!string.IsNullOrEmpty(progress.ErrorMessage))
            {
                Console.Write($" Error: {progress.ErrorMessage}");
            }
        }

        private static async Task ExportResultsAsync(List<BusinessData> businesses)
        {
            Console.WriteLine($"\n\nScraped {businesses.Count} businesses.");
            Console.WriteLine("Export options:");
            Console.WriteLine("1. CSV");
            Console.WriteLine("2. JSON");
            Console.WriteLine("3. Excel");
            Console.WriteLine("4. All formats");
            Console.WriteLine("5. Cancel");
            
            Console.Write("\nSelect option (1-5): ");
            var option = Console.ReadLine();
            
            if (option == "5")
                return;
            
            // Create export directory
            var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "exports");
            Directory.CreateDirectory(exportDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filenameBase = Path.Combine(exportDir, $"businesses_{timestamp}");
            
            if (option == "1" || option == "4")
            {
                var csvPath = $"{filenameBase}.csv";
                Console.Write($"Exporting to CSV: {csvPath}... ");
                await _exporter.ExportToCsvAsync(businesses, csvPath);
                Console.WriteLine("Done!");
            }
            
            if (option == "2" || option == "4")
            {
                var jsonPath = $"{filenameBase}.json";
                Console.Write($"Exporting to JSON: {jsonPath}... ");
                await _exporter.ExportToJsonAsync(businesses, jsonPath);
                Console.WriteLine("Done!");
            }
            
            if (option == "3" || option == "4")
            {
                var excelPath = $"{filenameBase}.xlsx";
                Console.Write($"Exporting to Excel: {excelPath}... ");
                await _exporter.ExportToExcelAsync(businesses, excelPath);
                Console.WriteLine("Done!");
            }
            
            Console.WriteLine($"\nExport completed. Files saved to: {exportDir}");
        }
    }
}

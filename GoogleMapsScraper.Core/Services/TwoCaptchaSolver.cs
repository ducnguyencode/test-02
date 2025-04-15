using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GoogleMapsScraper.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GoogleMapsScraper.Core.Services
{
    /// <summary>
    /// Implementation of ICaptchaSolver using the 2Captcha service
    /// </summary>
    public class TwoCaptchaSolver : ICaptchaSolver
    {
        private readonly ILogger<TwoCaptchaSolver> _logger;
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _serviceUrl = "https://2captcha.com/";

        /// <summary>
        /// Creates a new instance of the TwoCaptchaSolver class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public TwoCaptchaSolver(ILogger<TwoCaptchaSolver> logger = null)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        /// <inheritdoc/>
        public string ServiceName => "2Captcha";

        /// <inheritdoc/>
        public async Task<bool> InitializeAsync(string apiKey, string serviceUrl = null)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty");
                }

                _apiKey = apiKey;
                
                if (!string.IsNullOrEmpty(serviceUrl))
                {
                    _serviceUrl = serviceUrl.EndsWith("/") ? serviceUrl : serviceUrl + "/";
                }

                // Test the API key by checking balance
                var balance = await GetBalanceAsync();
                _logger?.LogInformation($"2Captcha service initialized with API key. Current balance: {balance}");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize 2Captcha service: {ex.Message}");
                throw new CaptchaSolverException($"Failed to initialize 2Captcha service: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<decimal> GetBalanceAsync()
        {
            try
            {
                var url = $"{_serviceUrl}res.php?key={_apiKey}&action=getbalance&json=1";
                var response = await _httpClient.GetStringAsync(url);
                
                var jsonResponse = JObject.Parse(response);
                if (jsonResponse["status"].ToString() == "1")
                {
                    return decimal.Parse(jsonResponse["request"].ToString());
                }
                
                throw new CaptchaSolverException($"Failed to get balance: {jsonResponse["request"]}");
            }
            catch (Exception ex) when (!(ex is CaptchaSolverException))
            {
                _logger?.LogError(ex, $"Error getting 2Captcha balance: {ex.Message}");
                throw new CaptchaSolverException($"Error getting 2Captcha balance: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<string> SolveImageCaptchaAsync(string captchaImageData, int timeout = 60000)
        {
            CheckInitialized();
            
            try
            {
                _logger?.LogInformation("Sending image CAPTCHA to 2Captcha service");
                
                // Step 1: Send the captcha to 2Captcha
                var submitUrl = $"{_serviceUrl}in.php";
                var submitContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("key", _apiKey),
                    new KeyValuePair<string, string>("method", "base64"),
                    new KeyValuePair<string, string>("body", captchaImageData),
                    new KeyValuePair<string, string>("json", "1")
                });
                
                var submitResponse = await _httpClient.PostAsync(submitUrl, submitContent);
                var submitResult = await submitResponse.Content.ReadAsStringAsync();
                
                var jsonSubmitResult = JObject.Parse(submitResult);
                if (jsonSubmitResult["status"].ToString() != "1")
                {
                    throw new CaptchaSolverException($"Failed to submit CAPTCHA: {jsonSubmitResult["request"]}");
                }
                
                var captchaId = jsonSubmitResult["request"].ToString();
                _logger?.LogInformation($"CAPTCHA submitted successfully. ID: {captchaId}");
                
                // Step 2: Wait for the solution
                var resultUrl = $"{_serviceUrl}res.php?key={_apiKey}&action=get&id={captchaId}&json=1";
                var startTime = DateTime.UtcNow;
                
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
                {
                    // Wait for 5 seconds before checking
                    await Task.Delay(5000);
                    
                    var resultResponse = await _httpClient.GetStringAsync(resultUrl);
                    var jsonResultResponse = JObject.Parse(resultResponse);
                    
                    if (jsonResultResponse["status"].ToString() == "1")
                    {
                        var solution = jsonResultResponse["request"].ToString();
                        _logger?.LogInformation("CAPTCHA solved successfully");
                        return solution;
                    }
                    
                    if (jsonResultResponse["request"].ToString() != "CAPCHA_NOT_READY")
                    {
                        throw new CaptchaSolverException($"Failed to solve CAPTCHA: {jsonResultResponse["request"]}");
                    }
                    
                    _logger?.LogInformation("CAPTCHA not ready yet, waiting...");
                }
                
                throw new CaptchaSolverException($"CAPTCHA solving timed out after {timeout}ms");
            }
            catch (Exception ex) when (!(ex is CaptchaSolverException))
            {
                _logger?.LogError(ex, $"Error solving image CAPTCHA: {ex.Message}");
                throw new CaptchaSolverException($"Error solving image CAPTCHA: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<string> SolveRecaptchaV2Async(string siteKey, string siteUrl, int timeout = 180000)
        {
            CheckInitialized();
            
            try
            {
                _logger?.LogInformation($"Sending reCAPTCHA v2 request to 2Captcha service. Site: {siteUrl}");
                
                // Step 1: Send the reCAPTCHA request to 2Captcha
                var submitUrl = $"{_serviceUrl}in.php";
                var submitContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("key", _apiKey),
                    new KeyValuePair<string, string>("method", "userrecaptcha"),
                    new KeyValuePair<string, string>("googlekey", siteKey),
                    new KeyValuePair<string, string>("pageurl", siteUrl),
                    new KeyValuePair<string, string>("json", "1")
                });
                
                var submitResponse = await _httpClient.PostAsync(submitUrl, submitContent);
                var submitResult = await submitResponse.Content.ReadAsStringAsync();
                
                var jsonSubmitResult = JObject.Parse(submitResult);
                if (jsonSubmitResult["status"].ToString() != "1")
                {
                    throw new CaptchaSolverException($"Failed to submit reCAPTCHA: {jsonSubmitResult["request"]}");
                }
                
                var captchaId = jsonSubmitResult["request"].ToString();
                _logger?.LogInformation($"reCAPTCHA submitted successfully. ID: {captchaId}");
                
                // Step 2: Wait for the solution
                var resultUrl = $"{_serviceUrl}res.php?key={_apiKey}&action=get&id={captchaId}&json=1";
                var startTime = DateTime.UtcNow;
                
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
                {
                    // Wait for 5 seconds before checking
                    await Task.Delay(5000);
                    
                    var resultResponse = await _httpClient.GetStringAsync(resultUrl);
                    var jsonResultResponse = JObject.Parse(resultResponse);
                    
                    if (jsonResultResponse["status"].ToString() == "1")
                    {
                        var solution = jsonResultResponse["request"].ToString();
                        _logger?.LogInformation("reCAPTCHA solved successfully");
                        return solution;
                    }
                    
                    if (jsonResultResponse["request"].ToString() != "CAPCHA_NOT_READY")
                    {
                        throw new CaptchaSolverException($"Failed to solve reCAPTCHA: {jsonResultResponse["request"]}");
                    }
                    
                    _logger?.LogInformation("reCAPTCHA not ready yet, waiting...");
                }
                
                throw new CaptchaSolverException($"reCAPTCHA solving timed out after {timeout}ms");
            }
            catch (Exception ex) when (!(ex is CaptchaSolverException))
            {
                _logger?.LogError(ex, $"Error solving reCAPTCHA v2: {ex.Message}");
                throw new CaptchaSolverException($"Error solving reCAPTCHA v2: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if the service is initialized with an API key
        /// </summary>
        private void CheckInitialized()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new CaptchaSolverException("2Captcha service is not initialized. Call InitializeAsync first.");
            }
        }
    }
} 
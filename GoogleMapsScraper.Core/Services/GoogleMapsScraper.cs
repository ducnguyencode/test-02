using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GoogleMapsScraper.Core.Interfaces;
using GoogleMapsScraper.Core.Models;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace GoogleMapsScraper.Core.Services
{
    /// <summary>
    /// Implementation of the IScraper interface for Google Maps
    /// </summary>
    public class GoogleMapsScraper : IScraper
    {
        private readonly ILogger<GoogleMapsScraper> _logger;
        private readonly ICaptchaSolver _captchaSolver;
        private ScraperSettings _settings;
        private IWebDriver _driver;
        private List<BusinessData> _scrapedBusinesses;
        private ScraperStatus _status;
        private CancellationTokenSource _pauseCts;
        private Random _random;
        private int _currentProxyIndex;
        private string _googleMapsUrl = "https://www.google.com/maps";
        
        /// <summary>
        /// Creates a new instance of the GoogleMapsScraper class
        /// </summary>
        /// <param name="captchaSolver">The CAPTCHA solver service</param>
        /// <param name="logger">The logger instance</param>
        public GoogleMapsScraper(ICaptchaSolver captchaSolver = null, ILogger<GoogleMapsScraper> logger = null)
        {
            _captchaSolver = captchaSolver;
            _logger = logger;
            _status = ScraperStatus.NotInitialized;
            _scrapedBusinesses = new List<BusinessData>();
            _random = new Random();
            _currentProxyIndex = 0;
        }

        /// <inheritdoc/>
        public async Task<bool> InitializeAsync(ScraperSettings settings)
        {
            try
            {
                _logger?.LogInformation("Initializing Google Maps scraper");
                
                // Store the settings
                _settings = settings ?? throw new ArgumentNullException(nameof(settings));
                
                // Validate essential settings
                if (string.IsNullOrEmpty(settings.SearchKeyword))
                {
                    throw new ArgumentException("Search keyword cannot be empty", nameof(settings.SearchKeyword));
                }
                
                // Setup chrome driver
                await InitializeWebDriverAsync();
                
                _status = ScraperStatus.Ready;
                _logger?.LogInformation("Google Maps scraper initialized successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize Google Maps scraper: {ex.Message}");
                _status = ScraperStatus.Error;
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<BusinessData>> ScrapeAsync(IProgress<ScrapeProgress> progress = null, CancellationToken cancellationToken = default)
        {
            if (_status != ScraperStatus.Ready && _status != ScraperStatus.Paused)
            {
                throw new InvalidOperationException($"Scraper is not ready. Current status: {_status}");
            }
            
            try
            {
                _status = ScraperStatus.Running;
                _pauseCts = new CancellationTokenSource();
                _scrapedBusinesses.Clear();
                
                _logger?.LogInformation($"Starting scraping with keyword: '{_settings.SearchKeyword}' in area: '{_settings.GeographicArea}'");
                
                // Navigate to Google Maps
                _driver.Navigate().GoToUrl(_googleMapsUrl);
                await RandomDelayAsync();

                // Search for the business type + location
                var query = _settings.GeographicArea != null 
                    ? $"{_settings.SearchKeyword} in {_settings.GeographicArea}" 
                    : _settings.SearchKeyword;
                
                // Initialize progress
                var progressData = new ScrapeProgress
                {
                    StatusMessage = $"Searching for: {query}"
                };
                progress?.Report(progressData);
                
                // Search
                await SearchAsync(query);
                await RandomDelayAsync();
                
                // Check if Google Maps redirected to a specific result (for known places)
                if (IsSpecificBusinessPage())
                {
                    _logger?.LogInformation("Google Maps redirected to a specific business page");
                    var business = await ScrapeBusinessDetailsAsync();
                    if (business != null)
                    {
                        _scrapedBusinesses.Add(business);
                    }
                }
                else
                {
                    // Get the list of results
                    var businessLinks = await GetSearchResultsAsync();
                    var totalResults = businessLinks.Count;
                    
                    _logger?.LogInformation($"Found {totalResults} search results");
                    
                    progressData.BusinessesFound = totalResults;
                    progressData.StatusMessage = $"Found {totalResults} results";
                    progress?.Report(progressData);
                    
                    // Limit the number of results to scrape
                    if (totalResults > _settings.MaxResults)
                    {
                        businessLinks = businessLinks.Take(_settings.MaxResults).ToList();
                        _logger?.LogInformation($"Limiting to {_settings.MaxResults} results");
                    }
                    
                    // Process each business
                    int processedCount = 0;
                    foreach (var link in businessLinks)
                    {
                        // Check for cancellation
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger?.LogInformation("Scraping was cancelled");
                            _status = ScraperStatus.Cancelled;
                            break;
                        }
                        
                        // Check for pause
                        if (_pauseCts.IsCancellationRequested)
                        {
                            _logger?.LogInformation("Scraping was paused");
                            _status = ScraperStatus.Paused;
                            
                            // Wait for resume
                            while (_status == ScraperStatus.Paused && !cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(500);
                            }
                            
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger?.LogInformation("Scraping was cancelled while paused");
                                _status = ScraperStatus.Cancelled;
                                break;
                            }
                            
                            _pauseCts = new CancellationTokenSource();
                        }
                        
                        try
                        {
                            // Click on the result to view details
                            await ClickElementAsync(link);
                            await RandomDelayAsync();
                            
                            // Scrape business details
                            var business = await ScrapeBusinessDetailsAsync();
                            
                            // Check if we should only save businesses with phone numbers
                            if (_settings.OnlyBusinessesWithPhoneNumbers && string.IsNullOrEmpty(business?.PhoneNumber))
                            {
                                _logger?.LogInformation($"Skipping business '{business?.Name}' - no phone number");
                            }
                            else if (business != null)
                            {
                                _scrapedBusinesses.Add(business);
                                _logger?.LogInformation($"Scraped business: {business.Name}");
                            }
                            
                            processedCount++;
                            progressData.BusinessesProcessed = processedCount;
                            progressData.PercentComplete = (double)processedCount / businessLinks.Count * 100;
                            progressData.StatusMessage = $"Processed {processedCount} of {businessLinks.Count} businesses";
                            progress?.Report(progressData);
                            
                            // Go back to search results
                            _driver.Navigate().Back();
                            await RandomDelayAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error processing business: {ex.Message}");
                            progressData.ErrorMessage = ex.Message;
                            progress?.Report(progressData);
                            
                            // Check for CAPTCHA
                            if (IsCaptchaPresent())
                            {
                                progressData.CaptchaDetected = true;
                                progress?.Report(progressData);
                                
                                _logger?.LogWarning("CAPTCHA detected, attempting to solve");
                                
                                if (await HandleCaptchaAsync())
                                {
                                    progressData.CaptchaSolved = true;
                                    progressData.ErrorMessage = null;
                                    progress?.Report(progressData);
                                    
                                    // Retry the current business
                                    _logger?.LogInformation("CAPTCHA solved, retrying current business");
                                    continue;
                                }
                                else
                                {
                                    _logger?.LogError("Failed to solve CAPTCHA, changing proxy and retrying");
                                    
                                    // Change proxy if available
                                    if (_settings.UseProxyRotation && _settings.Proxies.Count > 1)
                                    {
                                        await RotateProxyAsync();
                                        progressData.StatusMessage = "Changed proxy, retrying";
                                        progress?.Report(progressData);
                                        
                                        // Retry the current business
                                        continue;
                                    }
                                }
                            }
                            
                            // Try to continue with the next business
                            try
                            {
                                _driver.Navigate().Back();
                                await RandomDelayAsync();
                            }
                            catch { /* Ignore any error when going back */ }
                        }
                    }
                }
                
                // Set status based on completion
                _status = cancellationToken.IsCancellationRequested ? ScraperStatus.Cancelled : ScraperStatus.Completed;
                
                progressData.StatusMessage = $"Scraping completed. Extracted {_scrapedBusinesses.Count} businesses.";
                progressData.PercentComplete = 100;
                progress?.Report(progressData);
                
                _logger?.LogInformation($"Scraping completed. Scraped {_scrapedBusinesses.Count} businesses");
                
                return _scrapedBusinesses;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error during scraping: {ex.Message}");
                _status = ScraperStatus.Error;
                
                var progressData = new ScrapeProgress
                {
                    StatusMessage = "Scraping failed",
                    ErrorMessage = ex.Message
                };
                progress?.Report(progressData);
                
                return _scrapedBusinesses;
            }
        }

        /// <inheritdoc/>
        public Task<bool> PauseAsync()
        {
            if (_status != ScraperStatus.Running)
            {
                return Task.FromResult(false);
            }
            
            _logger?.LogInformation("Pausing scraper");
            _pauseCts.Cancel();
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<bool> ResumeAsync()
        {
            if (_status != ScraperStatus.Paused)
            {
                return Task.FromResult(false);
            }
            
            _logger?.LogInformation("Resuming scraper");
            _status = ScraperStatus.Running;
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public ScraperStatus GetStatus()
        {
            return _status;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _logger?.LogInformation("Disposing Google Maps scraper");
            _driver?.Quit();
            _driver?.Dispose();
            _driver = null;
        }

        #region Private Methods

        /// <summary>
        /// Initialize the Selenium WebDriver
        /// </summary>
        private async Task InitializeWebDriverAsync()
        {
            var options = new ChromeOptions();
            
            // Add arguments to the Chrome options
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-extensions");
            
            // Set user agent
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            // Configure proxy if needed
            if (_settings.UseProxyRotation && _settings.Proxies.Count > 0)
            {
                var proxy = _settings.Proxies[_currentProxyIndex];
                _logger?.LogInformation($"Using proxy: {proxy.Host}:{proxy.Port}");
                
                options.AddArgument($"--proxy-server={proxy.Address}");
                
                if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
                {
                    // For authenticated proxies, we need to use a proxy extension
                    // This would need to be implemented for Chrome extensions
                }
            }
            
            // Add any extra options
            options.AddArgument("--lang=en-US");
            
            // Initialize Chrome driver
            _driver = new ChromeDriver(options);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromMilliseconds(_settings.RequestTimeoutMs);
        }

        /// <summary>
        /// Perform a search on Google Maps
        /// </summary>
        /// <param name="query">The search query</param>
        private async Task SearchAsync(string query)
        {
            try
            {
                _logger?.LogInformation($"Searching for: {query}");
                
                // Find the search box
                var searchBox = WaitForElement(By.Name("q"));
                if (searchBox == null)
                {
                    searchBox = WaitForElement(By.XPath("//input[@aria-label='Search Google Maps']"));
                }
                
                // Clear the search box and enter the query
                searchBox.Clear();
                searchBox.SendKeys(query);
                await RandomDelayAsync(500, 1000);
                searchBox.SendKeys(Keys.Enter);
                
                // Wait for results to load
                await RandomDelayAsync(2000, 4000);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error during search: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if the current page is a specific business page
        /// </summary>
        /// <returns>True if the current page is a specific business page</returns>
        private bool IsSpecificBusinessPage()
        {
            try
            {
                // Check if the page has business details elements
                var nameElement = _driver.FindElements(By.XPath("//h1[contains(@class, 'fontHeadlineLarge')]"));
                var addressElement = _driver.FindElements(By.XPath("//button[contains(@aria-label, 'Address:')]"));
                
                return nameElement.Count > 0 && addressElement.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the search results from Google Maps
        /// </summary>
        /// <returns>A list of web elements representing the search results</returns>
        private async Task<List<IWebElement>> GetSearchResultsAsync()
        {
            _logger?.LogInformation("Getting search results");
            
            // Wait for the results to load
            await RandomDelayAsync(2000, 4000);
            
            int scrollAttempts = 0;
            int previousResultCount = 0;
            int sameCountIterations = 0;
            List<IWebElement> results = new List<IWebElement>();
            
            // Try to scroll through all results
            while (scrollAttempts < 20 && (results.Count < _settings.MaxResults || _settings.MaxResults <= 0))
            {
                // Get current results
                results = _driver.FindElements(By.XPath("//a[contains(@href, '/maps/place/')]")).ToList();
                
                if (results.Count == previousResultCount)
                {
                    sameCountIterations++;
                    if (sameCountIterations >= 3)
                    {
                        // If the count hasn't changed in 3 iterations, we're probably at the end
                        break;
                    }
                }
                else
                {
                    sameCountIterations = 0;
                    previousResultCount = results.Count;
                }
                
                _logger?.LogInformation($"Found {results.Count} results so far");
                
                // Scroll to load more results
                try
                {
                    var resultList = _driver.FindElement(By.XPath("//div[contains(@role, 'feed')]"));
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollTop = arguments[0].scrollHeight", resultList);
                }
                catch
                {
                    // Try a different method if the first one fails
                    try
                    {
                        ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");
                    }
                    catch
                    {
                        // If both scroll methods fail, continue with the results we have
                        break;
                    }
                }
                
                scrollAttempts++;
                await RandomDelayAsync(1000, 2000);
            }
            
            return results;
        }

        /// <summary>
        /// Scrape the details of a business from the current page
        /// </summary>
        /// <returns>The business data</returns>
        private async Task<BusinessData> ScrapeBusinessDetailsAsync()
        {
            try
            {
                _logger?.LogInformation("Scraping business details");
                
                var business = new BusinessData();
                
                // Get name
                var nameElement = WaitForElement(By.XPath("//h1[contains(@class, 'fontHeadlineLarge')]"));
                if (nameElement != null)
                {
                    business.Name = nameElement.Text;
                }
                
                // Get address
                var addressElement = _driver.FindElements(By.XPath("//button[contains(@aria-label, 'Address:')]")).FirstOrDefault();
                if (addressElement != null)
                {
                    business.Address = addressElement.GetAttribute("aria-label").Replace("Address: ", "");
                }
                
                // Get phone number
                var phoneElement = _driver.FindElements(By.XPath("//button[contains(@aria-label, 'Phone:')]")).FirstOrDefault();
                if (phoneElement != null)
                {
                    business.PhoneNumber = phoneElement.GetAttribute("aria-label").Replace("Phone: ", "");
                }
                
                // Get website
                var websiteElement = _driver.FindElements(By.XPath("//a[contains(@aria-label, 'Website:')]")).FirstOrDefault();
                if (websiteElement != null)
                {
                    business.Website = websiteElement.GetAttribute("href");
                }
                
                // Get rating
                var ratingElement = _driver.FindElements(By.XPath("//div[@aria-label[contains(., 'stars')]]")).FirstOrDefault();
                if (ratingElement != null)
                {
                    var ratingText = ratingElement.GetAttribute("aria-label");
                    var ratingMatch = Regex.Match(ratingText, @"(\d+(?:\.\d+)?)");
                    if (ratingMatch.Success)
                    {
                        business.Rating = double.Parse(ratingMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                }
                
                // Get review count
                var reviewCountElement = _driver.FindElements(By.XPath("//span[contains(text(), 'reviews')]")).FirstOrDefault();
                if (reviewCountElement != null)
                {
                    var reviewText = reviewCountElement.Text;
                    var reviewMatch = Regex.Match(reviewText, @"(\d+(?:,\d+)*)");
                    if (reviewMatch.Success)
                    {
                        business.ReviewCount = int.Parse(reviewMatch.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture);
                    }
                }
                
                // Get categories
                var categoryElements = _driver.FindElements(By.XPath("//button[contains(@jsaction, 'pane.rating.category')]"));
                foreach (var categoryElement in categoryElements)
                {
                    business.Categories.Add(categoryElement.Text);
                }
                
                // Get coordinates from URL
                var currentUrl = _driver.Url;
                var coordMatch = Regex.Match(currentUrl, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
                if (coordMatch.Success)
                {
                    business.Latitude = double.Parse(coordMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    business.Longitude = double.Parse(coordMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                }
                
                // Get operating hours
                await ExpandHoursAsync();
                var hoursElements = _driver.FindElements(By.XPath("//tr[contains(@class, 'reverse-hours-row')]"));
                foreach (var hoursElement in hoursElements)
                {
                    var dayElement = hoursElement.FindElements(By.XPath(".//td[1]")).FirstOrDefault();
                    var timeElement = hoursElement.FindElements(By.XPath(".//td[2]")).FirstOrDefault();
                    
                    if (dayElement != null && timeElement != null)
                    {
                        var day = dayElement.Text.Trim();
                        var time = timeElement.Text.Trim();
                        
                        if (!string.IsNullOrEmpty(day) && !string.IsNullOrEmpty(time))
                        {
                            business.OperatingHours[day] = time;
                        }
                    }
                }
                
                // Get additional details
                var detailsElements = _driver.FindElements(By.XPath("//div[contains(@class, 'mLhM4')]"));
                foreach (var detailElement in detailsElements)
                {
                    var key = detailElement.FindElements(By.XPath(".//div[1]")).FirstOrDefault()?.Text.Trim();
                    var value = detailElement.FindElements(By.XPath(".//div[2]")).FirstOrDefault()?.Text.Trim();
                    
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    {
                        business.AdditionalDetails[key] = value;
                    }
                }
                
                _logger?.LogInformation($"Successfully scraped details for business: {business.Name}");
                return business;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error scraping business details: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Click on an element and wait for it to be clickable
        /// </summary>
        /// <param name="element">The element to click</param>
        private async Task ClickElementAsync(IWebElement element)
        {
            try
            {
                // Scroll to the element
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true);", element);
                await RandomDelayAsync(500, 1000);
                
                // Click the element
                element.Click();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error clicking element: {ex.Message}");
                
                // Try JavaScript click if direct click fails
                try
                {
                    ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", element);
                }
                catch (Exception jsEx)
                {
                    _logger?.LogError(jsEx, $"Error clicking element with JavaScript: {jsEx.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Expand the operating hours section
        /// </summary>
        private async Task ExpandHoursAsync()
        {
            try
            {
                var hoursButton = _driver.FindElements(By.XPath("//button[contains(@aria-label, 'Hours')]")).FirstOrDefault();
                if (hoursButton != null)
                {
                    await ClickElementAsync(hoursButton);
                    await RandomDelayAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error expanding hours: {ex.Message}");
            }
        }

        /// <summary>
        /// Wait for an element to be present and return it
        /// </summary>
        /// <param name="by">The locator to find the element</param>
        /// <param name="timeoutSeconds">Timeout in seconds</param>
        /// <returns>The found element or null if not found</returns>
        private IWebElement WaitForElement(By by, int timeoutSeconds = 10)
        {
            try
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
                return wait.Until(d => d.FindElement(by));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a CAPTCHA is present on the page
        /// </summary>
        /// <returns>True if a CAPTCHA is detected</returns>
        private bool IsCaptchaPresent()
        {
            try
            {
                // Check for common CAPTCHA indicators
                return _driver.FindElements(By.XPath("//form[contains(@action, 'captcha')]")).Count > 0 ||
                       _driver.FindElements(By.XPath("//*[contains(text(), 'captcha')]")).Count > 0 ||
                       _driver.FindElements(By.XPath("//*[contains(text(), 'CAPTCHA')]")).Count > 0 ||
                       _driver.FindElements(By.XPath("//iframe[contains(@src, 'recaptcha')]")).Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Handle a CAPTCHA challenge
        /// </summary>
        /// <returns>True if the CAPTCHA was solved successfully</returns>
        private async Task<bool> HandleCaptchaAsync()
        {
            if (_captchaSolver == null)
            {
                _logger?.LogWarning("No CAPTCHA solver available");
                return false;
            }
            
            try
            {
                _logger?.LogInformation("Attempting to solve CAPTCHA");
                
                // Check for reCAPTCHA v2
                var recaptchaFrames = _driver.FindElements(By.XPath("//iframe[contains(@src, 'recaptcha')]"));
                if (recaptchaFrames.Count > 0)
                {
                    // Switch to the reCAPTCHA iframe
                    _driver.SwitchTo().Frame(recaptchaFrames[0]);
                    
                    // Find the sitekey
                    var siteKey = recaptchaFrames[0].GetAttribute("src").Split("k=")[1].Split("&")[0];
                    _logger?.LogInformation($"Found reCAPTCHA with site key: {siteKey}");
                    
                    // Get current URL
                    var currentUrl = _driver.Url;
                    
                    // Switch back to the main content
                    _driver.SwitchTo().DefaultContent();
                    
                    // Solve the reCAPTCHA
                    var token = await _captchaSolver.SolveRecaptchaV2Async(siteKey, currentUrl);
                    
                    // Execute the callback function to set the token
                    ((IJavaScriptExecutor)_driver).ExecuteScript($"document.getElementById('g-recaptcha-response').innerHTML='{token}'");
                    
                    // Submit the form
                    var submit = _driver.FindElement(By.XPath("//button[@type='submit']"));
                    submit.Click();
                    
                    await RandomDelayAsync(2000, 4000);
                    
                    return !IsCaptchaPresent();
                }
                else
                {
                    // Handle image CAPTCHA
                    var captchaImg = _driver.FindElement(By.XPath("//img[contains(@src, 'captcha')]"));
                    if (captchaImg != null)
                    {
                        var captchaUrl = captchaImg.GetAttribute("src");
                        
                        // Download the CAPTCHA image
                        using (var client = new WebClient())
                        {
                            var imageData = client.DownloadData(captchaUrl);
                            var base64Image = Convert.ToBase64String(imageData);
                            
                            // Solve the CAPTCHA
                            var solution = await _captchaSolver.SolveImageCaptchaAsync(base64Image);
                            
                            // Find the input field and submit
                            var captchaInput = _driver.FindElement(By.XPath("//input[@name='captcha']"));
                            captchaInput.Clear();
                            captchaInput.SendKeys(solution);
                            
                            var submit = _driver.FindElement(By.XPath("//input[@type='submit']"));
                            submit.Click();
                            
                            await RandomDelayAsync(2000, 4000);
                            
                            return !IsCaptchaPresent();
                        }
                    }
                }
                
                // No recognized CAPTCHA format
                _logger?.LogWarning("Unrecognized CAPTCHA format");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error handling CAPTCHA: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Introduce a random delay between requests
        /// </summary>
        /// <param name="minMs">Minimum delay in milliseconds</param>
        /// <param name="maxMs">Maximum delay in milliseconds</param>
        private async Task RandomDelayAsync(int minMs = 0, int maxMs = 0)
        {
            if (minMs <= 0)
                minMs = _settings.MinDelayBetweenRequests;
            
            if (maxMs <= 0)
                maxMs = _settings.MaxDelayBetweenRequests;
            
            var delayMs = _random.Next(minMs, maxMs);
            await Task.Delay(delayMs);
        }

        /// <summary>
        /// Rotate to the next proxy in the list
        /// </summary>
        private async Task RotateProxyAsync()
        {
            if (_settings.Proxies.Count <= 1)
                return;
            
            // Update the current proxy usage count
            if (_currentProxyIndex < _settings.Proxies.Count)
            {
                _settings.Proxies[_currentProxyIndex].UseCount++;
            }
            
            // Find the next active proxy
            int nextIndex = _currentProxyIndex;
            do
            {
                nextIndex = (nextIndex + 1) % _settings.Proxies.Count;
            } 
            while (!_settings.Proxies[nextIndex].IsActive && nextIndex != _currentProxyIndex);
            
            // If we couldn't find an active proxy, just use the next one
            if (nextIndex == _currentProxyIndex && !_settings.Proxies[nextIndex].IsActive)
            {
                nextIndex = (nextIndex + 1) % _settings.Proxies.Count;
            }
            
            _currentProxyIndex = nextIndex;
            
            _logger?.LogInformation($"Rotating to proxy: {_settings.Proxies[_currentProxyIndex].Host}:{_settings.Proxies[_currentProxyIndex].Port}");
            
            // Re-initialize the web driver with the new proxy
            _driver?.Quit();
            _driver?.Dispose();
            
            await InitializeWebDriverAsync();
        }

        #endregion
    }
} 
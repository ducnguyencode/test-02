using System.Collections.Generic;

namespace GoogleMapsScraper.Core.Models
{
    /// <summary>
    /// Settings for configuring the Google Maps scraper behavior
    /// </summary>
    public class ScraperSettings
    {
        /// <summary>
        /// The search keyword to look for on Google Maps
        /// </summary>
        public string SearchKeyword { get; set; }

        /// <summary>
        /// The geographic area to search in (city, region, country, etc.)
        /// </summary>
        public string GeographicArea { get; set; }

        /// <summary>
        /// The maximum number of results to retrieve
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Minimum delay between requests in milliseconds
        /// </summary>
        public int MinDelayBetweenRequests { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between requests in milliseconds
        /// </summary>
        public int MaxDelayBetweenRequests { get; set; } = 3000;

        /// <summary>
        /// Whether to only get businesses with phone numbers
        /// </summary>
        public bool OnlyBusinessesWithPhoneNumbers { get; set; } = false;

        /// <summary>
        /// List of proxy servers to use (if any)
        /// </summary>
        public List<ProxySettings> Proxies { get; set; } = new List<ProxySettings>();

        /// <summary>
        /// Whether to use proxy rotation
        /// </summary>
        public bool UseProxyRotation { get; set; } = false;

        /// <summary>
        /// The API key for CAPTCHA solving service (if any)
        /// </summary>
        public string CaptchaSolverApiKey { get; set; }

        /// <summary>
        /// The URL of the CAPTCHA solving service
        /// </summary>
        public string CaptchaSolverUrl { get; set; }

        /// <summary>
        /// Number of retry attempts when an error occurs
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Timeout for web requests in milliseconds
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// The Google Places API key (if available)
        /// </summary>
        public string GooglePlacesApiKey { get; set; }

        /// <summary>
        /// Whether to use the Google Places API instead of web scraping (when API key is provided)
        /// </summary>
        public bool UseGooglePlacesApi { get; set; } = false;

        /// <summary>
        /// Whether to use multithreading for parallel scraping
        /// </summary>
        public bool UseMultithreading { get; set; } = false;

        /// <summary>
        /// Number of concurrent threads to use when multithreading is enabled
        /// </summary>
        public int MaxConcurrentThreads { get; set; } = 2;

        /// <summary>
        /// The path where logs should be saved
        /// </summary>
        public string LogFilePath { get; set; } = "logs";
    }

    /// <summary>
    /// Settings for configuring a proxy server
    /// </summary>
    public class ProxySettings
    {
        /// <summary>
        /// The proxy server host address
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The proxy server port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The username for authenticated proxies (optional)
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The password for authenticated proxies (optional)
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Whether the proxy is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Number of times this proxy has been used
        /// </summary>
        public int UseCount { get; set; } = 0;

        /// <summary>
        /// Returns the proxy address in the format 'host:port'
        /// </summary>
        public string Address => $"{Host}:{Port}";

        /// <summary>
        /// Returns the full proxy URL including authentication if provided
        /// </summary>
        public string FullUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    return $"http://{Username}:{Password}@{Host}:{Port}";
                }
                return $"http://{Host}:{Port}";
            }
        }
    }
} 
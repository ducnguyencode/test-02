using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoogleMapsScraper.Core.Models;

namespace GoogleMapsScraper.Core.Interfaces
{
    /// <summary>
    /// Interface for Google Maps scraping functionality
    /// </summary>
    public interface IScraper : IDisposable
    {
        /// <summary>
        /// Initialize the scraper with the specified settings
        /// </summary>
        /// <param name="settings">The settings to configure the scraper</param>
        /// <returns>True if initialization was successful, otherwise false</returns>
        Task<bool> InitializeAsync(ScraperSettings settings);

        /// <summary>
        /// Scrape Google Maps for business data based on the configured settings
        /// </summary>
        /// <param name="progress">Optional progress callback</param>
        /// <param name="cancellationToken">Cancellation token to stop the scraping process</param>
        /// <returns>A collection of business data entries</returns>
        Task<IEnumerable<BusinessData>> ScrapeAsync(IProgress<ScrapeProgress> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pause the scraping process
        /// </summary>
        /// <returns>True if the process was paused successfully</returns>
        Task<bool> PauseAsync();

        /// <summary>
        /// Resume the scraping process after it was paused
        /// </summary>
        /// <returns>True if the process was resumed successfully</returns>
        Task<bool> ResumeAsync();

        /// <summary>
        /// Get the current status of the scraper
        /// </summary>
        /// <returns>The current scraper status</returns>
        ScraperStatus GetStatus();
    }

    /// <summary>
    /// Represents the progress of a scraping operation
    /// </summary>
    public class ScrapeProgress
    {
        /// <summary>
        /// The number of businesses found
        /// </summary>
        public int BusinessesFound { get; set; }

        /// <summary>
        /// The number of businesses processed
        /// </summary>
        public int BusinessesProcessed { get; set; }

        /// <summary>
        /// The percentage of completion (0-100)
        /// </summary>
        public double PercentComplete { get; set; }

        /// <summary>
        /// The current status message
        /// </summary>
        public string StatusMessage { get; set; }

        /// <summary>
        /// Any error message that occurred during scraping
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Whether a CAPTCHA was encountered
        /// </summary>
        public bool CaptchaDetected { get; set; }
        
        /// <summary>
        /// Whether the CAPTCHA was solved
        /// </summary>
        public bool CaptchaSolved { get; set; }
    }

    /// <summary>
    /// Represents the current status of the scraper
    /// </summary>
    public enum ScraperStatus
    {
        /// <summary>
        /// The scraper is not initialized
        /// </summary>
        NotInitialized,
        
        /// <summary>
        /// The scraper is ready but not running
        /// </summary>
        Ready,
        
        /// <summary>
        /// The scraper is currently running
        /// </summary>
        Running,
        
        /// <summary>
        /// The scraper is paused
        /// </summary>
        Paused,
        
        /// <summary>
        /// The scraper has completed its task
        /// </summary>
        Completed,
        
        /// <summary>
        /// The scraper encountered an error
        /// </summary>
        Error,
        
        /// <summary>
        /// The scraper was cancelled
        /// </summary>
        Cancelled
    }
} 
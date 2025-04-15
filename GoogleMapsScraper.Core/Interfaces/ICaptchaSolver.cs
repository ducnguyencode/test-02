using System;
using System.Threading.Tasks;

namespace GoogleMapsScraper.Core.Interfaces
{
    /// <summary>
    /// Interface for CAPTCHA solving services
    /// </summary>
    public interface ICaptchaSolver
    {
        /// <summary>
        /// Initialize the CAPTCHA solver with API key
        /// </summary>
        /// <param name="apiKey">The API key for the CAPTCHA solving service</param>
        /// <param name="serviceUrl">The URL of the CAPTCHA solving service (if different from default)</param>
        /// <returns>True if initialization was successful</returns>
        Task<bool> InitializeAsync(string apiKey, string serviceUrl = null);

        /// <summary>
        /// Solve a CAPTCHA from an image
        /// </summary>
        /// <param name="captchaImageData">The base64-encoded image data</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns>The solved CAPTCHA text</returns>
        Task<string> SolveImageCaptchaAsync(string captchaImageData, int timeout = 60000);

        /// <summary>
        /// Solve a reCAPTCHA v2
        /// </summary>
        /// <param name="siteKey">The reCAPTCHA site key</param>
        /// <param name="siteUrl">The URL of the site containing the CAPTCHA</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns>The solved CAPTCHA token</returns>
        Task<string> SolveRecaptchaV2Async(string siteKey, string siteUrl, int timeout = 180000);

        /// <summary>
        /// Get the current balance of the CAPTCHA solving service account
        /// </summary>
        /// <returns>The current balance</returns>
        Task<decimal> GetBalanceAsync();

        /// <summary>
        /// Get the service name
        /// </summary>
        string ServiceName { get; }
    }

    /// <summary>
    /// Exception thrown when a CAPTCHA solving error occurs
    /// </summary>
    public class CaptchaSolverException : Exception
    {
        /// <summary>
        /// Creates a new instance of the CaptchaSolverException class
        /// </summary>
        /// <param name="message">The error message</param>
        public CaptchaSolverException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new instance of the CaptchaSolverException class
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public CaptchaSolverException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
} 
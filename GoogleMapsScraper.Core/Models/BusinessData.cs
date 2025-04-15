using System;
using System.Collections.Generic;

namespace GoogleMapsScraper.Core.Models
{
    /// <summary>
    /// Represents business data scraped from Google Maps
    /// </summary>
    public class BusinessData
    {
        /// <summary>
        /// The name of the business
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The full address of the business
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The phone number of the business
        /// </summary>
        public string PhoneNumber { get; set; }

        /// <summary>
        /// The website URL of the business
        /// </summary>
        public string Website { get; set; }

        /// <summary>
        /// The latitude coordinate of the business
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// The longitude coordinate of the business
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// The rating of the business (usually between 1 and 5)
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// The number of reviews for the business
        /// </summary>
        public int? ReviewCount { get; set; }

        /// <summary>
        /// The operating hours of the business for each day of the week
        /// </summary>
        public Dictionary<string, string> OperatingHours { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The categories/types of the business
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Additional details about the business
        /// </summary>
        public Dictionary<string, string> AdditionalDetails { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The timestamp when this data was scraped
        /// </summary>
        public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    }
} 
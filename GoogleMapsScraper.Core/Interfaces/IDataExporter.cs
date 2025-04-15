using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleMapsScraper.Core.Models;

namespace GoogleMapsScraper.Core.Interfaces
{
    /// <summary>
    /// Interface for exporting scraped business data to various formats
    /// </summary>
    public interface IDataExporter
    {
        /// <summary>
        /// Export business data to CSV format
        /// </summary>
        /// <param name="businesses">The business data to export</param>
        /// <param name="filePath">The path where the CSV file should be saved</param>
        /// <returns>True if export was successful, otherwise false</returns>
        Task<bool> ExportToCsvAsync(IEnumerable<BusinessData> businesses, string filePath);

        /// <summary>
        /// Export business data to JSON format
        /// </summary>
        /// <param name="businesses">The business data to export</param>
        /// <param name="filePath">The path where the JSON file should be saved</param>
        /// <returns>True if export was successful, otherwise false</returns>
        Task<bool> ExportToJsonAsync(IEnumerable<BusinessData> businesses, string filePath);

        /// <summary>
        /// Export business data to Excel format
        /// </summary>
        /// <param name="businesses">The business data to export</param>
        /// <param name="filePath">The path where the Excel file should be saved</param>
        /// <returns>True if export was successful, otherwise false</returns>
        Task<bool> ExportToExcelAsync(IEnumerable<BusinessData> businesses, string filePath);
    }
} 
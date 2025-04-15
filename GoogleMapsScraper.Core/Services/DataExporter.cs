using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GoogleMapsScraper.Core.Interfaces;
using GoogleMapsScraper.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace GoogleMapsScraper.Core.Services
{
    /// <summary>
    /// Implementation of the IDataExporter interface
    /// </summary>
    public class DataExporter : IDataExporter
    {
        private readonly ILogger<DataExporter> _logger;

        /// <summary>
        /// Creates a new instance of the DataExporter class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public DataExporter(ILogger<DataExporter> logger = null)
        {
            _logger = logger;
            // Set the LicenseContext for EPPlus
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <inheritdoc/>
        public async Task<bool> ExportToCsvAsync(IEnumerable<BusinessData> businesses, string filePath)
        {
            try
            {
                _logger?.LogInformation($"Exporting {businesses.Count()} businesses to CSV: {filePath}");
                
                // Ensure the directory exists
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                    HasHeaderRecord = true
                };

                using (var writer = new StreamWriter(filePath))
                using (var csv = new CsvWriter(writer, config))
                {
                    // Register the business data map
                    csv.Context.RegisterClassMap<BusinessDataCsvMap>();
                    
                    // Write the records
                    await csv.WriteRecordsAsync(businesses);
                }

                _logger?.LogInformation($"Successfully exported to CSV: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error exporting to CSV: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExportToJsonAsync(IEnumerable<BusinessData> businesses, string filePath)
        {
            try
            {
                _logger?.LogInformation($"Exporting {businesses.Count()} businesses to JSON: {filePath}");
                
                // Ensure the directory exists
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Serialize to JSON
                var json = JsonConvert.SerializeObject(businesses, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                // Write to file
                await File.WriteAllTextAsync(filePath, json);

                _logger?.LogInformation($"Successfully exported to JSON: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error exporting to JSON: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExportToExcelAsync(IEnumerable<BusinessData> businesses, string filePath)
        {
            try
            {
                _logger?.LogInformation($"Exporting {businesses.Count()} businesses to Excel: {filePath}");
                
                // Ensure the directory exists
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Business Data");

                    // Add headers
                    worksheet.Cells[1, 1].Value = "Name";
                    worksheet.Cells[1, 2].Value = "Address";
                    worksheet.Cells[1, 3].Value = "Phone Number";
                    worksheet.Cells[1, 4].Value = "Website";
                    worksheet.Cells[1, 5].Value = "Latitude";
                    worksheet.Cells[1, 6].Value = "Longitude";
                    worksheet.Cells[1, 7].Value = "Rating";
                    worksheet.Cells[1, 8].Value = "Review Count";
                    worksheet.Cells[1, 9].Value = "Categories";
                    worksheet.Cells[1, 10].Value = "Operating Hours";
                    worksheet.Cells[1, 11].Value = "Additional Details";
                    worksheet.Cells[1, 12].Value = "Scraped At";

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 12])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    // Add data
                    int row = 2;
                    foreach (var business in businesses)
                    {
                        worksheet.Cells[row, 1].Value = business.Name;
                        worksheet.Cells[row, 2].Value = business.Address;
                        worksheet.Cells[row, 3].Value = business.PhoneNumber;
                        worksheet.Cells[row, 4].Value = business.Website;
                        worksheet.Cells[row, 5].Value = business.Latitude;
                        worksheet.Cells[row, 6].Value = business.Longitude;
                        worksheet.Cells[row, 7].Value = business.Rating;
                        worksheet.Cells[row, 8].Value = business.ReviewCount;
                        worksheet.Cells[row, 9].Value = string.Join(", ", business.Categories);
                        
                        // Format operating hours
                        var hours = string.Join(Environment.NewLine, 
                            business.OperatingHours.Select(h => $"{h.Key}: {h.Value}"));
                        worksheet.Cells[row, 10].Value = hours;
                        
                        // Format additional details
                        var details = string.Join(Environment.NewLine, 
                            business.AdditionalDetails.Select(d => $"{d.Key}: {d.Value}"));
                        worksheet.Cells[row, 11].Value = details;
                        
                        worksheet.Cells[row, 12].Value = business.ScrapedAt;
                        worksheet.Cells[row, 12].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                        
                        row++;
                    }

                    // Auto-fit columns
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Save the file
                    var fileInfo = new FileInfo(filePath);
                    await package.SaveAsAsync(fileInfo);
                }

                _logger?.LogInformation($"Successfully exported to Excel: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error exporting to Excel: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// CSV mapping class for BusinessData
    /// </summary>
    public class BusinessDataCsvMap : ClassMap<BusinessData>
    {
        /// <summary>
        /// Creates a new instance of the BusinessDataCsvMap class
        /// </summary>
        public BusinessDataCsvMap()
        {
            Map(m => m.Name).Name("Name");
            Map(m => m.Address).Name("Address");
            Map(m => m.PhoneNumber).Name("Phone Number");
            Map(m => m.Website).Name("Website");
            Map(m => m.Latitude).Name("Latitude");
            Map(m => m.Longitude).Name("Longitude");
            Map(m => m.Rating).Name("Rating");
            Map(m => m.ReviewCount).Name("Review Count");
            Map(m => string.Join(", ", m.Categories)).Name("Categories");
            
            // Convert operating hours to a single string
            Map(m => string.Join("; ", m.OperatingHours.Select(h => $"{h.Key}: {h.Value}")))
                .Name("Operating Hours");
            
            // Convert additional details to a single string
            Map(m => string.Join("; ", m.AdditionalDetails.Select(d => $"{d.Key}: {d.Value}")))
                .Name("Additional Details");
            
            Map(m => m.ScrapedAt).Name("Scraped At");
        }
    }
} 
# Google Maps Scraper Tool

A secure, feature-rich C# application for scraping business data from Google Maps.

## Features

- **Search Capabilities**: Search for businesses by keyword and geographic area
- **Comprehensive Data Extraction**: Scrapes business name, address, phone number, website, coordinates, ratings, reviews, categories, and hours of operation
- **Data Export**: Export scraped data to CSV, JSON, or Excel formats
- **Security Measures**:
  - CAPTCHA solving capabilities (using 2Captcha service)
  - Proxy rotation to avoid IP blocking
  - Random delays between requests
- **Customizable Settings**:
  - Set minimum/maximum delays between requests
  - Only retrieve businesses with phone numbers
  - Adjust maximum number of results
  - Configure various timeout settings
- **Robustness**:
  - Pause/resume functionality
  - Error handling and recovery
  - Detailed logging

## System Requirements

- .NET SDK 6.0 or higher
- Windows OS (for the GUI version)
- Internet connection

## Installation

### From Source Code

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/google-maps-scraper.git
   cd google-maps-scraper
   ```

2. Build the solution:
   ```
   dotnet build -c Release
   ```

3. Run the application:
   ```
   cd GoogleMapsScraper.UI/bin/Release/net6.0
   GoogleMapsScraper.UI.exe
   ```

### Executable Download

1. Download the latest release from the [Releases](https://github.com/yourusername/google-maps-scraper/releases) page
2. Extract the ZIP archive
3. Run the `GoogleMapsScraper.UI.exe` file

## Usage

### Console Version

1. Launch the application
2. Follow the interactive prompts to configure:
   - Search keyword (e.g., "restaurants", "hotels", etc.)
   - Geographic area (e.g., "New York, NY", "Chicago", etc.)
   - Maximum number of results
   - Minimum and maximum delay between requests
   - Proxy settings (optional)
   - CAPTCHA solver API key (optional)
3. The scraper will start collecting data
4. After completion, choose a format to export the collected data (CSV, JSON, Excel)

### GUI Version (Coming Soon)

The graphical user interface will provide all the features of the console version with an easier-to-use interface.

## Anti-Detection Mechanisms

The scraper implements several techniques to avoid detection:

1. **Random Delays**: Configurable delays between requests to mimic human behavior
2. **Proxy Rotation**: Support for multiple proxies with automatic rotation
3. **Custom User Agents**: Randomized user agents to avoid fingerprinting
4. **CAPTCHA Solving**: Integration with 2Captcha service to automatically solve CAPTCHAs

## Ethical Usage and Legal Considerations

This tool is provided for educational and research purposes only. Users are responsible for:

1. Complying with Google's Terms of Service
2. Respecting website robots.txt files
3. Using the tool in a manner that does not disrupt services
4. Obtaining proper authorization before scraping any website
5. Adhering to all applicable local, state, and federal laws

## Advanced Configuration

### Proxy Configuration

The tool supports HTTP, HTTPS, and SOCKS proxies. To use authenticated proxies, provide the username and password when prompted.

### CAPTCHA Solving

To enable automatic CAPTCHA solving:

1. Create an account at [2Captcha](https://2captcha.com/)
2. Obtain your API key
3. Enter the API key when prompted by the application

## Troubleshooting

### Common Issues

1. **Chrome driver errors**: Ensure you have the latest version of Chrome installed
2. **Proxy connection issues**: Verify your proxy settings and connectivity
3. **Data not being scraped**: Google Maps layout may have changed; check for updates to the application

### Logs

The application creates detailed logs in the `logs` directory. These logs are invaluable for diagnosing issues.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Disclaimer

This tool is for educational purposes only. The developers are not responsible for any misuse of this tool or for any violations of terms of service of any website. Use at your own risk.

## Contact

For support, feature requests, or bug reports, please open an issue on the GitHub repository. 
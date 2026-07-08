using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LoanDefaultPrediction.Web.Services
{
    public class DatasetDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly IngestionService _ingestionService;
        private readonly IConfiguration _configuration;

        public DatasetDownloadService(HttpClient httpClient, IngestionService ingestionService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _ingestionService = ingestionService;
            _configuration = configuration;
        }

        public async Task<IngestionSummary> DownloadAndIngestAsync(string datasetPath, string batchId)
        {
            // Read credentials from Environment variables or App Settings configuration
            string username = Environment.GetEnvironmentVariable("KAGGLE_USERNAME") 
                              ?? _configuration["Kaggle:Username"];
            string apiKey = Environment.GetEnvironmentVariable("KAGGLE_KEY") 
                            ?? _configuration["Kaggle:ApiKey"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Kaggle credentials are not configured. Please set KAGGLE_USERNAME and KAGGLE_KEY environment variables.");
            }

            // Validate dataset path format (owner/dataset-name)
            var parts = datasetPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                throw new ArgumentException("Dataset path must be in the format 'owner/dataset-name'.");
            }

            string owner = parts[0];
            string datasetName = parts[1];

            string requestUrl = $"https://www.kaggle.com/api/v1/datasets/download/{owner}/{datasetName}";

            // Setup Basic Authentication header for Kaggle API
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiKey}"));
            
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            // Execute the request and support redirection (HttpClient automatically follows redirects by default)
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Kaggle API returned error ({response.StatusCode}): {errorContent}");
            }

            // Open response stream directly as a zip archive (memory-efficient streaming)
            using var zipStream = await response.Content.ReadAsStreamAsync();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            ZipArchiveEntry csvEntry = null;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    csvEntry = entry;
                    break;
                }
            }

            if (csvEntry == null)
            {
                throw new FileNotFoundException("No CSV files found inside the Kaggle dataset zip archive.");
            }

            // Open the CSV file entry stream and ingest it directly into SQLite
            using var csvStream = csvEntry.Open();
            return await _ingestionService.IngestCsvAsync(csvStream, batchId);
        }
    }
}

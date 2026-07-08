using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using LoanDefaultPrediction.Web.Data;

namespace LoanDefaultPrediction.Web.Services
{
    public class IngestionSummary
    {
        public string BatchId { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int ValidatedRecords { get; set; }
        public int FailedRecords { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class IngestionService
    {
        private readonly LoanDbContext _dbContext;

        public IngestionService(LoanDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IngestionSummary> IngestCsvAsync(Stream csvStream, string batchId)
        {
            var summary = new IngestionSummary { BatchId = batchId };
            var recordsToInsert = new List<LoanRecord>();

            using var reader = new StreamReader(csvStream);
            
            // Read Header
            string? headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                summary.Errors.Add("CSV file is empty or missing a header.");
                return summary;
            }

            int lineNumber = 1;
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                summary.TotalRecords++;

                var parts = line.Split(',');
                if (parts.Length < 9)
                {
                    summary.FailedRecords++;
                    summary.Errors.Add($"Line {lineNumber}: Expected at least 9 columns, found {parts.Length}.");
                    continue;
                }

                try
                {
                    // Trim parts and parse
                    float age = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                    float income = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    float loanAmount = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                    float creditScore = float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
                    float monthsEmployed = float.Parse(parts[4].Trim(), CultureInfo.InvariantCulture);
                    float interestRate = float.Parse(parts[5].Trim(), CultureInfo.InvariantCulture);
                    float loanTerm = float.Parse(parts[6].Trim(), CultureInfo.InvariantCulture);
                    float debtToIncomeRatio = float.Parse(parts[7].Trim(), CultureInfo.InvariantCulture);
                    
                    bool isDefault = false;
                    string defaultStr = parts[8].Trim().ToLower();
                    if (defaultStr == "true" || defaultStr == "1" || defaultStr == "yes")
                    {
                        isDefault = true;
                    }
                    else if (defaultStr == "false" || defaultStr == "0" || defaultStr == "no")
                    {
                        isDefault = false;
                    }
                    else
                    {
                        throw new FormatException($"Invalid value for Default column: '{parts[8]}'");
                    }

                    // Validate ranges
                    if (age < 18 || age > 100) throw new ArgumentOutOfRangeException(nameof(age), "Age must be between 18 and 100.");
                    if (income < 0) throw new ArgumentOutOfRangeException(nameof(income), "Income cannot be negative.");
                    if (loanAmount <= 0) throw new ArgumentOutOfRangeException(nameof(loanAmount), "Loan amount must be positive.");
                    if (creditScore < 300 || creditScore > 850) throw new ArgumentOutOfRangeException(nameof(creditScore), "Credit score must be between 300 and 850.");
                    if (monthsEmployed < 0) throw new ArgumentOutOfRangeException(nameof(monthsEmployed), "Months employed cannot be negative.");
                    if (interestRate < 0) throw new ArgumentOutOfRangeException(nameof(interestRate), "Interest rate cannot be negative.");
                    if (loanTerm <= 0) throw new ArgumentOutOfRangeException(nameof(loanTerm), "Loan term must be positive.");
                    if (debtToIncomeRatio < 0 || debtToIncomeRatio > 2.0f) throw new ArgumentOutOfRangeException(nameof(debtToIncomeRatio), "Debt-to-income ratio must be between 0 and 2.0.");

                    recordsToInsert.Add(new LoanRecord
                    {
                        Age = age,
                        Income = income,
                        LoanAmount = loanAmount,
                        CreditScore = creditScore,
                        MonthsEmployed = monthsEmployed,
                        InterestRate = interestRate,
                        LoanTerm = loanTerm,
                        DebtToIncomeRatio = debtToIncomeRatio,
                        Default = isDefault,
                        BatchId = batchId,
                        CreatedAt = DateTime.UtcNow
                    });

                    summary.ValidatedRecords++;
                }
                catch (Exception ex)
                {
                    summary.FailedRecords++;
                    if (summary.Errors.Count < 50)
                    {
                        summary.Errors.Add($"Line {lineNumber}: Validation error - {ex.Message}");
                    }
                }
            }

            if (recordsToInsert.Count > 0)
            {
                await _dbContext.LoanRecords.AddRangeAsync(recordsToInsert);
                await _dbContext.SaveChangesAsync();
            }

            return summary;
        }
    }
}

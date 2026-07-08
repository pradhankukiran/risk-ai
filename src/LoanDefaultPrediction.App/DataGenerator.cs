using System;
using System.IO;
using System.Text;

namespace LoanDefaultPrediction.App
{
    public static class DataGenerator
    {
        public static void GenerateCsv(string outputPath, int recordCount)
        {
            var random = new Random(42); // Seeded for reproducibility
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("Age,Income,LoanAmount,CreditScore,MonthsEmployed,InterestRate,LoanTerm,DebtToIncomeRatio,Default");

            for (int i = 0; i < recordCount; i++)
            {
                float age = random.Next(18, 75);
                float income = random.Next(15000, 180000);
                
                // Loan amount usually proportional to income
                float maxLoan = income * (random.Next(10, 40) / 100.0f);
                float loanAmount = (float)Math.Round(random.NextDouble() * (maxLoan - 2000) + 2000, 2);
                
                float creditScore = random.Next(300, 850);
                float monthsEmployed = random.Next(0, 360);
                
                // Interest rate is inversely related to Credit Score
                float baseRate = 25f - (creditScore - 300) / 550f * 20f;
                float interestRate = (float)Math.Round(baseRate + (random.NextDouble() * 3.0 - 1.5), 2);
                interestRate = Math.Clamp(interestRate, 3.0f, 28.0f);
                
                float[] terms = { 12, 24, 36, 48, 60 };
                float loanTerm = terms[random.Next(terms.Length)];
                
                float debtToIncomeRatio = (float)Math.Round(random.NextDouble() * 0.6 + 0.1, 2);
                debtToIncomeRatio += (loanAmount / income) * 0.5f;
                debtToIncomeRatio = Math.Clamp(debtToIncomeRatio, 0.05f, 1.2f);

                // Calculate probability of default using a logistic function of features
                double score = -3.0; // Base log-odds
                score += (850 - creditScore) / 550.0 * 3.5; // low credit score = higher default
                score += debtToIncomeRatio * 4.0; // high DTI = higher default
                score += (interestRate / 100.0) * 5.0; // high interest rate = higher default
                score -= (monthsEmployed / 12.0) * 0.15; // length of employment = lower default
                score -= (income / 50000.0) * 0.5; // income level = lower default
                
                double probability = 1.0 / (1.0 + Math.Exp(-score));
                bool isDefault = random.NextDouble() < probability;

                sb.AppendLine($"{age},{income},{loanAmount},{creditScore},{monthsEmployed},{interestRate},{loanTerm},{debtToIncomeRatio:F2},{(isDefault ? 1 : 0)}");
            }

            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}

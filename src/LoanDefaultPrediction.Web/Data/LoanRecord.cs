using System;
using System.ComponentModel.DataAnnotations;

namespace LoanDefaultPrediction.Web.Data
{
    public class LoanRecord
    {
        [Key]
        public int Id { get; set; }

        public float Age { get; set; }
        public float Income { get; set; }
        public float LoanAmount { get; set; }
        public float CreditScore { get; set; }
        public float MonthsEmployed { get; set; }
        public float InterestRate { get; set; }
        public float LoanTerm { get; set; }
        public float DebtToIncomeRatio { get; set; }
        public bool Default { get; set; }
        
        public string BatchId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

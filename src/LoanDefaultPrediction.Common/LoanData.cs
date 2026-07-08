using Microsoft.ML.Data;

namespace LoanDefaultPrediction.Common
{
    public class LoanData
    {
        [LoadColumn(0)]
        public float Age { get; set; }

        [LoadColumn(1)]
        public float Income { get; set; }

        [LoadColumn(2)]
        public float LoanAmount { get; set; }

        [LoadColumn(3)]
        public float CreditScore { get; set; }

        [LoadColumn(4)]
        public float MonthsEmployed { get; set; }

        [LoadColumn(5)]
        public float InterestRate { get; set; }

        [LoadColumn(6)]
        public float LoanTerm { get; set; }

        [LoadColumn(7)]
        public float DebtToIncomeRatio { get; set; }

        [LoadColumn(8), ColumnName("Label")]
        public bool Default { get; set; }
    }
}

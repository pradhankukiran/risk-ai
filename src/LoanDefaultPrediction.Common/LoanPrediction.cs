using Microsoft.ML.Data;

namespace LoanDefaultPrediction.Common
{
    public class LoanPrediction
    {
        [ColumnName("PredictedLabel")]
        public bool PredictedDefault { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}

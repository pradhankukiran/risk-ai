using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LoanDefaultPrediction.Web.Data
{
    public class LoanDbContext : DbContext
    {
        public LoanDbContext(DbContextOptions<LoanDbContext> options) : base(options)
        {
        }

        public DbSet<LoanRecord> LoanRecords => Set<LoanRecord>();
        public DbSet<TrainingRun> TrainingRuns => Set<TrainingRun>();
    }

    public class TrainingRun
    {
        [Key]
        public int Id { get; set; }
        public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
        public string ModelPath { get; set; } = string.Empty;
        public float Accuracy { get; set; }
        public float AreaUnderRoc { get; set; }
        public float F1Score { get; set; }
        public float Precision { get; set; }
        public float Recall { get; set; }
        public string ConfusionMatrixJson { get; set; } = string.Empty; // Serialized TN, FP, FN, TP
        public string PfiMetricsJson { get; set; } = string.Empty; // Serialized JSON of feature importance
        public bool IsActive { get; set; }
    }
}

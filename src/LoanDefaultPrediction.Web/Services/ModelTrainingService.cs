using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using LoanDefaultPrediction.Common;
using LoanDefaultPrediction.Web.Data;

namespace LoanDefaultPrediction.Web.Services
{
    public class ModelTrainingResult
    {
        public int RunId { get; set; }
        public float Accuracy { get; set; }
        public float AreaUnderRoc { get; set; }
        public float F1Score { get; set; }
        public float Precision { get; set; }
        public float Recall { get; set; }
        public Dictionary<string, double> ConfusionMatrix { get; set; } = new();
        public Dictionary<string, double> FeatureImportance { get; set; } = new();
    }

    public class ModelTrainingService
    {
        private readonly LoanDbContext _dbContext;

        public ModelTrainingService(LoanDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ModelTrainingResult> TrainModelAsync()
        {
            // 1. Fetch records from DB
            var dbRecords = await _dbContext.LoanRecords.ToListAsync();
            if (dbRecords.Count == 0)
            {
                throw new InvalidOperationException("No training data found in database. Ingest CSV data first.");
            }

            // 2. Map to ML.NET schema
            var mlDataList = dbRecords.Select(r => new LoanData
            {
                Age = r.Age,
                Income = r.Income,
                LoanAmount = r.LoanAmount,
                CreditScore = r.CreditScore,
                MonthsEmployed = r.MonthsEmployed,
                InterestRate = r.InterestRate,
                LoanTerm = r.LoanTerm,
                DebtToIncomeRatio = r.DebtToIncomeRatio,
                Default = r.Default
            }).ToList();

            // 3. Initialize ML.NET
            var mlContext = new MLContext(seed: 42);
            var fullDataView = mlContext.Data.LoadFromEnumerable(mlDataList);

            // 4. Split into Train (80%) and Test (20%)
            var split = mlContext.Data.TrainTestSplit(fullDataView, testFraction: 0.2, seed: 42);
            var trainData = split.TrainSet;
            var testData = split.TestSet;

            // 5. Build features concatenation and normalization
            var featureNames = new[] { 
                "Age", 
                "Income", 
                "LoanAmount", 
                "CreditScore", 
                "MonthsEmployed", 
                "InterestRate", 
                "LoanTerm", 
                "DebtToIncomeRatio" 
            };
            
            var pipeline = mlContext.Transforms.Concatenate("Features", featureNames)
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features"));

            // 6. Train the model
            var model = pipeline.Fit(trainData);

            // 7. Evaluate
            var predictions = model.Transform(testData);
            var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");

            // Extract the trained predictor (the last transformer in the chain)
            var modelChain = (IEnumerable<ITransformer>)model;
            var predictor = (ISingleFeaturePredictionTransformer<object>)modelChain.Last();

            // Transform the test data to get the preprocessed vector features the predictor expects
            var preprocessedTestData = model.Transform(testData);

            var pfiResults = mlContext.BinaryClassification.PermutationFeatureImportance(
                predictor,
                preprocessedTestData,
                labelColumnName: "Label",
                permutationCount: 5
            );

            // Map PFI results back to feature names (Mean drop in AUC)
            var pfiDict = new Dictionary<string, double>();
            for (int i = 0; i < featureNames.Length; i++)
            {
                // ML.NET returns (PermutedMetric - OriginalMetric), which is negative for a performance drop.
                // We negate it to get the positive drop value.
                double meanDrop = -pfiResults[i].AreaUnderRocCurve.Mean;
                pfiDict[featureNames[i]] = Math.Max(0.0, meanDrop); // Ensure positive importance score
            }

            // Normalize PFI values to percentage
            double totalDrop = pfiDict.Values.Sum();
            var relativeImportance = new Dictionary<string, double>();
            if (totalDrop > 0)
            {
                foreach (var kvp in pfiDict)
                {
                    relativeImportance[kvp.Key] = Math.Round((kvp.Value / totalDrop) * 100, 2);
                }
            }
            else
            {
                foreach (var kvp in pfiDict)
                {
                    relativeImportance[kvp.Key] = 0.0;
                }
            }

            // Sort importance by descending values
            var sortedImportance = relativeImportance
                .OrderByDescending(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);

            // 9. Save model to zip file
            string modelFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            if (!Directory.Exists(modelFolder))
            {
                Directory.CreateDirectory(modelFolder);
            }
            string modelFileName = $"model_{Guid.NewGuid():N}.zip";
            string modelPath = Path.Combine(modelFolder, modelFileName);
            string activeModelPath = Path.Combine(modelFolder, "active_model.zip");
            
            mlContext.Model.Save(model, trainData.Schema, modelPath);
            File.Copy(modelPath, activeModelPath, overwrite: true);

            // 10. Update DB (mark existing active runs as inactive)
            var activeRuns = await _dbContext.TrainingRuns.Where(r => r.IsActive).ToListAsync();
            foreach (var run in activeRuns)
            {
                run.IsActive = false;
            }

            // Extract confusion matrix counts
            var confMatrix = metrics.ConfusionMatrix;
            var confusionDict = new Dictionary<string, double>
            {
                { "TN", confMatrix.Counts[0][0] },
                { "FP", confMatrix.Counts[0][1] },
                { "FN", confMatrix.Counts[1][0] },
                { "TP", confMatrix.Counts[1][1] }
            };

            var newRun = new TrainingRun
            {
                TrainedAt = DateTime.UtcNow,
                ModelPath = modelPath,
                Accuracy = (float)metrics.Accuracy,
                AreaUnderRoc = (float)metrics.AreaUnderRocCurve,
                F1Score = (float)metrics.F1Score,
                Precision = (float)metrics.PositivePrecision,
                Recall = (float)metrics.PositiveRecall,
                ConfusionMatrixJson = JsonSerializer.Serialize(confusionDict),
                PfiMetricsJson = JsonSerializer.Serialize(sortedImportance),
                IsActive = true
            };

            await _dbContext.TrainingRuns.AddAsync(newRun);
            await _dbContext.SaveChangesAsync();

            return new ModelTrainingResult
            {
                RunId = newRun.Id,
                Accuracy = newRun.Accuracy,
                AreaUnderRoc = newRun.AreaUnderRoc,
                F1Score = newRun.F1Score,
                Precision = newRun.Precision,
                Recall = newRun.Recall,
                ConfusionMatrix = confusionDict,
                FeatureImportance = sortedImportance
            };
        }
    }
}

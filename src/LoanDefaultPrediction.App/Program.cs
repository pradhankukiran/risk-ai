using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;
using LoanDefaultPrediction.Common;

namespace LoanDefaultPrediction.App
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================");
            Console.WriteLine("     Loan Default Prediction System (ML.NET)     ");
            Console.WriteLine("=================================================");
            Console.ResetColor();

            if (args.Length > 0 && args[0].ToLower() == "--generate")
            {
                int count = 10000;
                if (args.Length > 1 && int.TryParse(args[1], out int parsedCount))
                {
                    count = parsedCount;
                }

                string outputPath = "loans_synthetic.csv";
                if (args.Length > 2)
                {
                    outputPath = args[2];
                }

                Console.WriteLine($"\nGenerating {count} synthetic loan records...");
                try
                {
                    DataGenerator.GenerateCsv(outputPath, count);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Successfully generated {count} records and saved to '{Path.GetFullPath(outputPath)}'");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error generating data: {ex.Message}");
                    Console.ResetColor();
                }
                return;
            }

            if (args.Length > 0 && args[0].ToLower() == "--train")
            {
                string csvPath = "loans_synthetic.csv";
                if (!File.Exists(csvPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Synthetic data file '{csvPath}' not found. Run with --generate first.");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine("\n[Local Debug] Loading synthetic data...");
                var mlContext = new MLContext(seed: 42);
                var dataView = mlContext.Data.LoadFromTextFile<LoanData>(csvPath, hasHeader: true, separatorChar: ',');

                var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);
                var trainData = split.TrainSet;
                var testData = split.TestSet;

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

                Console.WriteLine("Training local LightGBM model...");
                var model = pipeline.Fit(trainData);

                Console.WriteLine("Evaluating local model metrics...");
                var predictions = model.Transform(testData);
                var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "Label");
                Console.WriteLine($"  Accuracy:     {metrics.Accuracy:F4}");
                Console.WriteLine($"  AUC:          {metrics.AreaUnderRocCurve:F4}");
                Console.WriteLine($"  F1-Score:     {metrics.F1Score:F4}");

                Console.WriteLine("\nEvaluating local PFI...");
                var modelChain = (IEnumerable<ITransformer>)model;
                var predictor = (ISingleFeaturePredictionTransformer<object>)modelChain.Last();
                var preprocessedTestData = model.Transform(testData);

                try
                {
                    var pfiResults = mlContext.BinaryClassification.PermutationFeatureImportance(
                        predictor,
                        preprocessedTestData,
                        labelColumnName: "Label",
                        permutationCount: 5
                    );

                    // Compute relative importance
                    var pfiDict = new Dictionary<string, double>();
                    for (int i = 0; i < featureNames.Length; i++)
                    {
                        double meanDrop = -pfiResults[i].AreaUnderRocCurve.Mean;
                        pfiDict[featureNames[i]] = Math.Max(0.0, meanDrop);
                    }

                    double totalDrop = pfiDict.Values.Sum();
                    var relativeImportance = new List<KeyValuePair<string, double>>();
                    foreach (var kvp in pfiDict)
                    {
                        double relVal = totalDrop > 0 ? (kvp.Value / totalDrop) * 100 : 0;
                        relativeImportance.Add(new KeyValuePair<string, double>(kvp.Key, Math.Round(relVal, 2)));
                    }

                    var sorted = relativeImportance.OrderByDescending(x => x.Value).ToList();

                    Console.WriteLine("\nPermutation Feature Importance (PFI) ranking:");
                    Console.WriteLine("┌───────────────────────┬────────────┐");
                    Console.WriteLine("│ Feature               │ Importance │");
                    Console.WriteLine("├───────────────────────┼────────────┤");
                    foreach (var item in sorted)
                    {
                        Console.WriteLine($"│ {item.Key,-21} │ {item.Value,9:F2}% │");
                    }
                    Console.WriteLine("└───────────────────────┴────────────┘");

                    // Save the model
                    string modelPath = "model.zip";
                    mlContext.Model.Save(model, trainData.Schema, modelPath);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nSuccessfully saved trained model to '{Path.GetFullPath(modelPath)}'");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"PFI Calculation/Saving Error: {ex.Message}");
                    Console.ResetColor();
                }
                return;
            }

            Console.WriteLine("\nAvailable CLI arguments:");
            Console.WriteLine("  --generate [count] [path]  Generate a synthetic loan dataset CSV (default: 10000 records to loans_synthetic.csv)");
            Console.WriteLine("  --train                    Train, evaluate, and save the ML model");
            Console.WriteLine("  --predict                  Run loan default prediction interactively");
        }
    }
}

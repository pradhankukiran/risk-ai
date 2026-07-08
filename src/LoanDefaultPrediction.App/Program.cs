using System;
using System.IO;

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

            Console.WriteLine("\nAvailable CLI arguments:");
            Console.WriteLine("  --generate [count] [path]  Generate a synthetic loan dataset CSV (default: 10000 records to loans_synthetic.csv)");
            Console.WriteLine("  --train                    Train, evaluate, and save the ML model");
            Console.WriteLine("  --predict                  Run loan default prediction interactively");
        }
    }
}

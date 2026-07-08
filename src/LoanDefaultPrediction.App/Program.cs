using System;

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

            Console.WriteLine("\nProject successfully scaffolded!");
            Console.WriteLine("This solution contains:");
            Console.WriteLine("  - LoanDefaultPrediction.Common : Shared library containing data models.");
            Console.WriteLine("  - LoanDefaultPrediction.App    : Console app for data generation, training, and inference.");
            
            Console.WriteLine("\nFuture Phases Roadmap:");
            Console.WriteLine("  - Phase 1: Data Preparation & Synthetic Dataset Generation");
            Console.WriteLine("  - Phase 2: Building and Training the ML.NET Pipeline");
            Console.WriteLine("  - Phase 3: Model Evaluation and Inference");

            Console.WriteLine("\nAvailable CLI arguments (to be implemented):");
            Console.WriteLine("  --generate   Generate a synthetic loan dataset CSV");
            Console.WriteLine("  --train      Train, evaluate, and save the ML model");
            Console.WriteLine("  --predict    Run loan default prediction interactively");
            Console.WriteLine("\nPhase 0 Setup complete. Ready for Phase 1!");
        }
    }
}

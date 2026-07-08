using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ML;
using LoanDefaultPrediction.Common;
using LoanDefaultPrediction.Web.Data;
using LoanDefaultPrediction.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext with SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=loans.db";
builder.Services.AddDbContext<LoanDbContext>(options =>
    options.UseSqlite(connectionString));

// Register Services
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<ModelTrainingService>();
builder.Services.AddHttpClient<DatasetDownloadService>();

// Register PredictionEnginePool for ML.NET predictions
var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "active_model.zip");
var modelDirectory = Path.GetDirectoryName(modelPath);
if (!string.IsNullOrEmpty(modelDirectory) && !Directory.Exists(modelDirectory))
{
    Directory.CreateDirectory(modelDirectory);
}
// Bootstrap copy if active_model.zip is missing
if (!File.Exists(modelPath))
{
    // 1. Try to find the most recent model_*.zip in the models directory
    var zipFiles = Directory.GetFiles(modelDirectory, "model_*.zip");
    if (zipFiles.Length > 0)
    {
        var mostRecent = zipFiles.Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTime).First();
        File.Copy(mostRecent.FullName, modelPath, overwrite: true);
    }
    // 2. Search for solution-level model.zip fallback
    else if (File.Exists("model.zip"))
    {
        File.Copy("model.zip", modelPath, overwrite: true);
    }
    else if (File.Exists("../model.zip"))
    {
        File.Copy("../model.zip", modelPath, overwrite: true);
    }
    else if (File.Exists("../../model.zip"))
    {
        File.Copy("../../model.zip", modelPath, overwrite: true);
    }
}

if (File.Exists(modelPath))
{
    builder.Services.AddPredictionEnginePool<LoanData, LoanPrediction>()
        .FromFile(modelPath);
}

// Configure CORS to allow local dev access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

// Ensure Database is Created & Migrated on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
    dbContext.Database.Migrate();
}

app.MapGet("/api/database-stats", async (LoanDbContext dbContext) =>
{
    var totalRecords = await dbContext.LoanRecords.CountAsync();
    var defaultCount = await dbContext.LoanRecords.CountAsync(r => r.Default);
    var activeRun = await dbContext.TrainingRuns.OrderByDescending(r => r.TrainedAt).FirstOrDefaultAsync(r => r.IsActive);
    
    // Get historical runs to show on the dashboard
    var historicalRuns = await dbContext.TrainingRuns
        .OrderByDescending(r => r.TrainedAt)
        .Take(5)
        .Select(r => new { r.Id, r.TrainedAt, r.Accuracy, r.AreaUnderRoc, r.F1Score, r.Precision, r.Recall, r.IsActive })
        .ToListAsync();

    return Results.Ok(new
    {
        TotalRecords = totalRecords,
        DefaultCount = defaultCount,
        NonDefaultCount = totalRecords - defaultCount,
        DefaultRate = totalRecords > 0 ? (double)defaultCount / totalRecords : 0.0,
        ActiveModel = activeRun != null ? new { 
            activeRun.Id, 
            activeRun.TrainedAt, 
            activeRun.Accuracy, 
            activeRun.AreaUnderRoc,
            activeRun.F1Score,
            activeRun.Precision,
            activeRun.Recall,
            ConfusionMatrix = string.IsNullOrEmpty(activeRun.ConfusionMatrixJson) ? new Dictionary<string, double>() : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(activeRun.ConfusionMatrixJson),
            PfiMetrics = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(activeRun.PfiMetricsJson)
        } : null,
        HistoricalRuns = historicalRuns
    });
});

app.MapPost("/api/ingest", async (IFormFile file, IngestionService ingestionService) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { Error = "No file uploaded or file is empty." });
    }

    if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { Error = "Only CSV files are supported." });
    }

    var batchId = Guid.NewGuid().ToString("N");
    using var stream = file.OpenReadStream();
    
    var summary = await ingestionService.IngestCsvAsync(stream, batchId);
    
    return Results.Ok(summary);
}).DisableAntiforgery();

app.MapPost("/api/kaggle-ingest", async (KaggleIngestRequest request, DatasetDownloadService downloadService) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.DatasetPath))
    {
        return Results.BadRequest(new { Error = "Dataset path is required." });
    }

    var batchId = Guid.NewGuid().ToString("N");
    try
    {
        var summary = await downloadService.DownloadAndIngestAsync(request.DatasetPath, batchId);
        return Results.Ok(summary);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("credentials"))
    {
        return Results.Json(new { Error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
}).DisableAntiforgery();

app.MapPost("/api/train", async (ModelTrainingService trainingService, IServiceProvider serviceProvider) =>
{
    try
    {
        var result = await trainingService.TrainModelAsync();
        
        // Reload pool if registered
        var pool = serviceProvider.GetService<PredictionEnginePool<LoanData, LoanPrediction>>();
        if (pool != null)
        {
            var activeModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "active_model.zip");
            // If the pool wasn't registered at startup (because no model.zip existed), reloading won't work automatically,
            // but since we bootstrapped it, it is registered and will reload.
            // We call GetPredictionEngine to trigger file watcher re-evaluation if Kestrel has cached it.
            pool.GetPredictionEngine(); 
        }

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
}).DisableAntiforgery();

app.MapPost("/api/predict", (PredictionEnginePool<LoanData, LoanPrediction> predictionEnginePool, LoanData input) =>
{
    try
    {
        var prediction = predictionEnginePool.Predict(input);
        
        // Explainability rules
        var explanations = new List<string>();
        
        if (input.CreditScore < 600)
        {
            explanations.Add($"Low Credit Score ({input.CreditScore}) significantly increases default probability.");
        }
        else if (input.CreditScore > 740)
        {
            explanations.Add($"Strong Credit Score ({input.CreditScore}) lowers credit risk.");
        }

        if (input.DebtToIncomeRatio > 0.45f)
        {
            explanations.Add($"High Debt-to-Income ratio ({input.DebtToIncomeRatio * 100:F0}%) reduces repayment capacity.");
        }
        else if (input.DebtToIncomeRatio < 0.20f)
        {
            explanations.Add($"Very low Debt-to-Income ratio ({input.DebtToIncomeRatio * 100:F0}%) shows healthy capacity.");
        }

        if (input.InterestRate > 15f)
        {
            explanations.Add($"High interest rate ({input.InterestRate:F1}%) increases structural default risks.");
        }

        if (input.MonthsEmployed < 18)
        {
            explanations.Add($"Employment tenure is low ({input.MonthsEmployed} months), indicating career stability risk.");
        }
        else if (input.MonthsEmployed > 60)
        {
            explanations.Add("Long term employment shows strong job and income stability.");
        }

        string decision = prediction.PredictedDefault ? "Rejected" : "Approved";
        string message = prediction.PredictedDefault 
            ? "Loan application rejected due to excessive credit risk."
            : "Loan application approved with low default risk.";

        return Results.Ok(new
        {
            prediction.PredictedDefault,
            Probability = prediction.Probability,
            Score = prediction.Score,
            Decision = decision,
            Message = message,
            Explanations = explanations
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            Error = "Prediction engine is unavailable. Please verify a model is trained.", 
            Details = ex.Message 
        });
    }
}).DisableAntiforgery();

app.Run();

public class KaggleIngestRequest
{
    public string DatasetPath { get; set; } = string.Empty;
}

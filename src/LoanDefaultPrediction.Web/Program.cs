using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

app.UseCors();

// Ensure Database is Created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapGet("/", () => Results.Text("Loan Default Prediction API is running. Database initialized!\n\nEndpoints:\n- GET  /api/database-stats\n- POST /api/ingest (multipart/form-data with 'file')"));

app.MapGet("/api/database-stats", async (LoanDbContext dbContext) =>
{
    var totalRecords = await dbContext.LoanRecords.CountAsync();
    var defaultCount = await dbContext.LoanRecords.CountAsync(r => r.Default);
    var activeRun = await dbContext.TrainingRuns.FirstOrDefaultAsync(r => r.IsActive);
    
    return Results.Ok(new
    {
        TotalRecords = totalRecords,
        DefaultCount = defaultCount,
        NonDefaultCount = totalRecords - defaultCount,
        DefaultRate = totalRecords > 0 ? (double)defaultCount / totalRecords : 0.0,
        ActiveModel = activeRun != null ? new { activeRun.Id, activeRun.TrainedAt, activeRun.Accuracy, activeRun.AreaUnderRoc } : null
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

app.MapPost("/api/train", async (ModelTrainingService trainingService) =>
{
    try
    {
        var result = await trainingService.TrainModelAsync();
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
}).DisableAntiforgery();

app.Run();

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoanDefaultPrediction.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecisionRecallConfusionMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoanRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Age = table.Column<float>(type: "REAL", nullable: false),
                    Income = table.Column<float>(type: "REAL", nullable: false),
                    LoanAmount = table.Column<float>(type: "REAL", nullable: false),
                    CreditScore = table.Column<float>(type: "REAL", nullable: false),
                    MonthsEmployed = table.Column<float>(type: "REAL", nullable: false),
                    InterestRate = table.Column<float>(type: "REAL", nullable: false),
                    LoanTerm = table.Column<float>(type: "REAL", nullable: false),
                    DebtToIncomeRatio = table.Column<float>(type: "REAL", nullable: false),
                    Default = table.Column<bool>(type: "INTEGER", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrainedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModelPath = table.Column<string>(type: "TEXT", nullable: false),
                    Accuracy = table.Column<float>(type: "REAL", nullable: false),
                    AreaUnderRoc = table.Column<float>(type: "REAL", nullable: false),
                    F1Score = table.Column<float>(type: "REAL", nullable: false),
                    Precision = table.Column<float>(type: "REAL", nullable: false),
                    Recall = table.Column<float>(type: "REAL", nullable: false),
                    ConfusionMatrixJson = table.Column<string>(type: "TEXT", nullable: false),
                    PfiMetricsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRuns", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanRecords");

            migrationBuilder.DropTable(
                name: "TrainingRuns");
        }
    }
}

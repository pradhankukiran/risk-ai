# RiskAI: Loan Default Prediction and Explainability System

[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg?style=flat-square)](https://dotnet.microsoft.com/en-us/)
[![ML.NET Version](https://img.shields.io/badge/ML.NET-3.0%20%2F%2010.0--preview-blue.svg?style=flat-square)](https://dotnet.microsoft.com/en-us/apps/machinelearning-ai/ml-dotnet)
[![Database](https://img.shields.io/badge/Database-SQLite-green.svg?style=flat-square)](https://www.sqlite.org/)
[![Docker Support](https://img.shields.io/badge/Docker-Supported-blue.svg?style=flat-square)](https://www.docker.com/)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg?style=flat-square)](LICENSE)

RiskAI is a production-grade machine learning system built on ASP.NET Core 10.0 and ML.NET, designed to predict loan default risks and provide real-time model explainability. It features a responsive, flat, light-themed underwriting console, custom in-memory streaming pipelines for Kaggle API datasets, dynamic CSV schema mapping, and global feature importance metrics (PFI).

---

## Architectural Components

The system is split into three primary C# assemblies and a client-side interface:

1. **LoanDefaultPrediction.Common**: Core class library containing the data structures (`LoanData` and `LoanPrediction` classes) and the ML.NET prediction pipeline definitions.
2. **LoanDefaultPrediction.App**: CLI helper application used to train baseline models, evaluate test sets, and generate synthetic baseline loan datasets.
3. **LoanDefaultPrediction.Web**: ASP.NET Core Kestrel Web API server that manages:
   * REST endpoints for loan ingestion, database status, model training, and predictions.
   * Background services (`DatasetDownloadService` & `ModelTrainingService`) for in-memory Kaggle downloads and pipeline retraining.
   * Entity Framework Core tracking for loan records and historical training runs.
4. **Web UI Dashboard**: A lightweight client interface communicating with the Kestrel API, rendering active model metrics (Accuracy, AUC, Precision, Recall), a Confusion Matrix, and Permutation Feature Importance (PFI) bar charts using Chart.js.

---

## Features

* **Zero-Disk In-Memory Kaggle Ingestion**: Pulls datasets directly from the Kaggle REST API, unzips the payload in-memory, and streams the CSV records directly to SQLite (preventing high disk I/O and temp file pollution).
* **Flexible Schema Ingestion**: The parser dynamically maps headers by name (e.g., mapping `dti` or `DTIRatio` to `DebtToIncomeRatio`, and `loan_amnt` to `LoanAmount`), ensuring compatibility with common Kaggle schemas.
* **Database Reset Option**: Admin control to purge database records and trained models to observe cold-start performance metrics.
* **Real-time Explainability**: Computes and displays Permutation Feature Importance (PFI) metrics to show which applicant attributes drive loan defaults.
* **Fixed Glassmorphic Layout**: Responsive UI with a floating brand badge and flat, zero-border-radius aesthetics.

---

## System Configuration

The system uses environment variables for secure, cross-environment credential storage. Locally, it parses a `.env` file at startup.

Create a `.env` file in the root of your project:

```env
KAGGLE_USERNAME=your_kaggle_username
KAGGLE_KEY=your_kaggle_api_key
```

*Note: The `.env` file is excluded from Git commits via `.gitignore` and ignored during Docker builds via `.dockerignore` to prevent accidental credential exposure.*

---

## Local Development Setup

### Prerequisites
* .NET SDK 10.0 or later
* SQLite 3

### Running the Application

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

2. **Run Entity Framework Core migrations** (will automatically run on startup, but can be forced manually):
   ```bash
   dotnet ef database update --project src/LoanDefaultPrediction.Web
   ```

3. **Run the Kestrel Server**:
   ```bash
   dotnet run --project src/LoanDefaultPrediction.Web/LoanDefaultPrediction.Web.csproj --urls http://localhost:5000
   ```

4. Open `http://localhost:5000` in your web browser.

---

## Docker Deployment

The application includes a multi-stage `Dockerfile` configured to compile the assemblies and host the ASP.NET Core runtime.

### 1. Build the Docker Image
```bash
docker build -t loan-default-prediction .
```

### 2. Run the Container (passing credentials and mounting persistent volume)
```bash
docker run -d \
  -p 8080:8080 \
  --env-file .env \
  -v loan_data:/app/data \
  --name loan-app \
  loan-default-prediction
```

The application will be accessible at `http://localhost:8080`.

---

## API Endpoints

### 1. Database Statistics
* **Route**: `GET /api/database-stats`
* **Response**: Returns total record count, default rate distribution, active model metrics, confusion matrix, PFI metrics, and last 5 historical runs.

### 2. Ingest CSV Data
* **Route**: `POST /api/ingest`
* **Payload**: FormFile (`file`)
* **Query Parameters**: `clearExisting` (boolean, default: `true` - purges prior records).

### 3. Kaggle API Ingest
* **Route**: `POST /api/kaggle-ingest`
* **Payload**: `{"datasetPath": "owner/dataset-name", "clearExisting": true}`
* **Response**: JSON ingestion summary with total records processed, validated, and failed.

### 4. Train Model
* **Route**: `POST /api/train`
* **Action**: Initiates the ML.NET LightGBM binary classification pipeline on SQLite records, saves the new model to disk, registers evaluation metrics, and reloads Kestrel's `PredictionEnginePool`.

### 5. Calculate Risk Score
* **Route**: `POST /api/predict`
* **Payload**: JSON applicant parameters (`Age`, `Income`, `LoanAmount`, `CreditScore`, `MonthsEmployed`, `InterestRate`, `LoanTerm`, `DebtToIncomeRatio`).
* **Response**: Predictions, confidence score, recommendation, and local model justifications.

### 6. Reset Database
* **Route**: `POST /api/database-clear`
* **Action**: Purges the `LoanRecords` and `TrainingRuns` tables.

---

## Ingested Data Schema

The CSV parser expects (and maps by column headers) the following numeric fields:

| Column Name | Mapped Alternatives | DataType | Description |
|---|---|---|---|
| **Age** | `age` | `float` | Borrower age (18 - 100) |
| **Income** | `annualincome`, `person_income` | `float` | Annual salary |
| **LoanAmount** | `loan_amnt`, `loan_amount` | `float` | Requested loan principal |
| **CreditScore** | `fico`, `credit_score` | `float` | FICO score (300 - 850) |
| **MonthsEmployed**| `employment`, `employment_length`| `float` | Employment duration |
| **InterestRate** | `loan_int_rate`, `interest_rate` | `float` | Loan annual rate |
| **LoanTerm** | `term`, `loan_term` | `float` | Loan term duration in months |
| **DebtToIncomeRatio**| `dtiratio`, `dti`, `debt_to_income`| `float` | DTI ratio (0.0 - 2.0) |
| **Default** | `loan_default` | `bool` | Default label (0/1, True/False, Yes/No) |

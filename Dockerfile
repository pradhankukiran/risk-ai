# Stage 1: Build the C# solution
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching
COPY LoanDefaultPrediction.slnx ./
COPY src/LoanDefaultPrediction.Common/LoanDefaultPrediction.Common.csproj src/LoanDefaultPrediction.Common/
COPY src/LoanDefaultPrediction.App/LoanDefaultPrediction.App.csproj src/LoanDefaultPrediction.App/
COPY src/LoanDefaultPrediction.Web/LoanDefaultPrediction.Web.csproj src/LoanDefaultPrediction.Web/

# Restore NuGet dependencies
RUN dotnet restore src/LoanDefaultPrediction.Web/LoanDefaultPrediction.Web.csproj

# Copy all source files
COPY src/ src/
COPY model.zip ./

# Publish Web app in Release mode
RUN dotnet publish src/LoanDefaultPrediction.Web/LoanDefaultPrediction.Web.csproj -c Release -o /app/publish --no-restore

# Stage 2: Production runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Create persistent directories for SQLite database and ML models
RUN mkdir -p /app/data /app/models

# Copy published Web build
COPY --from=build /app/publish .

# Bootstrap the active model.zip into Kestrel's runtime folder
COPY --from=build /src/model.zip ./models/active_model.zip

# Set default env configs
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=data/loans.db"

# Mount volume at /app/data for DB persistence
VOLUME /app/data

ENTRYPOINT ["dotnet", "LoanDefaultPrediction.Web.dll"]

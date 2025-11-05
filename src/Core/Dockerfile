# AiKernel Core Orchestration Service

## Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY AiKernel.Core.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . ./

# Build application
RUN dotnet build -c Release --no-restore

# Publish application
RUN dotnet publish -c Release --no-build -o /app/publish

## Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Set entry point
ENTRYPOINT ["dotnet", "AiKernel.Core.dll"]
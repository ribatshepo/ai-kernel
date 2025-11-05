# AiKernel Development Environment

## Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY AiKernel.sln ./

# Copy project files
COPY src/Core/*.csproj ./src/Core/
COPY src/Proxy/*.csproj ./src/Proxy/
COPY src/Secrets/*.csproj ./src/Secrets/
COPY src/Catalog/*.csproj ./src/Catalog/
COPY src/Intelligence/*.csproj ./src/Intelligence/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build the application
RUN dotnet build --no-restore -c Release

# Run tests
RUN dotnet test --no-build -c Release --verbosity normal

# Publish the application
RUN dotnet publish src/Core/AiKernel.Core.csproj -c Release -o /app/publish --no-restore

## Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install required system packages
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r aikernel && useradd -r -g aikernel aikernel

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R aikernel:aikernel /app

# Switch to non-root user
USER aikernel

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose ports
EXPOSE 8080 8081

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080;https://+:8081
ENV ASPNETCORE_HTTPS_PORT=8081

# Start the application
ENTRYPOINT ["dotnet", "AiKernel.Core.dll"]
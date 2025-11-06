using Microsoft.EntityFrameworkCore;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Neo4j.Driver;
using AIKernel.Core.Catalog.Contracts.Interfaces;
using AIKernel.Core.Catalog.Persistence.PostgreSQL;
using AIKernel.Core.Catalog.Persistence.Neo4j;
using AIKernel.Core.Catalog.Persistence.Elasticsearch;
using AIKernel.Core.Catalog.Services;
using AIKernel.Core.Catalog.Events;
using AIKernel.Core.Catalog.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc();

// Add REST API controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AI Kernel Catalog API",
        Version = "v1",
        Description = "Catalog service for managing AI resources, relationships, and metadata"
    });
});

// Configure PostgreSQL
var postgresConnectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? "Host=localhost;Port=5432;Database=aikernel_catalog;Username=aikernel_user;Password=aikernel_pass";

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

// Configure Neo4j
var neo4jUri = builder.Configuration.GetValue<string>("Neo4j:Uri") ?? "bolt://localhost:7687";
var neo4jUser = builder.Configuration.GetValue<string>("Neo4j:User") ?? "neo4j";
var neo4jPassword = builder.Configuration.GetValue<string>("Neo4j:Password") ?? "neo4j_password";
var neo4jDatabase = builder.Configuration.GetValue<string>("Neo4j:Database") ?? "neo4j";

builder.Services.AddSingleton<IDriver>(sp =>
    GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(neo4jUser, neo4jPassword)));

// Configure Elasticsearch
var elasticsearchUri = builder.Configuration.GetValue<string>("Elasticsearch:Uri") ?? "http://localhost:9200";
var elasticsearchIndexName = builder.Configuration.GetValue<string>("Elasticsearch:IndexName") ?? "catalog-resources";

builder.Services.AddSingleton<ElasticsearchClient>(sp =>
{
    var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUri))
        .DefaultIndex(elasticsearchIndexName)
        .DisableDirectStreaming();

    return new ElasticsearchClient(settings);
});

// Register repositories
builder.Services.AddScoped<IResourceRepository, PostgreSqlResourceRepository>();
builder.Services.AddScoped<IRelationshipRepository>(sp =>
{
    var driver = sp.GetRequiredService<IDriver>();
    return new Neo4jGraphRepository(driver, neo4jDatabase);
});
builder.Services.AddScoped<ISearchRepository>(sp =>
{
    var client = sp.GetRequiredService<ElasticsearchClient>();
    return new ElasticsearchSearchRepository(client, elasticsearchIndexName);
});

// Register event infrastructure
builder.Services.AddSingleton<ICatalogEventPublisher, CatalogEventPublisher>();
builder.Services.AddSingleton<SearchIndexSynchronizer>();

// Register version control service
builder.Services.AddScoped<ResourceVersionService>();

// Register catalog service
builder.Services.AddScoped<ICatalogService, CatalogService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnectionString, name: "postgresql")
    .AddElasticsearch(elasticsearchUri, name: "elasticsearch");

var app = builder.Build();

// Initialize event subscribers
using (var scope = app.Services.CreateScope())
{
    // Initialize SearchIndexSynchronizer to subscribe to catalog events
    var _ = scope.ServiceProvider.GetRequiredService<SearchIndexSynchronizer>();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// Map gRPC services
app.MapGrpcService<CatalogGrpcService>();

// Map REST controllers
app.MapControllers();

// Map health checks
app.MapHealthChecks("/health");

// Map gRPC reflection (for tools like grpcurl)
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => "AI Kernel Catalog API - Use /swagger for REST API docs or gRPC reflection for gRPC");
}

app.Run();

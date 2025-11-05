using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapHealthChecks("/health");

app.MapGet("/", () => "AI Kernel Core Orchestration Service - Development Environment Ready");

app.Run();

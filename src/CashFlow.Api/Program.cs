using System.Reflection;
using Asp.Versioning;
using Swashbuckle.AspNetCore.SwaggerUI;
using CashFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add RabbitMQ keyed client
builder.AddKeyedRabbitMQClient(name: "rabbitmq");

builder.AddInfrastructureServices();

// Add services to the container.
builder.Services.AddControllers();

// Configure OpenAPI and Swagger
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "CashFlow API",
        Version = "v1",
        Description = "API para gerenciamento de fluxo de caixa com suporte a processamento assíncrono via RabbitMQ"
    });

    // Add XML comments documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

var app = builder.Build();

app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Add Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CashFlow API v1");
        options.RoutePrefix = "swagger";
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelExpandDepth(2);
        options.DocExpansion(DocExpansion.List);
        options.EnableFilter();
    });
}

app.UseAuthorization();

app.MapControllers();

app.Run();

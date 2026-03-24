using CashFlow.Application;
using CashFlow.Infrastructure;
using CashFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Add RabbitMQ client
builder.AddKeyedRabbitMQClient(name: "rabbitmq");

builder.Services.AddApplicationServices();

builder.AddInfrastructureServices();

builder.Services.AddOutboxDispatcher();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();

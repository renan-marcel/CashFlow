var builder = DistributedApplication.CreateBuilder(args);

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb(pgWeb => pgWeb.WithHostPort(5050));

var cashflowdb = postgres.AddDatabase("cashflowdb");

var migrations = builder.AddProject<Projects.CashFlow_Migrations>("cashflow-migrations")
    .WithReference(cashflowdb)
    .WaitFor(cashflowdb);

var api = builder.AddProject<Projects.CashFlow_Api>("cashflow-api")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(migrations)
    .WithReference(migrations)
    .WithReference(cashflowdb);

var worker = builder.AddProject<Projects.CashFlow_Worker>("cashflow-worker")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(migrations)
    .WithReference(migrations)
    .WithReference(cashflowdb);

builder.Build().Run();

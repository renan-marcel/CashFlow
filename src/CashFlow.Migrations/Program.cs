using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Add logging with detailed EF Core logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add service defaults
builder.AddServiceDefaults();

// Add PostgreSQL DbContext
builder.AddNpgsqlDbContext<CashFlowDbContext>(connectionName: "cashflowdb");

var host = builder.Build();

// Get logger and services
using var scope = host.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("🔄 Iniciando execução de migrations do Entity Framework...");
    logger.LogInformation("📍 Ambiente: {Environment}", host.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

    // Get DbContext
    var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();

    // Test database connection
    try
    {
        logger.LogInformation("🔗 Testando conexão com o banco de dados...");
        var canConnect = await dbContext.Database.CanConnectAsync();

        if (!canConnect)
        {
            logger.LogError("❌ Não foi possível conectar ao banco de dados");
            logger.LogError("⚠️  Verifique:");
            logger.LogError("   1. PostgreSQL está rodando?");
            logger.LogError("   2. Connection string está correta? (appsettings.json)");
            logger.LogError("   3. Banco de dados 'cashflow' existe?");
            logger.LogError("   4. Usuário postgres tem permissão?");
            Environment.Exit(1);
        }

        logger.LogInformation("✅ Conexão com banco de dados estabelecida!");
    }
    catch (Exception connEx)
    {
        logger.LogError(connEx, "❌ Erro ao conectar ao banco de dados");
        logger.LogError("📍 Mensagem: {Message}", connEx.Message);
        logger.LogError("⚠️  Verifique a configuração em appsettings.json");
        Environment.Exit(1);
    }

    // Get applied migrations
    try
    {
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
        logger.LogInformation($"📋 Migrations aplicadas: {appliedMigrations.Count}");
        foreach (var migration in appliedMigrations)
        {
            logger.LogInformation($"   ✅ {migration}");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("⚠️  Erro ao ler migrations aplicadas: {Message}", ex.Message);
        logger.LogInformation("   (Isso é normal na primeira vez)");
    }

    // Get pending migrations
    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

    if (pendingMigrations.Count == 0)
    {
        logger.LogInformation("✅ Banco de dados está atualizado. Nenhuma migration pendente.");
        Environment.Exit(0);
    }

    logger.LogInformation($"📋 Migrations pendentes: {pendingMigrations.Count}");
    foreach (var migration in pendingMigrations)
    {
        logger.LogInformation($"   ⏳ {migration}");
    }

    logger.LogInformation("🚀 Aplicando migrations...");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("✅ Migrations aplicadas com sucesso!");

    logger.LogInformation("🎉 Executor de migrations finalizado com sucesso!");
    Environment.Exit(0);
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Erro crítico ao executar migrations: {ErrorMessage}", ex.Message);
    logger.LogError("📋 Stack Trace: {StackTrace}", ex.StackTrace);

    if (ex.InnerException != null)
    {
        logger.LogError("📎 Inner Exception: {InnerException}", ex.InnerException.Message);
        logger.LogError("   Stack: {InnerStack}", ex.InnerException.StackTrace);
    }

    logger.LogError("\n⚠️  DIAGNÓSTICO:");
    logger.LogError("   1. Verifique se PostgreSQL está rodando");
    logger.LogError("   2. Verifique appsettings.json - connection string");
    logger.LogError("   3. Verifique se banco 'cashflow' existe");
    logger.LogError("   4. Verifique permissões do usuário 'postgres'");

    Environment.Exit(1);
}

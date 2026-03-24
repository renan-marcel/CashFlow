using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Factory for creating CashFlowDbContext instances
/// Required for Entity Framework Core design-time tools (migrations)
/// </summary>
public class CashFlowDbContextFactory : IDesignTimeDbContextFactory<CashFlowDbContext>
{
    /// <summary>
    /// Creates a new instance of CashFlowDbContext for design-time operations
    /// </summary>
    /// <param name="args">Command-line arguments (unused)</param>
    /// <returns>A configured instance of CashFlowDbContext</returns>
    public CashFlowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CashFlowDbContext>();

        // Use PostgreSQL with connection string from environment or default
        var connectionString = GetConnectionString();
        optionsBuilder.UseNpgsql(connectionString);

        return new CashFlowDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Gets the connection string from environment variables or appsettings.json
    /// </summary>
    /// <returns>The PostgreSQL connection string</returns>
    private static string GetConnectionString()
    {
        // Try to get from environment variable first
        var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__cashflowdb");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        // Try to get from appsettings.json
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (File.Exists(configPath))
        {
            try
            {
                var configJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
                if (configJson.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
                {
                    if (connectionStrings.TryGetProperty("cashflowdb", out var cashflowdb))
                    {
                        return cashflowdb.GetString() ?? GetDefaultConnectionString();
                    }
                }
            }
            catch
            {
                // If parsing fails, use default
            }
        }

        return GetDefaultConnectionString();
    }

    /// <summary>
    /// Gets the default connection string for local development
    /// </summary>
    /// <returns>Default PostgreSQL connection string</returns>
    private static string GetDefaultConnectionString()
    {
        return "Host=localhost;Database=cashflow;Username=postgres;Password=postgres;Port=5432";
    }
}

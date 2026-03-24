using CashFlow.Application.Ledger;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using CashFlow.Infrastructure.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CashFlow.Infrastructure
{

    /// <summary>
    /// Dependency injection configuration for Infrastructure layer.
    /// </summary>
    public static class DependencyInjection
    {
        public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
        {

            builder.AddNpgsqlDbContext<CashFlowDbContext>(connectionName: "cashflowdb");

            // Registrar validadores
            builder.Services.AddScoped<IValidator<LedgerEntry>, LedgerEntryValidator>();
            builder.Services.AddScoped<IValidator<DailyBalance>, DailyBalanceValidator>();

            builder.Services.AddScoped<ILedgerEntryApplicationService, LedgerEntryApplicationService>();
            builder.Services.AddScoped<IDailyBalanceQueryService, DailyBalanceQueryService>();
            builder.Services.AddScoped<LedgerConsolidationService>();
            builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

            return builder;
        }

        public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services)
        {
            services.AddHostedService<Outbox.OutboxDispatcherBackgroundService>();
            return services;
        }
    }
}

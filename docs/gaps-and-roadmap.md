# Lacunas, Riscos e Roadmap

Documento que analisa limitações atuais, riscos identificados e plano de evolução da solução.

---

## 📊 Sumário Executivo

| Categoria | Quantidade | Prioridade Alta |
|-----------|-----------|-----------------|
| **Lacunas funcionais** | 8 | 3 |
| **Riscos operacionais** | 6 | 2 |
| **Débitos técnicos** | 5 | 2 |

**Impacto potencial em produção:** 🟡 MÉDIA  
**Urgência de correção:** 🔴 ALTA (autenticação), 🟡 MÉDIA (resilência)  
**Custo estimado de correção:** 3-4 semanas (sprints paralelas)

---

## 🚨 Lacunas Críticas (Must-Fix antes de Produção)

### 1. **Sem Autenticação e Autorização**

**Severidade:** 🔴 CRÍTICA  
**Impacto:** Endpoints completamente públicos; qualquer client pode criar/ler lançamentos  
**Evidência:** [LedgerEntriesController](../src/CashFlow.Api/Controllers/LedgerEntriesController.cs) — sem `[Authorize]`

#### Consequências

- ❌ Qualquer pessoa acessa API (sem token validation)
- ❌ Sem isolamento de dados por merchant/usuário
- ❌ Sem auditoria de quem fez transação
- ❌ Violação de compliance (LGPD, PCI-DSS se houver dados financeiros reais)

#### Plano de Mitigação (v1.1)

```csharp
// PLANEJADO: Adicionar OAuth2/JWT
services.AddAuthentication("Bearer")
    .AddJwtBearer(options => {
        options.Authority = "https://seu-sso.com";
        options.TokenValidationParameters.ValidateAudience = true;
    });

// PLANEJADO: Proteger endpoints
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ledger-entries")]
[Authorize(Roles = "Ledger.Write")]  // <-- NOVO
public class LedgerEntriesController
{
    [HttpPost]
    [Authorize(Roles = "Ledger.Write")]
    public async Task<ActionResult<CreateLedgerEntryResponse>> Create(
        CreateLedgerEntryRequest request)
    {
        var merchantId = User.FindFirst("merchant_id")?.Value;
        if (!Guid.TryParse(merchantId, out var actualMerchantId))
            return Unauthorized();
        
        if (actualMerchantId != request.MerchantId)
            return Forbid(); // Isolamento: só pode acessar seu merchant
        
        // ... resto da lógica
    }
}
```

**Esforço:** 2-3 dias  
**Dependências:** Provedor SSO (Keycloak, Auth0, Azure AD)  
**Bloqueador:** Nenhum (implementação direta)

---

### 2. **Sem Rate Limiting**

**Severidade:** 🔴 CRÍTICA  
**Impacto:** Vulnerável a DDoS; cliente malicioso pode sobrecarregar API  
**Evidência:** Sem middleware de limiting em [Program.cs](../src/CashFlow.Api/Program.cs)

#### Consequências

- ❌ Sem proteção contra flood (1000 req/s mata banco de dados)
- ❌ SLA para clientes legítimos quebrado facilmente
- ❌ Custo cloud cresce sem limitar (se rodar em cloud)

#### Plano de Mitigação (v1.1)

```csharp
// PLANEJADO: Polly + AddRateLimiting
services.AddRateLimiter(options => {
    options.GlobalLimiter ??= PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Request.Headers["Merchant-ID"].ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,        // 100 req
                Window = TimeSpan.FromSeconds(60)  // por minuto
            }
        ));
});

app.UseRateLimiter(); // Middleware
```

**Alternativa com Polly:**
```csharp
var policy = Policy
    .Handle<HttpRequestException>()
    .Or<OperationCanceledException>()
    .Bulkhead(10)  // Max 10 concurrent
    .Wrap(Policy.Timeout(TimeSpan.FromSeconds(5)));
```

**Esforço:** 1-2 dias  
**Custos:** Sem custo adicional (libs incluídas)

---

### 3. **Sem Health Checks para Dependências**

**Severidade:** 🟡 ALTA  
**Impacto:** Impossível detectar quando DB ou RabbitMQ caem (API continua respondendo)  
**Evidência:** `Program.cs` — sem `AddHealthChecks()` em produção

#### Consequências

- ❌ Lançamentos são persistidos mas não consolidados (acumulam na fila)
- ❌ Alerts lentos — detecta problema após timeout user (30s+)
- ❌ Load balancer continua roteando para serviço "morto"

#### Plano de Mitigação (v1.1)

```csharp
// PLANEJADO: Health checks para DB e RabbitMQ
services.AddHealthChecks()
    .AddDbContextCheck<CashFlowDbContext>(name: "postgres-db")
    .AddRabbitMQ(connectionString, name: "rabbitmq");

app.MapHealthChecks("/health", new HealthCheckOptions {
    ResponseWriter = WriteResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions {
    Predicate = (check) => check.Tags.Contains("ready")
});
```

**Integração com Kubernetes:**
```yaml
# PLANEJADO: deployment.yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5189
  initialDelaySeconds: 10
  periodSeconds: 5

readinessProbe:
  httpGet:
    path: /health/ready
    port: 5189
  initialDelaySeconds: 5
  periodSeconds: 10
```

**Esforço:** 1 dia  
**Dependências:** AspNetCore.Diagnostics.HealthChecks (NuGet)

---

## ⚠️ Lacunas Funcionais (Importante)

### 4. **Sem Circuit Breaker para RabbitMQ**

**Severidade:** 🟡 MÉDIA  
**Impacto:** Falha prolongada no RabbitMQ causa retry infinito, congestão  
**Evidência:** [OutboxDispatcher](../src/CashFlow.Infrastructure/Outbox/OutboxDispatcherBackgroundService.cs) — loop simples sem circuit

#### Cenário problemático

```
1. RabbitMQ fica indisponível (1 hora)
2. OutboxDispatcher tenta publicar → falha → log + continua
3. Acumula 1200 mensagens (3s * 1800 tentativas)
4. Storage Outbox explode
5. Quando RabbitMQ volta, envia 1200 de uma vez
6. Consolidador leva horas para processar
```

#### Plano de Mitigação (v1.2)

```csharp
// PLANEJADO: Polly Circuit Breaker
var circuitBreakerPolicy = Policy
    .Handle<IOException>()
    .Or<HttpRequestException>()
    .CircuitBreaker(
        handledEventsAllowedBeforeBreaking: 5,  // 5 erros → abre
        durationOfBreak: TimeSpan.FromSeconds(30),  // aguarda 30s
        onBreak: (outcome, duration) => 
            logger.LogError($"Circuit quebrado por {duration.TotalSeconds}s")
    );

var retryPolicy = Policy
    .Handle<IOException>()
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),  // exponential backoff
        onRetry: (outcome, duration, attempt) =>
            logger.LogWarning($"Tentativa {attempt} após {duration.TotalSeconds}s")
    );

var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

var result = await combinedPolicy.ExecuteAsync(
    async () => await publisher.PublishAsync(message)
);
```

**Estados do Circuit:**

```
Closed (Normal)
  ↓ (5 erros)
Open (Rejeitando)
  ↓ (30s passados)
Half-Open (Testando)
  ↓ (sucesso)
Closed (Recuperado)
```

**Esforço:** 2-3 dias  
**Dependências:** Polly NuGet package

---

### 5. **Sem Cache de Saldos Diários**

**Severidade:** 🟡 MÉDIA  
**Impacto:** Cada GET /daily-balances hits banco de dados even para leitura (N+1)  
**Evidência:** [DailyBalanceQueryService](../src/CashFlow.Infrastructure/Services/DailyBalanceQueryService.cs) — sem cache

#### Análise de performance

```
Cenário: 100 clientes simultâneos consultando saldo do mesmo merchant/date

Sem cache:
  - 100 queries ao PostgreSQL (SELECT daily_balance WHERE ...)
  - 100ms DB latency * 100 = potencial timeout em shared pool
  - Scalabilidade: O(n clientes)

Com cache Redis (1 hora TTL):
  - Query 1: Hit DB (100ms) + cache (10ms) = 110ms total
  - Queries 2-100: Hit cache (10ms) = 10ms total
  - Escalabilidade: O(1) after first hit
```

#### Plano de Mitigação (v1.2)

```csharp
// PLANEJADO: Redis caching decorator
public class CachedDailyBalanceQueryService : IDailyBalanceQueryService
{
    private readonly IDailyBalanceQueryService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedDailyBalanceQueryService> _logger;

    public async Task<DailyBalanceDto?> GetAsync(GetDailyBalanceQuery query, CancellationToken ct)
    {
        var cacheKey = $"daily-balance:{query.MerchantId}:{query.Date:yyyy-MM-dd}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        
        if (cached != null)
        {
            _logger.LogInformation("Cache hit: {CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<DailyBalanceDto>(cached);
        }

        var result = await _inner.GetAsync(query, ct);
        
        if (result != null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                },
                ct
            );
        }

        return result;
    }
}

// PLANEJADO: Registrar em DependencyInjection
services.AddStackExchangeRedisCache(options => {
    options.Configuration = configuration.GetConnectionString("redis");
});
services.Decorate<IDailyBalanceQueryService, CachedDailyBalanceQueryService>();
```

**Invalidação de cache:**

```csharp
// Quando LedgerConsolidationService atualiza saldo:
await _cache.RemoveAsync($"daily-balance:{merchantId}:{date:yyyy-MM-dd}");
```

**Esforço:** 2-3 dias  
**Custo:** Redis container (free em Aspire) ou Redis Cloud  
**ROI:** 10x performance improvement em reads

---

### 6. **Sem Limpeza Periódica de Outbox**

**Severidade:** 🟡 MÉDIA  
**Impacto:** Tabela `outbox_messages` cresce indefinidamente (sem cleanup)  
**Evidência:** [OutboxDispatcher](../src/CashFlow.Infrastructure/Outbox/OutboxDispatcherBackgroundService.cs) — não deleta após processamento

#### Análise de crescimento

```
Cenário: 50 req/s * 60s * 60m * 24h = 4.32M mensagens/dia

SEM cleanup:
  - 1 mês = 129.6M registros
  - 1 ano = 1.5B registros
  - DB query performance degrada (table scans lentos)

COM cleanup (30 dias):
  - Keep only last 30 dias de processado
  - Max ~129.6M registros
  - Índices mantem tamanho pequeno
```

#### Plano de Mitigação (v1.1)

```csharp
// PLANEJADO: Background service de cleanup
public class OutboxCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly int _retentionDays = 30;
    private readonly int _cleanupIntervalSeconds = 3600; // 1 hora

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante cleanup de outbox");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_cleanupIntervalSeconds),
                stoppingToken
            );
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
        var deletedCount = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc.HasValue && m.ProcessedAtUtc < cutoffDate)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Removidos {DeletedCount} mensagens de outbox antigas",
            deletedCount
        );
    }
}

// registrar em Program.cs
services.AddHostedService<OutboxCleanupService>();
```

**Alternativamente, com tabela particionada (PostgreSQL):**

```sql
-- PLANEJADO: Particionamento por mês
CREATE TABLE outbox_messages_2026_03 PARTITION OF outbox_messages
    FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');

-- Cleanup é apenas DROP PARTITION (rápido)
ALTER TABLE outbox_messages DROP PARTITION outbox_messages_2026_01;
```

**Esforço:** 1-2 dias  
**ROI:** Mantém DB performance estável

---

### 7. **Sem Auditoria de Transações**

**Severidade:** 🟡 MÉDIA  
**Impacto:** Impossível rastrear quem criou/modificou registro (compliance)  
**Evidência:** `LedgerEntry` — campos created_by, created_at desconhecidos

#### Plano de Mitigação (v1.2)

```csharp
// PLANEJADO: Adicionar campos de auditoria
public class LedgerEntry : Entity
{
    public Guid Id { get; private set; }
    public Guid MerchantId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    
    // NOVO:
    public Guid CreatedByUserId { get; private set; }  // Extracted from JWT
    public DateTime CreatedAtUtc { get; private set; }
    public string? CreatedByIpAddress { get; private set; }  // Request IP
    
    // Soft delete:
    public bool IsDeleted { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
}

// Migration:
migrationBuilder.AddColumn<Guid>(
    name: "created_by_user_id",
    table: "ledger_entries",
    type: "UUID",
    nullable: false
);

migrationBuilder.AddColumn<timestamp>(
    name: "created_at_utc",
    table: "ledger_entries",
    nullable: false,
    defaultValueSql: "NOW()"
);

migrationBuilder.CreateIndex(
    name: "ix_ledger_entries_created_by_user_id",
    table: "ledger_entries",
    column: "created_by_user_id"
);
```

**Captura de contexto na API:**

```csharp
var createdByUserId = User.FindFirst("sub")?.Value;  // JWT subject
var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

var entry = LedgerEntry.Create(
    merchantId: request.MerchantId,
    type: request.Type,
    amount: request.Amount,
    createdByUserId: Guid.Parse(createdByUserId),
    createdByIpAddress: ipAddress
);
```

**Esforço:** 2-3 dias  
**Dependências:** Sistema de usuários (via autenticação)

---

### 8. **Testes E2E Faltando**

**Severidade:** 🟡 MÉDIA  
**Impacto:** Sem validação de fluxo completo (lançamento → consolidação → query)  
**Evidência:** Pasta `tests/CashFlow.IntegrationTests/` — apenas service tests, não E2E HTTP

#### Plano de Mitigação (v1.2)

```csharp
// PLANEJADO: Testes E2E via HttpClient
public class LedgerFlowE2ETests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    // Override DB com InMemory para testes
                    var descriptor = services.SingleOrDefault(d =>
                        d.ServiceType == typeof(DbContextOptions<CashFlowDbContext>)
                    );
                    services.Remove(descriptor);
                    services.AddDbContext<CashFlowDbContext>(o =>
                        o.UseInMemoryDatabase("test-cashflow")
                    );
                });
            });

        _client = _factory.CreateClient();
        _client.BaseAddress = new Uri("http://localhost:5189");
    }

    [Fact]
    public async Task CompleteFlow_CreateLedger_ConsolidateBalance_QueryBalance()
    {
        var merchantId = Guid.NewGuid();
        
        // 1. CREATE
        var createRequest = new CreateLedgerEntryRequest
        {
            MerchantId = merchantId,
            Type = "credit",
            Amount = 100.00m,
            OccurredAt = DateTime.UtcNow,
            IdempotencyKey = "txn-001"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/ledger-entries",
            createRequest
        );
        
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createResult = await createResponse.Content.ReadAsAsync<CreateLedgerEntryResponse>();
        Assert.NotEqual(Guid.Empty, createResult.LedgerEntryId);

        // 2. WAIT for consolidation
        await Task.Delay(TimeSpan.FromSeconds(4));  // Outbox dispatcher runs every 3s

        // 3. QUERY
        var queryResponse = await _client.GetAsync(
            $"/api/v1/daily-balances?merchantId={merchantId}&date={DateTime.Today:yyyy-MM-dd}"
        );

        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        var balance = await queryResponse.Content.ReadAsAsync<DailyBalanceResponse>();
        Assert.Equal(100.00m, balance.Balance);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        _client?.Dispose();
    }
}
```

**Esforço:** 2-3 dias  
**Cobertura adicional:** ~10-15 E2E test cases

---

## 🎯 Riscos Operacionais

### Risco 1: Latência de 3 Segundos em Consolidação

**Risco:** Clientes esperam até 3s antes de saldo aparecer após lançamento  
**Probabilidade:** 🟠 ALTA (acontece toda vez)  
**Impacto:** 🟡 MÉDIO (expectativa vs. realidade)  

**Exemplo:**

```
14:00:00.000 - Client: POST /ledger-entries com $100
14:00:00.050 - API: Persistido com sucesso, retorna 201

14:00:00.100 - Client: GET /daily-balances
14:00:00.150 - API: Saldo = $0 (ainda não consolidado!)

14:00:03.000 - OutboxDispatcher: Publica evento para RabbitMQ
14:00:03.100 - Worker: Consome e consolida, atualiza saldo para $100

14:00:03.150 - Client: GET /daily-balances
14:00:03.200 - API: Saldo = $100 ✅
```

**Mitigação:**

1. **Documentar expectativa**: Readme deixa claro "até 3s latência"
2. **Melhorar para real-time** (v1.3): Substituir Outbox por Kafka streaming
3. **Otimizar para 1s** (v1.2): Reduzir intervalo OutboxDispatcher para 1s

---

### Risco 2: Perda de Mensagens acima de 5 Retentativas

**Risco:** Se RabbitMQ falhar 5+ vezes, mensagem é abandonada silenciosamente  
**Probabilidade:** 🟠 MÉDIA (produção intermitivamente falha)  
**Impacto:** 🔴 ALTA (débito/crédito perdido permanentemente)

**Análise:**

```
Cenário: RabbitMQ intermitente (5 falhas em 15 minutos)
1. Mensagem fica em Outbox com attempts = 5
2. OutboxDispatcher não retenta mais (max 5)
3. Saldo nunca consolida
4. Lançamento registo mas saldo = 0 forever
```

**Mitigação (v1.1):**

```csharp
// NÃO abandonar após 5 retentativas; ao invés:
// 1. Alertar (Slack/PagerDuty)
// 2. Manter tentando indefinidamente
// 3. Implementar Dead Letter Queue (DLQ)

public class OutboxDispatcherBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(...)
    {
        foreach (var message in pendingMessages)
        {
            try
            {
                await _publisher.PublishAsync(message);
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex) when (message.Attempts >= 5)
            {
                // NOVO: Instead of giving up, send to DLQ
                await _dlqService.SendToDeadLetterAsync(message);
                _alerting.SendSlackAlert($"Mensagem {message.Id} em DLQ");
                
                // Marcar como processada (mover para DLQ)
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                _logger.LogError(ex, "Falha ao publicar #{Attempt}", message.Attempts);
            }
        }
    }
}
```

---

### Risco 3: Desempenho sob Pico de 50 req/s

**Risco:** Comportamento desconhecido quando atinge 50 req/s sustentado  
**Probabilidade:** 🟠 ALTA (requisito de escalabilidade)  
**Impacto:** 🟡 MÉDIO (SLA quebra ou DB lentifica)

**Análise:**

```
50 req/s = 50 lançamentos criados/segundo
Outbox: 16.67 msg/s (50 msg a cada 3s)
RabbitMQ: PublishAsync deve processar 50/s without blocking

Potencial gargalo:
- PostgreSQL: CREATE ledger_entry + INSERT outbox_message + index update = ~5-10ms?
  * 50 * 10ms = 500ms CPU per batch (aceitável)
  
- RabbitMQ: Publish 50 msg/s
  * RabbitMQ handles 50K msg/s easily (não é issue)

Teste de carga necessário.
```

**Mitigação (v1.2):**

```csharp
// PLANEJADO: Load test com k6
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 50,           // 50 virtual users
  duration: '5m',    // 5 minutos
  thresholds: {
    'http_req_duration': ['p(95)<500'],  // 95th percentile < 500ms
    'http_req_failed': ['rate<0.05'],    // failure rate < 5%
  },
};

export default function () {
  const url = 'http://localhost:5189/api/v1/ledger-entries';
  const payload = JSON.stringify({
    merchantId: '11111111-1111-1111-1111-111111111111',
    type: 'credit',
    amount: Math.random() * 1000,
    occurredAt: new Date().toISOString(),
    idempotencyKey: `txn-${Date.now()}-${Math.random()}`
  });

  const res = http.post(url, payload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(res, {
    'status is 201': (r) => r.status === 201,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });

  sleep(1);  // 1 second between requests
}
```

---

### Risco 4: Possibilidade de Saldo Negativo em DailyBalance

**Risco:** Validador permite débito que deixa saldo negativo (regra de negócio violada)  
**Probabilidade:** 🟢 BAIXA (validador está em lugar)  
**Impacto:** 🔴 ALTA (integridade de dados)

**Evidência:** [DailyBalanceValidator](../src/CashFlow.Domain/Ledger/Validators/DailyBalanceValidator.cs) — válida `Balance >= 0`

**Análise:**

```
Cenário: Dois débitos simultâneos
14:00:00 - Saldo = $100
14:00:00.001 - Débito 1 ($80) inicia consolidação → $20
14:00:00.002 - Débito 2 ($50) inicia consolidação → $-30 ❌

Sem controle de concorrência, ambas transações podem suceder.
```

**Mitigação (v1.1):**

```csharp
// OPÇÃO A: PESSIMISTIC LOCKING
var daily = await dbContext.DailyBalances
    .FromSqlInterpolated($"SELECT * FROM daily_balances WHERE id = {id} FOR UPDATE")
    .FirstOrDefaultAsync();

// OPÇÃO B: Optimistic Locking com RowVersion
public class DailyBalance : Entity
{
    [Timestamp]  // EF Core attribute
    public byte[] RowVersion { get; set; }
    
    public decimal Balance { get; set; }
    // ...
}

// EF Core automatically adds WHERE ... AND RowVersion = @oldRowVersion
// Se falhar, retry com versão nova

// OPÇÃO C: SERIALIZABLE ISOLATION LEVEL
var transaction = await dbContext.Database.BeginTransactionAsync(
    IsolationLevel.Serializable
);
```

**Esforço:** 1-2 dias  
**Recomendação:** Usar Optimistic Locking (menos bloqueio, melhor throughput)

---

### Risco 5: Falta de Monitoramento em Produção

**Risco:** Nenhum observabilidade de métricas (latência, erros, saturation)  
**Probabilidade:** 🟠 MÉDIA (sem dashboards)  
**Impacto:** 🔴 ALTA (impossível diagnosticar issues em produção)

**Plano de Mitigação (v1.2):**

```csharp
// PLANEJADO: Prometheus metrics
var counter = new Counter(
    name: "cashflow_ledger_entries_created_total",
    help: "Total de lançamentos criados",
    labelNames: new[] { "merchant_id", "type", "status" }
);

public async Task<CreateLedgerEntryResponse> Create(CreateLedgerEntryRequest request)
{
    try
    {
        // ... lógica
        counter.WithLabels(request.MerchantId.ToString(), request.Type, "success").Inc();
        return response;
    }
    catch (Exception ex)
    {
        counter.WithLabels(request.MerchantId.ToString(), request.Type, "error").Inc();
        throw;
    }
}

// PLANEJADO: Grafana dashboard
// - API p95 latenci
// - RabbitMQ queue depth
// - DB connection pool utilization
// - Error rate per endpoint
```

---

### Risco 6: Falta de Disaster Recovery

**Risco:** Sem backup/recovery strategy se banco de dados corrompido  
**Probabilidade:** 🟢 BAIXA (PostgreSQL é resiliente)  
**Impacto:** 🔴 CRÍTICA (perda total de dados)

**Plano de Mitigação (v1.2):**

```yaml
# PLANEJADO: Regular backups com pg_dump
apiVersion: batch/v1
kind: CronJob
metadata:
  name: cashflow-db-backup
spec:
  schedule: "0 2 * * *"  # Diário às 2am
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: backup
            image: postgres:16
            command:
            - /bin/bash
            - -c
            - |
              pg_dump -h $DB_HOST -U $DB_USER $DB_NAME > /backups/cashflow-$(date +%Y%m%d-%H%M%S).sql
              # Upload para S3, Azure Blob, etc.
```

---

## 🛣️ Roadmap (3 Fases)

### Fase 1: MVP → Produção Segura (Sprint 1-2, 2-3 semanas)

| Item | Esforço | Prioridade | Status |
|------|---------|-----------|--------|
| Autenticação OAuth2/JWT | 3 dias | 🔴 CRÍTICA | Planejado |
| Rate limiting | 2 dias | 🔴 CRÍTICA | Planejado |
| Health checks DB+RabbitMQ | 1 dia | 🟡 ALTA | Planejado |
| Limpeza periódica Outbox | 2 dias | 🟡 ALTA | Planejado |
| **Subtotal** | **8 dias** | | |

### Fase 2: Escalabilidade & Confiabilidade (Sprint 3-4, 2-3 semanas)

| Item | Esforço | Prioridade | Status |
|------|---------|-----------|--------|
| Circuit breaker RabbitMQ | 3 dias | 🟡 MÉDIA | Planejado |
| Redis cache de saldos | 3 dias | 🟡 MÉDIA | Planejado |
| Testes E2E | 3 dias | 🟡 MÉDIA | Planejado |
| Load testing (50 req/s) | 2 dias | 🟡 MÉDIA | Planejado |
| Auditoria de transações | 2 dias | 🟡 MÉDIA | Planejado |
| **Subtotal** | **13 dias** | | |

### Fase 3: Observabilidade & Evolução (Sprint 5+, 2+ semanas)

| Item | Esforço | Prioridade | Status |
|------|---------|-----------|--------|
| Prometheus + Grafana | 3 dias | 🟡 MÉDIA | Planejado |
| Disaster recovery / Backups | 2 dias | 🟢 BAIXA | Planejado |
| Multi-tenancy refactor | 5 dias | 🟢 BAIXA | Futuro |
| Event sourcing (opcional) | 5 dias | 🟢 BAIXA | Futuro |
| Migração Kafka (opcional) | 3 dias | 🟢 BAIXA | Futuro |

---

## 📋 Matriz de Risco vs. Impacto

```
Impacto
  ▲
  │ Autenticação  │ Perda mensagens  │ Disaster recovery
  │ Rate limiting │ Saldo negativo   │
  │               │                  │
  ├──────────────────────────────────────────────> Probabilidade
  │ Health checks │ Latência 3s      │ Falta monitoramento
  │ Cleanup       │                  │ Load test faltando
  │
```

---

## 🎯 Recomendações Executivas

1. **NÃO lançar em produção** sem: Autenticação, Rate limiting, Health checks (Fase 1)
2. **Implementar Fase 1** antes de aceitar tráfego real (clientes/dados financeiros)
3. **Executar Fase 2** nos primeiros 2-3 meses em produção (hardening)
4. **Monitorar Fase 3** continuamente (observabilidade)

**Cronograma sugerido:**

- **Semana 1-2:** Fase 1 (MVP → Prod-ready)
- **Semana 3-4:** Fase 2 (Hardening + testes)
- **Semana 5+:** Produção + monitoramento Fase 3

---

**Última atualização:** Março 2026  
**Próxima revisão:** Junho 2026  
**Autor:** Equipe Backend

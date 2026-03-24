# Architecture Decision Records (ADRs)

> **ADR** documentam decisões arquiteturais significativas, contexto, consequências e trade-offs.  
> Format: RFC 5741 (Y-NNNN-DD, Status, Título, Contexto, Decisão, Consequências)

---

## ADR-001: Adotar Padrão Outbox para Garantir Exactly-Once Semantics

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

A api precisa registrar lançamentos (débito/crédito) e notificar o consolidador via mensageria sem perder dados. Em cenários de falha (ex: crash entre persistência e publicação RabbitMQ), há risco de:

1. **Perda de mensagem**: Persistir no DB mas não publicar no MQ (Worker não consolida)
2. **Duplicação**: Publicar no MQ e falhar antes de comittar no DB (retry duplica)
3. **Inconsistência**: Estados diferentes entre DB e MQ

Alternativas:
- **Publicar direto no RabbitMQ** (sem Outbox): Simples mas com risco de perda
- **Saga pattern**: Garante distribuição mas adiciona complexidade (2-phase commit)
- **Use Message Bus abstrado**: Encapsula ambos mas still requires atomicity

### Decisão

Adotar **Outbox Pattern**:

1. Criar tabela `outbox_messages` (payload, routing_key, processed_at_utc, attempts)
2. Persistir `LedgerEntry` + `OutboxMessage` em **mesma transação atômica** no PostgreSQL
3. Background service (`OutboxDispatcher`) processa a cada 3 segundos:
   - Busca mensagens **não processadas** (processed_at_utc IS NULL)
   - Publica no RabbitMQ via keyed DI service
   - Marca `processed_at_utc = NOW()` e comita
4. Em caso de falha de publicação:
   - Não marca como processado
   - Background service retoma no próximo ciclo
   - Incrementa `attempts` (retry limit: 5 attempts)

### Consequências

✅ **Positivas:**
- **Garantia forte**: Atomicidade transacional entre persistência e fila
- **Recuperação automática**: Background service retenta indefinidamente
- **Sem duplicação**: Se evento já processado, tabela raspa idempotência

❌ **Negativas:**
- **Latência**: Até 3 segundos de delay até publicação (aceitável para caso de uso)
- **Sobrecarga DB**: Outbox é mais uma tabela (mitigado com limpeza periódica)
- **Complexidade adicional**: Mais um serviço de background para monitorar

### Referências

- Código: [LedgerEntryApplicationService.CreateAsync()](../src/CashFlow.Infrastructure/Services/LedgerEntryApplicationService.cs#L40-L75)
- Tabela: [OutboxMessage](../src/CashFlow.Infrastructure/Persistence/OutboxMessageConfiguration.cs)
- Dispatcher: [OutboxDispatcherBackgroundService.ExecuteAsync()](../src/CashFlow.Infrastructure/Outbox/OutboxDispatcherBackgroundService.cs#L35-L80)

---

## ADR-002: Consolidação Assíncrona a Cada 3 Segundos com Batch de 50

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Após lançamento persistido, o saldo diário (`daily_balance`) deve ser atualizado. Duas abordagens:

1. **Síncrono (após POST)**: API persiste + calcula + retorna. Simples mas lento (consolidação bloqueante)
2. **Assíncrono (background)**: API persiste + retorna 201; background atualiza saldo. Rápido mas com lag

Requisito funcional: **Suportar até 50 req/s** de lançamentos com **máx 5% de perda**.

- 50 req/s = 180.000 lançamentos/hora
- 5% perda = aceitável retry de ~9.000 mensagens/hora

### Decisão

Usar **consolidação assíncrona com batch de 50**:

1. **OutboxDispatcher** processa a cada **3 segundos** (intervalo)
2. **Fetch**: Busca até **50 mensagens não processadas**
3. **Publish**: Envia lote ao RabbitMQ em paralelo (sem await paralelo, sequencial por safety)
4. **Mark**: Marca todas processadas após sucesso
5. **Retry**: Em falha de publicação, não marca; próximo ciclo retenta

Fórmula de throughput:
- 50 mensagens / 3 segundos = **16,67 msg/s** (conservador)
- À 50 req/s, acumula fila, mas OutboxDispatcher drena em ~3-6 ciclos (9-18s)

### Consequências

✅ **Positivas:**
- **API rápida**: POST retorna 201 em ~10ms (sem aguardar consolidação)
- **Escalável**: Batch processing reduz latência de RabbitMQ vs. um por um
- **Resiliente**: Retry automático até 5 tentativas
- **Observável**: Métricas de fila no Outbox

❌ **Negativas:**
- **Latência**: Até 3 segundos para saldo refletir (aceitável)
- **Perda episódica**: Se 5+ retries falham, mensagem é silenciada (mitigado com alertas)
- **Storage crescente**: Outbox table cresce até limpeza periódica

### Configurações

```csharp
// src/CashFlow.Infrastructure/Outbox/OutboxDispatcherBackgroundService.cs
private const int BatchSize = 50;
private const int ProcessingIntervalMs = 3000; // 3 segundos
private const int MaxRetries = 5;
```

### Referências

- Dispatcher: [OutboxDispatcherBackgroundService](../src/CashFlow.Infrastructure/Outbox/OutboxDispatcherBackgroundService.cs)
- Teste: [Dispatcher_ShouldPublishPendingOutboxMessage](../tests/CashFlow.IntegrationTests/OutboxDispatcherIntegrationTests.cs#L28-L62)

---

## ADR-003: PostgreSQL + Entity Framework Core para Persistência

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Projeto precisa de banco de dados relacional para:
- Transações ACID (atomicidade Outbox)
- Constrainsts (UNIQUE, índices, FK)
- Migrations automáticas
- Cross-platform (Linux Docker, Windows dev)

Alternativas avaliadas:
1. **PostgreSQL + EF Core**: Open source, ACID, Npgsql native, Aspire nativo
2. **SQL Server + EF Core**: Enterprise, ACID, but licença e Windows-only
3. **SQLite + EF Core**: Simples, mas não thread-safe para produçã (WAL mode)
4. **Dapper (minimal ORM)**: Controle, mas sem migrations automáticas

### Decisão

Usar **PostgreSQL 16 + Entity Framework Core 10.0.5**:

1. **ORM**: EF Core para abstração DB-agnostic
2. **Provider**: Npgsql (driver oficial PostgreSQL para .NET)
3. **Migrations**: EF Core Code-First com versionamento automático
4. **Aspire**: Integrateçào nativa (PostgreSQL container + connection string automática)
5. **Índices**: Fluent API para índices (UNIQUE, composite, partial)

### Consequências

✅ **Positivas:**
- **Cross-platform**: Linux (produção), Windows/Mac (dev)
- **ACID forte**: Transações totalmente isoladas
- **Migrations versionadas**: GitOps-friendly (migrations commitadas)
- **Aspire integrado**: PostgreSQL node automático no AppHost
- **Custo zero**: Open source

❌ **Negativas:**
- **Overhead ORM**: Queries podem ser menos otimizadas (mitiga com raw SQL onde needed)
- **Dependência Npgsql**: Outra dependência (mas bem mantida)
- **Configuração inicial**: Setup em dev (mitigado pelo Aspire)

### Configurações

```csharp
// src/CashFlow.Infrastructure/DependencyInjection.cs
services.AddDbContext<CashFlowDbContext>(options =>
    options.UseNpgsql(connectionString)
           .LogTo(Console.WriteLine, LogLevel.Information)
);
```

### Referências

- DbContext: [CashFlowDbContext](../src/CashFlow.Infrastructure/Persistence/CashFlowDbContext.cs)
- Configurações: [LedgerEntryConfiguration](../src/CashFlow.Infrastructure/Persistence/Configurations/LedgerEntryConfiguration.cs)
- Migrations: [20260324125712_InitialCreate.cs](../src/CashFlow.Migrations/Migrations/20260324125712_InitialCreate.cs)

---

## ADR-004: Idempotência via IdempotencyKey + ProcessedIntegrationEvent

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Em sistemas distribuídos, retries de requisições podem causar duplicação:

1. **Lançamento duplicado**: Cliente retenta POST /ledger-entries com mesmo `IdempotencyKey`
2. **Consolidação duplicada**: RabbitMQ reenvia mensagem (timeout/nack)

Abordagens:
- **Sem proteção**: Simples, mas duplicação não rastreada
- **Cache distribuído** (Redis): Rápido, mas single point of failure
- **Double-write** (DB duplo): Tabelas de idempotência + tracking

### Decisão

Usar **Double-Write com duas tabelas separadas**:

#### 1. **IdempotencyKey** (para lançamentos)

```sql
ALTER TABLE ledger_entries ADD CONSTRAINT uk_ledger_entry_idempotency 
  UNIQUE (merchant_id, idempotency_key);
```

- API recebe POST com `idempotencyKey` único
- Se chave existe, retorna `isDuplicate: true` com entrada anterior
- Protege contra retries acidentais de clientes

#### 2. **ProcessedIntegrationEvent** (para consolidação)

```sql
CREATE TABLE processed_integration_events (
  id UUID PRIMARY KEY,
  event_id TEXT UNIQUE NOT NULL,
  processed_at_utc TIMESTAMP NOT NULL
);
```

- Worker registra cada evento processado
- Se evento `EventId` duplicado, ignora
- Garante exactly-once consolidação

### Consequências

✅ **Positivas:**
- **Sem cache externo**: Idempotência no DB (durável)
- **Dupla proteção**: Lançamentos + eventos rastreados
- **Auditoria**: Histórico de processamento claro

❌ **Negativas:**
- **Queries extras**: Dois checks por transação (mitigado com índices UNIQUE)
- **Storage**: Duas tabelas de rastreamento (aceitável)

### Referências

- Lançamento: [LedgerEntryApplicationService.CreateAsync()](../src/CashFlow.Infrastructure/Services/LedgerEntryApplicationService.cs#L42-L50)
- Consolidação: [LedgerConsolidationService.ApplyAsync()](../src/CashFlow.Infrastructure/Services/LedgerConsolidationService.cs#L30-L45)
- Teste: [CreateAsync_WithSameIdempotencyKey_ShouldNotDuplicate](../tests/CashFlow.IntegrationTests/LedgerEntryApplicationServiceIntegrationTests.cs#L78-L105)

---

## ADR-005: Background Service Pattern para Consolidação (vs. Event Streaming Direto)

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Após publicação no RabbitMQ, há duas formas de consumir:

1. **Streaming direto** (Handler inline): Subscriber busca evento e processa imediatamente
2. **Background Service** (Worker): Serviço dedicado polling do MQ periodicamente

Requisitos:
- API deve estar pronta mesmo se Worker falhar
- Consolidação pode ter até 3s latência
- Facilmente escalável (múltiplos Workers)

### Decisão

Usar **Background Service (Worker) separado**:

1. **CashFlow.Worker**: `.ReadAsHostedService()`
2. **RabbitMQ Channel**: Consumer com prefetch = 10 (QoS)
3. **Processing**: Cada evento→ LedgerConsolidationService.ApplyAsync()
4. **Ack/Nack**: BasicAckAsync após sucesso; BasicNackAsync com requeue=true em falha

```csharp
// src/CashFlow.Worker/Worker.cs
private async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.Received += async (model, ea) =>
    {
        try {
            await consolidationService.ApplyAsync(/*...*/); // <-- PROCESS
            channel.BasicAckAsync(ea.DeliveryTag);
        } catch {
            channel.BasicNackAsync(ea.DeliveryTag, requeue: true); // <-- RETRY
        }
    };
}
```

### Consequências

✅ **Positivas:**
- **Desacoplamento completo**: API continua mesmo se Worker morre
- **Escalabilidade**: Múltiplos Workers leem mesma fila (round-robin)
- **Resiliência**: Worker falha não afetam lançamentos
- **Observabilidade**: Worker é um serviço separado, logs isolados

❌ **Negativas:**
- **Latência**: Até 3s entre lançamento e consolidação visível
- **Complexidade**: Serviço adicional para deployar/monitorar
- **Eventual consistency**: Cliente não vê consolidação imediatamente em GET

### Alternativa não adotada

**Event Streaming direto** (publicar e esperar response):
```csharp
// ❌ Não feito aqui
await publisher.PublishAsync(evt);  // Bloqueia até confirmação
saldoConsolidado = evt.NovoSaldo;   // Imediato mas acoplado
```

### Referências

- Worker: [Worker.cs](../src/CashFlow.Worker/Worker.cs#L18-L78)
- Service: [LedgerConsolidationService.ApplyAsync()](../src/CashFlow.Infrastructure/Services/LedgerConsolidationService.cs)
- Teste resilência: [CreateAsync_WhenWorkerIsDown_ShouldStillPersistEntry](../tests/CashFlow.IntegrationTests/LedgerEntryApplicationServiceIntegrationTests.cs#L107-L132)

---

## ADR-006: Sem Autenticação/Autorização v1 (Segurança Adiada)

**Status:** ⏳ **ACEITO COM DEBT** | Decisão (com plano de melhoria)  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Requisitos funcionais prioritários:
- Lançamentos e consolidação operacionais
- Padrão Outbox and exactly-once
- Escalabilidade até 50 req/s

Segurança é importante mas foi priorizado após MVP:
- Sem contexto de múltiplos merchants em dev/test
- Sem requisito de isolamento de dados confidenciais (PoC)

### Decisão

**Versão 1.0 lança SEM autenticação/autorização**:

1. Endpoints públicos (sem token validation)
2. Sem rate limiting por merchantId
3. Sem auditoria de quem chamou API

Plano para v1.1 (roadmap curto).

### Consequências

✅ **Positivas:**
- **Prototipagem rápida**: Foco em lógica de negócio
- **Deploy simplificado**: Sem OAuth provider externo no MVP

❌ **Negativas:**
- 🔴 **Segurança BAIXA**: Endpoints exploráveis em produção
- **Sem auditoria**: Quem criou o lançamento fica desconhecido
- **Sem isolamento**: Qualquer client vê todos os merchants

### Plano de Melhoria

**v1.1 — Autenticação OAuth2/JWT:**
```csharp
// Planejado:
services.AddAuthentication("Bearer")
    .AddJwtBearer(o => o.Authority = "https://keycloak:8443");
    
[Authorize(Roles = "LedgerWrite")] // Proteção por role
public async Task<CreateLedgerEntryResponse> Create()
```

**v1.2 — Autorização e multi-tenancy:**
```csharp
// Planejado:
var merchantId = User.FindFirst("merchant_id")?.Value;  // Extrair do token
// Validar merchantId do token == request.MerchantId (isolamento)
```

### Referências

- Controllers: [LedgerEntriesController.cs](../src/CashFlow.Api/Controllers/LedgerEntriesController.cs) (sem [Authorize])
- Roadmap: [docs/gaps-and-roadmap.md](gaps-and-roadmap.md#v11---autenticação)

---

## ADR-007: RabbitMQ com Direct Exchange + Routing Keys (vs. Kafka)

**Status:** ✅ **ACEITO** | Decisão  
**Data:** 2026-03-24  
**Autores:** Equipe Backend  

### Contexto

Projeto precisa publicar eventos de lançamento para consolidador. Escolher broker:

| Aspecto | RabbitMQ | Kafka | Redis Streams |
|--------|----------|-------|-----------------|
| **Modelo** | AMQP (exchanges, queues, routing) | Topics, partitions, offset | Stream per key |
| **Latência** | < 1ms | < 100ms | < 1ms |
| **Throughput** | 50K msg/s | 1M+ msg/s | 100K msg/s |
| **Setup** | Simples (container) | Complexo (Zookeeper) | Muito simples |
| **Aspire** | ✅ Nativo | ❌ Não incluído | ⚠️ Parcial |
| **Durabilidade** | TTL configurável | Ilimitado (log) | Até 2GB padrão |

Requisitos:
- 50 req/s (16 msg/s outbox) — todos suportam
- Setup rápido (Aspire) — RabbitMQ vence
- Simplicidade de consumidor — RabbitMQ vence

### Decisão

Usar **RabbitMQ 3 com Direct Exchange + Routing Keys**:

1. **Exchange type**: `direct` (routing sem fan-out complexo)
2. **Routing key**: `cashflow.ledger.entry.registered`
3. **Queue**: `cashflow.ledger.consolidation` (Worker consome)
4. **Durability**: `durable=true` (persiste em disk)
5. **QoS**: Prefetch = 10 (backpressure)

```csharp
// src/CashFlow.Infrastructure/Services/RabbitMqPublisher.cs
channel.ExchangeDeclare(
    exchange: "direct",
    type: ExchangeType.Direct,
    durable: true
);
channel.BasicPublish(
    exchange: "direct",
    routingKey: "cashflow.ledger.entry.registered",
    body: payload
);
```

### Consequências

✅ **Positivas:**
- **Latência baixa**: Direct exchange é O(1), ideal para Outbox
- **Aspire integrado**: Container autorizado nativamente
- **Simplicidade**: Modelo publish-subscribe é direto
- **Escalável**: 50 req/s é trivial (50K+ msg/s capacity)

❌ **Negativas:**
- **Escalabilidade horizontal**: Requer RabbitMQ Cluster (não feito aqui)
- **Overhead**: Mais um serviço para monitorar
- **TTL limitado**: Sem zero-copy partitions como Kafka

### Alternativa não adotada

**Kafka**: Descarta por:
- Setup complexo (Zookeeper, multi-broker)
- Sem suporte Aspire automático
- Overkill para 50 req/s

### Referências

- Publisher: [RabbitMqPublisher.cs](../src/CashFlow.Infrastructure/Services/RabbitMqPublisher.cs)
- Configuração: [RabbitMQ exchange/queue declara](../src/CashFlow.Worker/Worker.cs#L40-L65)
- AppHost: [RabbitMQ delaration](../src/AppHost/CashFlow.AppHost/CashFlow.AppHost.AppHost/AppHost.cs#L20)

---

## Sumário de Decisões

| ADR | Título | Status | Trade-off Principal |
|-----|--------|--------|-------------------|
| **ADR-001** | Outbox Pattern | ✅ ACEITO | +Durabilidade −Latência 3s |
| **ADR-002** | Batch 50/3s | ✅ ACEITO | +Throughput −Perda possível |
| **ADR-003** | PostgreSQL+EF Core | ✅ ACEITO | +Cross-platform −Overhead ORM |
| **ADR-004** | IdempotencyKey duplo | ✅ ACEITO | +Rastreamento −Storage |
| **ADR-005** | Background Service Worker | ✅ ACEITO | +Resilência −Eventual consistency |
| **ADR-006** | Sem Auth v1 | ⏳ DEBT | +Velocidade −Segurança |
| **ADR-007** | RabbitMQ Direct | ✅ ACEITO | +Simplicidade −Clustering |

---

## Próximos ADRs (Planejamento)

- **ADR-008**: Circuit Breaker para RabbitMQ (Polly)
- **ADR-009**: Redis caching de saldos
- **ADR-010**: Multi-tenancy support
- **ADR-011**: Event Sourcing vs. State Snapshots

---

**Última atualização:** Março 2026  
**Próxima revisão:** Junho 2026

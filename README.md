# kart-inventory-service

Stock truth per warehouse/SKU, synchronous reservation holds with a 15-minute TTL, and oversell
prevention via row-level pessimistic locking (`SELECT ... FOR UPDATE`) — the platform's
highest-contention service (`kart-platform/docs/PLATFORM_BLUEPRINT.md`) and a mandatory,
blocking participant in the Order Saga (reserve = saga step 1, release = the compensating action).

Full design docs: `kart-platform/docs/services/kart-inventory-service/*.md`. This repo is that
design's implementation.

## Architecture

.NET 8 / ASP.NET Core, Clean Architecture + Vertical Slice (CQRS via MediatR) — the same shape as
every other Kart service (see [folder-structure.md](https://github.com/kakon-mehedi/agent-reusables)):

```
src/Api             Controllers, gRPC (InventoryAvailabilityService), middleware, auth, observability
src/Application      MediatR commands/queries+handlers, one folder per ticket (INV-1..INV-8)
src/Domain           WarehouseStock + Reservation aggregates, Result/Error, zero framework deps
src/Infrastructure   EF Core (Postgres), Redis cache-aside, RabbitMQ (manifest-driven), JWT
```

`Domain` has no dependencies; `Application → Domain`; `Infrastructure → Application, Domain`;
`Api → Application, Infrastructure`.

## Message bus

`contracts/message-bus-manifest.json` is the single source of truth for this service's entire
RabbitMQ topology — exchanges, queues, bindings, dead-letter queues, retry ladders. Nothing
messaging-related is a hardcoded string literal in `Infrastructure/Messaging`; the manifest is
loaded once at startup and the topology is declared idempotently from it (same pattern as
`kart-identity-service`/`kart-category-service`).

## Local development prerequisites

- .NET 8 SDK
- PostgreSQL 16
- Redis 7
- RabbitMQ 3.x (management plugin optional)

### Connection configuration (env vars, double-underscore ASP.NET Core convention)

```
ConnectionStrings__InventoryDatabase=Host=localhost;Port=5432;Database=kart_inventory;Username=kart_inventory_service;Password=changeme
ConnectionStrings__Redis=localhost:6379
RabbitMq__HostName=localhost
Identity__JwksUri=http://kart-identity-service/.well-known/jwks.json
```

### Run migrations

```
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

### Run the API

```
dotnet run --project src/Api
```

REST on `/v1/inventory/*`, gRPC `InventoryAvailabilityService` on the same Kestrel endpoint,
Prometheus scrape target at `/metrics`.

## Tests

```
dotnet test
```

- `tests/UnitTests` — Domain invariants (oversell rejection, idempotent release) + handler tests.
- `tests/IntegrationTests` — Testcontainers-backed PostgreSQL; includes a genuine concurrency test
  proving `SELECT ... FOR UPDATE` actually serializes concurrent reservations against the same SKU.
- `tests/ContractTests` — `WebApplicationFactory`-driven HTTP wire-shape assertions against
  `contracts/api-contract.yaml`.

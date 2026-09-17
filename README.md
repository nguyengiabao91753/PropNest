# PropNest

PropNest is a modular-monolith property-listing system. The API owns HTTP, authentication and request validation. The Workflow Worker owns asynchronous processing, durable workflow state and retries. SQL Server is the system of record; RabbitMQ is the event transport.

## Projects

- `PropNest.Domain`: entities, enums and business invariants.
- `PropNest.Application`: use cases and dependency abstractions.
- `PropNest.Infrastructure`: EF Core, persistence, repositories and the outbox dispatcher.
- `PropNest.Api`: HTTP API, middleware, authorization and Swagger.
- `PropNest.Workflow.Worker`: background worker for outbox and future Saga consumers.

## Local setup

1. Copy `deploy/.env.example` to `deploy/.env` and choose a local SQL Server password.
2. Run `docker compose --env-file .env -f deploy/docker-compose.yml up -d` from `deploy`.
3. Set `ConnectionStrings__PropNest` and JWT values with User Secrets or environment variables.
4. Run `dotnet restore`, then `dotnet build PropNest.slnx`.
5. Create the initial database migration with `dotnet ef migrations add InitialCreate --project src/PropNest.Infrastructure --startup-project src/PropNest.Api`.

MassTransit and RabbitMQ consumers are introduced after the core outbox flow is verified. The worker dispatches persisted outbox messages through the `IOutboxMessagePublisher` boundary, so a MassTransit implementation can replace the logging publisher without changing domain or API code.

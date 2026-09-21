# PropNest

PropNest is a modular-monolith property-listing system. The API owns HTTP, authentication and request validation. The Workflow Worker owns asynchronous processing, durable workflow state and retries. SQL Server is the system of record; RabbitMQ is the event transport.

## Projects

- `PropNest.Domain`: entities, enums and business invariants.
- `PropNest.Application`: use cases and dependency abstractions.
- `PropNest.Infrastructure`: EF Core, persistence, repositories and the outbox dispatcher.
- `PropNest.Api`: HTTP API, middleware, authorization and Swagger.
- `PropNest.Workflow.Worker`: background worker for outbox and future Saga consumers.

## Local setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (recommended for SQL Server and RabbitMQ) or a local SQL Server instance.
- EF Core CLI tool (install once globally):
  ```powershell
  dotnet tool install --global dotnet-ef
  ```

### Getting started

1. **Start Infrastructure (SQL Server & RabbitMQ)**:
   - Create `deploy/.env` (or pass environment variables) with your SQL Server SA password:
     ```powershell
     $env:SA_PASSWORD="YourStrong@Pass123"
     docker compose -f deploy/docker-compose.yml up -d
     ```
2. **Configure Connection String & JWT**:
   - Update `ConnectionStrings:PropNest` in `src/PropNest.Api/appsettings.json` (or use .NET User Secrets / environment variables `ConnectionStrings__PropNest`).
3. **Build Solution**:
   ```powershell
   dotnet restore
   dotnet build PropNest.slnx
   ```
4. **Apply Database Migrations**:
   - Migration files are already committed to source control. To sync your local database, run:
     ```powershell
     dotnet ef database update --project src/PropNest.Infrastructure --startup-project src/PropNest.Api
     ```
5. **Run Applications**:
   - **PropNest API** (Swagger at `/swagger`, Health check at `/health`):
     ```powershell
     dotnet run --project src/PropNest.Api
     ```
   - **Workflow Worker** (Outbox & Saga processing):
     ```powershell
     dotnet run --project src/PropNest.Workflow.Worker
     ```

MassTransit and RabbitMQ consumers are introduced after the core outbox flow is verified. The worker dispatches persisted outbox messages through the `IOutboxMessagePublisher` boundary, so a MassTransit implementation can replace the logging publisher without changing domain or API code.

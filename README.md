# Expense Splitter

Educational ASP.NET Core backend for splitting group expenses during trips and events.

Participants belong to a trip, record expenses, and receive a clear settlement plan: who should pay whom and how much.

## MVP

- Create a group (trip/event) and add participants.
- Record an expense with an amount, description, payer, selected participants, split type, and creation time.
- Support equal splitting between all or selected participants.
- Calculate each participant's balance and propose a simplified settlement plan.

## Backend structure

The backend is in [`backend`](backend) and follows a four-layer architecture:

- `ExpenseSplitter.Api` — HTTP entry point.
- `ExpenseSplitter.Application` — use cases and application contracts.
- `ExpenseSplitter.Domain` — business model and rules.
- `ExpenseSplitter.Infrastructure` — database and external-service implementations.

`Directory.Build.props` and `Directory.Packages.props` in `backend` hold the shared build settings and package versions.

## Database

Persistence uses PostgreSQL 17 with EF Core 10. Start the local database and apply migrations
from the repository root:

```powershell
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString("N")
dotnet user-secrets set "ConnectionStrings:ExpenseSplitter" "Host=localhost;Port=55432;Database=expense_splitter;Username=expense_splitter;Password=$env:POSTGRES_PASSWORD" --project backend/src/ExpenseSplitter.Api
docker compose -f backend/compose.yaml up -d --wait
dotnet tool restore
dotnet ef database update --project backend/src/ExpenseSplitter.Infrastructure --startup-project backend/src/ExpenseSplitter.Api -- --environment Development
```

The Development connection string is stored in .NET User Secrets, outside the repository.
For other environments, configure `ConnectionStrings__ExpenseSplitter` externally.
Migrations are applied explicitly, not automatically when the API starts.
Money is represented as `decimal` end to end; persisted amounts use `numeric(29,2)`.
The upper bound `792281625142643375935439503.35` guarantees exact cent arithmetic
and reliable PostgreSQL round-trips.
See [persistence design and usage](docs/persistence.md) for relationships, delete rules,
money precision, aggregate loading, and migration details.

## Build and test

```powershell
dotnet build .\ExpenseSplitter.sln
dotnet test .\ExpenseSplitter.sln
```

Open `ExpenseSplitter.sln` from the repository root in the IDE. It groups the current
projects under `backend` and leaves the solution root available for future clients and
other top-level components.

Integration tests require a running Docker engine with Linux containers. They create
their own temporary PostgreSQL container and databases, apply migrations, and verify
the real schema; they do not use the development database. Domain and EF model tests
can be run without Docker (see [persistence.md](docs/persistence.md)).

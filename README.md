# Expense Splitter

Application for splitting group expenses during trips and events, with independent
ASP.NET Core backend and browser frontend projects.

Participants belong to a trip, record expenses, and receive a clear settlement plan: who should pay whom and how much.

## MVP

- Create a group (trip/event) and add participants.
- Record an expense with an amount, description, payer, selected participants, split type, and manually selectable occurrence time.
- Delete trips, participants, and expenses.
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
Money uses `decimal` in backend contracts and `numeric(29,2)` in PostgreSQL.
JSON monetary values are strings; the frontend calculates integer cents with `BigInt`.
The calculators also accumulate integer cents internally. Derived balances and transfers may
exceed the single-expense limit when exactly representable as `decimal`, without rounding.
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

## Frontend

The independent frontend project is in [`frontend`](frontend). It preserves the original
static design and communicates with the backend over HTTP; it is not hosted by or built
into the ASP.NET project.

```powershell
cd .\frontend
npm install
npm run dev
```

The development server opens at `http://localhost:5173` and uses
`http://localhost:5050` as the default API address. Set `VITE_API_BASE_URL` in
`frontend/.env.local` when the API is hosted elsewhere. See
[`frontend/README.md`](frontend/README.md) for the frontend commands.

Integration tests require a running Docker engine with Linux containers. They create
their own temporary PostgreSQL container and databases, apply migrations, and verify
the real schema; they do not use the development database. Domain and EF model tests
can be run without Docker (see [persistence.md](docs/persistence.md)).

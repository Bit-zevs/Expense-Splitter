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

## Build and test

```powershell
dotnet build .\backend\ExpenseSplitter.Backend.sln
dotnet test .\backend\ExpenseSplitter.Backend.sln
```

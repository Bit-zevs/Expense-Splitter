# Expense Splitter

## Project purpose

Educational ASP.NET Core backend for splitting group expenses.

## Architecture

Projects:

- ExpenseSplitter.Api
- ExpenseSplitter.Application
- ExpenseSplitter.Domain
- ExpenseSplitter.Infrastructure

Dependencies:

Api -> Application
Infrastructure -> Application
Application -> Domain

Domain must not reference Infrastructure or Api.

## Coding rules
[.idea](.idea)
- Use decimal for monetary values.
- Use async EF Core APIs.
- CancellationToken should be propagated for async application operations.
- Do not introduce MediatR unless explicitly requested.
- Do not introduce repositories unless there is a concrete reason.
- Prefer simple solutions over abstractions.
- Public application behavior should have tests.

## Commands

Build:

dotnet build

Tests:

dotnet test
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
- Monetary calculations in the backend must use only decimal, including intermediate values. BigInteger is prohibited; do not propose or implement a migration to it. Preserve exact cents and fail explicitly when a result cannot be represented, rather than silently rounding.
- The browser must use decimal.js for monetary arithmetic and strings in JSON. Do not use BigInt or JavaScript Number for money. Counts and dates may use Number.
- Keep the current application/store and frontend structure for MVP. Revisit use-case-oriented loading and views/state/actions/router separation before the next substantial feature, not as incidental cleanup.
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

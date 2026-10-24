# MAiMoney

[![CI](https://github.com/TrifShterev/MAiMoney/actions/workflows/ci.yml/badge.svg)](https://github.com/TrifShterev/MAiMoney/actions/workflows/ci.yml)

A simple personal finance tracker. Record income and expenses, organize them in categories, and keep an eye on monthly budgets.

Built with ASP.NET Core 10, Blazor (Interactive Server) and MongoDB.

## Run it

### Everything in Docker

```bash
docker compose up -d --build
```

Open http://localhost:8080 and create an account. MongoDB data and the sign-in keys live in Docker volumes, so they survive restarts.

### Local development

Requires the .NET 10 SDK.

```bash
docker compose up -d mongo
dotnet run --project src/MAiMoney.Web --launch-profile http
```

Open http://localhost:5166.

### Linux and Claude Code cloud sessions

`scripts/dev-env.sh up` installs the .NET 10 SDK if needed, starts MongoDB (existing server, Docker, or a `mongod` downloaded from NuGet) and runs the app in the background on http://localhost:5166. Cloud sessions run it automatically through the SessionStart hook in `.claude/settings.json`. See [CLAUDE.md](CLAUDE.md) for the details and the optional cloud environment setup script (`scripts/cloud-setup.sh`).

### Tests

The service tests are integration tests against a real MongoDB. They start their own throw-away server with [EphemeralMongo](https://github.com/asimmon/ephemeral-mongo), so Docker isn't needed. The `mongod` binaries are downloaded on the first run. To use binaries you already have (offline or behind a proxy), set `MAIMONEY_TEST_MONGO_BIN` to the folder that holds `mongod`.

```bash
dotnet test
```

Local test account used during development (only in your local database): `demo@maimoney.test` / `Demo-pass-2026`.

## Structure

| Project | Role |
|---|---|
| `src/MAiMoney.Web` | Web layer: Blazor UI, API controllers, cookie authentication, app wiring |
| `src/MAiMoney.Services` | Business layer: entities, services with all rules, options, MongoDB access |
| `tests/MAiMoney.Services.Tests` | xUnit integration tests for the services |

There is no repository layer. Services work with `IMongoCollection<T>` directly through `MongoDbContext`.

### Entities

- **Category**:
  - Fields: name, optional description, type (Income/Expense), color, and an optional monthly budget (expense categories only).
  - Names are unique per user and type.
  - A category can't be deleted while transactions use it.
- **Transaction**:
  - Fields: type, amount (> 0, max two decimals), date, category (must match the type) and an optional description.
- **User**: email, display name and password hash. It is used only for authentication.

### Configuration (`appsettings.json`)

- `MongoDb:ConnectionString`, `MongoDb:DatabaseName`
- `MAiMoney:CurrencyCode`: shown next to every amount (default `EUR`).
- `MAiMoney:DefaultCategories`: created for every new account.
- `DataProtection:KeysPath` (optional): where sign-in keys are persisted. Docker sets it to a volume.

## API

All endpoints require the sign-in cookie. Without it they return `401`. Errors are returned as ProblemDetails.

| Method | Route | |
|---|---|---|
| GET | `/api/categories?type=Expense` | List categories |
| GET/PUT/DELETE | `/api/categories/{id}` | Read, update, delete |
| POST | `/api/categories` | Create |
| GET | `/api/transactions?year=2026&month=10&type=&categoryId=&limit=` | List, newest first |
| GET | `/api/transactions/summary?year=2026&month=10` | Monthly totals and budget usage |
| GET | `/api/transactions/daily?year=2026&month=10` | Income and expense totals for every day of the month |
| GET/PUT/DELETE | `/api/transactions/{id}` | Read, update, delete |
| POST | `/api/transactions` | Create |

Login, registration and logout are form posts to `/auth/login`, `/auth/register` and `/auth/logout`.

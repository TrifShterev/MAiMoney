# MAiMoney

Personal finance tracker: ASP.NET Core 10 + Blazor (Interactive Server) + MongoDB. See [README.md](README.md) for the structure, entities, configuration and API.

- `src/MAiMoney.Web`: UI, API controllers, auth, wiring
- `src/MAiMoney.Services`: entities and all business rules; services use `MongoDbContext` directly (no repositories)
- `tests/MAiMoney.Services.Tests`: xUnit integration tests; `MongoFixture` starts its own throw-away mongod with EphemeralMongo (downloaded from `fastdl.mongodb.org`, or taken from the folder in `MAIMONEY_TEST_MONGO_BIN`)

## Working environment

The app needs the .NET 10 SDK and a MongoDB on `localhost:27017` (its default connection string). The tests need only the SDK and a `mongod` binary for EphemeralMongo.

### Cloud sessions (Claude Code on the web) and other Linux machines

[scripts/dev-env.sh](scripts/dev-env.sh) sets everything up. In cloud sessions the SessionStart hook in [.claude/settings.json](.claude/settings.json) runs `scripts/dev-env.sh session-start` on startup and resume, so the SDK, MongoDB and the app should already be up. Its status lines are at the top of the session. If they show a failure, or you're on another Linux box, run:

```bash
scripts/dev-env.sh up        # SDK + MongoDB + app (keeps what is already running)
scripts/dev-env.sh status
scripts/dev-env.sh app       # rebuild and restart the app after code changes
scripts/dev-env.sh test      # dotnet test with MAIMONEY_TEST_MONGO_BIN set to the NuGet mongod
scripts/dev-env.sh stop
```

- App: http://localhost:5166 (`dotnet run --launch-profile http`, Development). Log: `.dev-env/web.log`, build output: `.dev-env/build.log`.
- MongoDB comes from the first source that works: something already on 27017, then `docker compose up -d --wait mongo` (starting `dockerd` if needed), then the `mongod` binary from the `ephemeralmongo8.runtime.linux-x64` NuGet package, run from `.dev-env/mongo-data`. Force one with `MAIMONEY_MONGO=docker` or `MAIMONEY_MONGO=nuget`.
- The NuGet route exists because cloud sessions can reach `api.nuget.org` but usually not `fastdl.mongodb.org`, where EphemeralMongo downloads mongod. For the same reason the hook exports `MAIMONEY_TEST_MONGO_BIN` pointing at that NuGet mongod, so plain `dotnet test` works in cloud sessions too.
- .NET SDK: `apt-get install dotnet-sdk-10.0` (in Ubuntu 24.04's own archive), else `dotnet-install.sh` into `~/.dotnet`. Without libicu the script falls back to `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, which is fine because the app formats with `InvariantCulture`.
- [scripts/cloud-setup.sh](scripts/cloud-setup.sh) is the optional setup script for the cloud environment (claude.ai/code > environment settings > Setup script). It preinstalls the SDK and pulls `mongo:8` into the environment cache so sessions start faster. It lives in the environment, not the repo, so the user has to paste it there.
- If `dotnet test` fails because it can't download mongod, run `scripts/dev-env.sh test` instead (or set `MAIMONEY_TEST_MONGO_BIN` yourself).
- Register a fresh account in the app to try the UI (any email and password); nothing is seeded.

### Local (Windows)

Docker Desktop + `docker compose up -d mongo`, then `dotnet run --project src/MAiMoney.Web --launch-profile http`. The VS Code tasks in `.vscode/tasks.json` and the `maimoney-web` entry in `.claude/launch.json` do the same. `dotnet test` needs no Docker. The SessionStart hook does nothing outside cloud sessions.

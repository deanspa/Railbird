# Railbird Summary

## What It Does
Railbird is a local-only MVP that:
- Validates NLHE 6-max hands in the HRS v1 JSON format against the JSON Schema.
- Stores validated hands and their events in SQLite.
- Provides a CLI to import hands and list stored hands.

## How It Works
1. The CLI reads a hand JSON file.
2. The JSON is validated against the HRS schema in `docs/specs/hrs/v1/` using JsonSchema.Net.
3. If valid, the JSON is deserialized into POCO models.
4. A SQLite database is created/updated if needed via the migration script in `src/Railbird.Storage/Db/Migrations/001_init.sql`.
5. The hand, players, and events are persisted into SQLite tables.
6. The CLI can list recent hands with basic metadata.

## Running The App
All commands are run from the repo root.

### Build And Test
```bash
dotnet build src/Railbird.sln -c Release
dotnet test src/Railbird.sln -c Release --no-build
```

### Import A Single Hand
```bash
dotnet run --project src/Railbird.Cli -- import examples/hands/v1/HAND_RECORDING_EXAMPLE_1.json
```

### Import All Example Hands
```bash
dotnet run --project src/Railbird.Cli -- import-examples
```

### List Recent Hands
```bash
dotnet run --project src/Railbird.Cli -- list
```

### Generate A Hand Interactively
```bash
dotnet run --project src/Railbird.HandRecorder
```

## Data Storage
- Default SQLite file: `.local/railbird.db`
- Configure via `src/Railbird.Cli/appsettings.json` using the `ConnectionStrings:RailbirdDb` value.

## Repo Map
- Schema/spec: `docs/specs/hrs/v1/`
- Example hands: `examples/hands/v1/`
- Core models + validation: `src/Railbird.Core/`
- SQLite storage: `src/Railbird.Storage/`
- CLI: `src/Railbird.Cli/`
- Tests: `src/Railbird.Core.Tests/`

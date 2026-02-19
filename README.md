# Railbird

Railbird is a local-only MVP for validating and storing NLHE 6-max hands recorded in the Hand Recording Standard (HRS) v1 format. It provides a CLI to import hands and list stored hands.

## MVP Scope
- Validate HRS JSON against the v1 JSON Schema
- Store validated hands and events in SQLite
- CLI commands to import hands and list stored hands

## Quickstart

```bash
# Import a single example hand
dotnet run --project src/Railbird.Cli -- import examples/hands/v1/HAND_RECORDING_EXAMPLE_1.json

# Import all examples
dotnet run --project src/Railbird.Cli -- import-examples

# List recent hands
dotnet run --project src/Railbird.Cli -- list
```

## Repo Structure
See `docs/specs/hrs/v1/` for the canonical HRS spec and schema.

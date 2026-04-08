# HR System App Tests

This folder contains:

- `HrSystemApp.Tests.Unit`: fast unit tests (handlers, validators, mapping).
- `HrSystemApp.Tests.Integration`: API + DB integration tests using PostgreSQL Testcontainers.

## Prerequisites

- .NET 8 SDK
- Docker running (required for integration tests)

## Run Tests Locally

### Git Bash (MINGW)

Use forward slashes in paths:

```bash
dotnet test "tests/HrSystemApp.Tests.Unit/HrSystemApp.Tests.Unit.csproj" -v minimal
dotnet test "tests/HrSystemApp.Tests.Integration/HrSystemApp.Tests.Integration.csproj" -v minimal
dotnet test "HrSystemApp.sln" -v minimal
```

### PowerShell

```powershell
dotnet test tests\HrSystemApp.Tests.Unit\HrSystemApp.Tests.Unit.csproj -v minimal
dotnet test tests\HrSystemApp.Tests.Integration\HrSystemApp.Tests.Integration.csproj -v minimal
dotnet test HrSystemApp.sln -v minimal
```

## Coverage

Run with built-in collector:

```bash
dotnet test "HrSystemApp.sln" --collect:"XPlat Code Coverage"
```

Coverage files are generated under `**/TestResults/**/coverage.cobertura.xml`.

## CI Baseline (GitHub Actions Example)

```yaml
name: Tests

on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Unit tests
        run: dotnet test tests/HrSystemApp.Tests.Unit/HrSystemApp.Tests.Unit.csproj -v minimal

      - name: Integration tests
        run: dotnet test tests/HrSystemApp.Tests.Integration/HrSystemApp.Tests.Integration.csproj -v minimal

      - name: Coverage
        run: dotnet test HrSystemApp.sln --collect:"XPlat Code Coverage"
```

Notes:

- GitHub hosted runners already have Docker available, which integration tests need.
- If you split pipelines, run unit tests on every PR and integration tests on PR/nightly.

## Troubleshooting

- `MSB1009 Project file does not exist` in Git Bash:
  Use forward slashes, not backslashes.
- Docker/Testcontainers errors:
  Start Docker Desktop and re-run integration tests.
- Integration DB FK errors during seeding:
  Ensure test fixture creates related `Users` rows before `Employees`.

# Default Commands Standard

Use these commands as the default local workflow.

## Command Execution Order

1. Restore dependencies
2. Build
3. Run tests
4. Optional format/lint
5. Update changelog and commit

## PowerShell Commands

> Run from repository root: `Q:/unityproject/sbox-scenestaging`

### Restore / Build

```powershell
dotnet restore .\sbox-scenestaging.sln
dotnet build .\sbox-scenestaging.sln -c Debug
```

### Run Tests

```powershell
dotnet test .\sbox-scenestaging.sln -c Debug --no-build
```

### Release Build (Optional)

```powershell
dotnet build .\sbox-scenestaging.sln -c Release
dotnet test .\sbox-scenestaging.sln -c Release --no-build
```

## Definition of Local Done

A change is locally done only when:

- Spec is updated or created in `docs/specs/`
- Build succeeds
- Tests pass
- `CHANGELOG.md` has an `Unreleased` entry

## Command Documentation Rule

When introducing a new recurring command, document it in this file.

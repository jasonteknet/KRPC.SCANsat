# AGENTS.md - KRPC.SCANsat

## Scope

- Applies to: `/Users/jason/src/ksp-mods/KRPC.SCANsat`

## Project intent

- Provide a kRPC service for SCANsat in flight scene.
- Keep runtime behavior defensive against SCANsat API drift.

## Build and release

- Build command:
  - `msbuild /p:Configuration=Release KRPC.SCANsat.csproj`
- Release artifact must include:
  - `GameData/kRPC/KRPC.SCANsat.dll`
- Before release tags, keep versions aligned:
  - `Properties/AssemblyInfo.cs`
  - Git tag (`vX.Y.Z`)
  - Release title/version

## Coding expectations

- Preserve backward compatibility for existing kRPC procedures unless a major version bump is planned.
- Prefer additive API changes (`KRPCProperty`, `KRPCProcedure`, `KRPCEnum`, `KRPCClass`).
- Throw `SCANsatServiceException` with concrete error text for missing bodies/vessels/modules/signatures.
- Keep reflection binding centralized in `InitTypes()` and explicit helper methods.

## Validation checklist

- Build succeeds in `Release`.
- kRPC service loads in flight scene.
- `APIReady` and `ScannerAPIReady` behavior is verified.
- New procedures tested with at least one loaded vessel carrying scanner modules.
- If replacing DLL in a running KSP session, restart KSP before validation.

## Repo hygiene

- Do not commit KSP runtime logs or local machine artifacts.
- Keep changes focused; avoid unrelated refactors in release commits.
- Document externally visible API changes in release notes and CKAN metadata updates.

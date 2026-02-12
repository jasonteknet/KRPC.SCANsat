# KRPC.SCANsat

`KRPC.SCANsat` is a kRPC service plugin that exposes SCANsat data and scanner control to remote clients.

It is intended for automation workflows where scripts need to query map coverage, elevation/slope, resource overlays, and scanner module state in flight.

## Status

- Current release: `v1.1.0`
- Active branch: `main`
- Flight scene service: `SCANsat`

## Requirements

- Kerbal Space Program (KSP 1.x)
- [kRPC](https://krpc.github.io/krpc/)
- [SCANsat](https://github.com/KSPModStewards/SCANsat)

## Installation

1. Download a release zip from the releases page.
2. Extract into your KSP root.
3. Verify this file exists:
   - `GameData/kRPC/KRPC.SCANsat.dll`
4. Start KSP and connect your kRPC client.

## Public API (Service: `SCANsat`)

### Properties

- `APIReady`
- `ScannerAPIReady`
- `ActiveSensors`
- `ActiveVessels`
- `ActualPasses`

### Procedures

- `BodyKnown(bodyName)`
- `Coverage(bodyName, scanType)`
- `CoverageBySensor(bodyName, family)`
- `IsCovered(bodyName, latitude, longitude, scanType)`
- `ResourceValue(bodyName, latitude, longitude, resourceName, biomeLock=false)`
- `Elevation(bodyName, latitude, longitude)`
- `Slope(bodyName, latitude, longitude, sampleOffsetMeters=5.0)`
- `AvailableResources()`
- `GetScanners(vesselName=null, family=Nothing)`
- `GetActiveScanners(vesselName=null, family=Nothing)`
- `IsScannerEnabled(vesselName, partFlightId, family)`
- `SetScannerEnabled(vesselName, partFlightId, family, enabled)`
- `SetSingleScannerEnabled(vesselName, family, preferredPartFlightId=0)`

### Types

- `ScanType`
- `ScannerFamily`
- `ScannerModuleStatus`
- `SCANsatServiceException`

## Build

From repository root:

1. Copy required reference assemblies into `lib/` (see `lib/README.md`).
2. Build:

```bash
msbuild /p:Configuration=Release KRPC.SCANsat.csproj
```

Build output:

- `bin/Release/KRPC.SCANsat.dll`

## Release Packaging

Release assets should contain this KSP path layout:

- `GameData/kRPC/KRPC.SCANsat.dll`

Example packaging flow:

```bash
mkdir -p /tmp/KRPC.SCANsat-vX.Y.Z/GameData/kRPC
cp bin/Release/KRPC.SCANsat.dll /tmp/KRPC.SCANsat-vX.Y.Z/GameData/kRPC/
(cd /tmp/KRPC.SCANsat-vX.Y.Z && zip -r /tmp/KRPC.SCANsat-vX.Y.Z.zip GameData)
```

## CKAN

This project is distributed via a custom metadata repo:

- https://github.com/jasonteknet/jason-ckan-metadata

Example metadata entry for this mod:

- https://github.com/jasonteknet/jason-ckan-metadata/blob/main/KRPC.SCANsat/KRPC.SCANsat-v1.1.0.ckan

## Notes

- Scanner control procedures require SCANsat scanner part-module APIs (`ScannerAPIReady`).
- If SCANsat internals change, service methods may throw `SCANsatServiceException` when signatures are unavailable.

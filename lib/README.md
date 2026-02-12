# Local Reference Assemblies

This folder is for local build-time reference assemblies and is intentionally not tracked in git.

Required files for this project:

- `Assembly-CSharp.dll` (from KSP managed assemblies)
- `UnityEngine.dll` (from KSP managed assemblies)
- `UnityEngine.CoreModule.dll` (from KSP managed assemblies)
- `KRPC.dll` (from `GameData/kRPC/Plugins`)
- `KRPC.Core.dll` (from `GameData/kRPC/Plugins`)

Typical source paths:

- `<KSP_ROOT>/KSP_x64_Data/Managed/*.dll`
- `<KSP_ROOT>/GameData/kRPC/Plugins/*.dll`

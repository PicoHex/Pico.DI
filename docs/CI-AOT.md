# CI: Native AOT (Windows) Publishing Guide

This document explains how to build and publish Native AOT executables (PublishAOT) for Windows/x64 in a CI environment (for example, GitHub Actions).

> Goal: produce a runnable Native AOT binary for win-x64 with TrimMode=full in CI.

## Prerequisites

- .NET 10 SDK (install via `actions/setup-dotnet` or equivalent in your CI).
- MSVC linker (`cl.exe` / `link.exe`) and the Windows SDK available on the Windows runner.
  - Recommended: install **Visual Studio Build Tools** or **Visual Studio** with the **Desktop development with C++** workload on the runner.
  - Alternatively, ensure `VsDevCmd.bat` is available and invoked to expose the toolchain on the runner.

## Key points

- Target only x64 (`-r win-x64`); ARM64 is not required.
- It is recommended to run `VsDevCmd.bat` (or an equivalent initializer) before publishing so MSVC tools are visible, or set `IlcUseEnvironmentalTools=true` so the AOT build uses the tools already present in the environment.
- We strongly recommend using `TrimMode=full` together with `PublishTrimmed=true` for a stable, trimmable AOT binary.

## GitHub Actions example (Windows)

Below is a minimal workflow example that shows how to locate Visual Studio, set up the environment, and publish a Native AOT binary on a `windows-2022` runner:

```yaml
name: CI - Native AOT (Windows)

on:
  push:
    branches: [ main ]

jobs:
  publish-aot-windows:
    runs-on: windows-2022
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.x'

      - name: Locate Visual Studio
        shell: pwsh
        run: |
          $vs = & 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -products * -property installationPath
          if (-not $vs) { throw 'Visual Studio not found — install Build Tools with C++ workload.' }
          echo "VS_INSTALL_PATH=$vs" >> $env:GITHUB_ENV

      - name: Publish Native AOT (x64)
        shell: cmd
        run: |
          call "%VS_INSTALL_PATH%\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
          dotnet publish samples\Pico.DI.AotTest -c Release -r win-x64 -p:PublishAOT=true -p:IlcUseEnvironmentalTools=true -p:TrimMode=full --self-contained true -o artifacts\publish\AotTest.AOT

      - name: Upload publish artifact
        uses: actions/upload-artifact@v4
        with:
          name: AotTest-win-x64
          path: artifacts/publish/AotTest.AOT
```

Notes:

- This workflow uses `vswhere` to locate Visual Studio and stores the path in `GITHUB_ENV`. It then calls `VsDevCmd.bat` to initialize the MSVC environment so `cl.exe` and `link.exe` are available.
- Setting `IlcUseEnvironmentalTools=true` tells the Native AOT build to use the MSVC/linker tools available in the environment. This can avoid additional installation steps if the tools are already present.

## CI troubleshooting

- Error: `Platform linker not found` — indicates the build cannot find `link.exe`. Fix: run `VsDevCmd.bat` before the build step, or ensure the Desktop C++ workload is installed.
- Error: `NETSDK1207: AOT not supported for the target framework` — some TFMs or SDK versions do not support PublishAOT. Ensure your project TFM is supported by the .NET SDK in the runner (we recommend using a validated .NET version such as .NET 10 for this repo).
- If you prefer not to install full Visual Studio on the runner, install Visual Studio Build Tools or add the MSVC bin directory to PATH on a self-hosted runner.

## Artifacts and publishing

- We recommend uploading the publish output as a CI artifact (for example: `artifacts/publish/AotTest.AOT`) so you can download and verify the produced executable.

---

Would you like me to add this workflow example directly to `.github/workflows/ci-aot-windows.yml` as an optional sample?

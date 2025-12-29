Benchmarks with Native AOT
=========================

This project runs BenchmarkDotNet benchmarks and can be published as a Native AOT executable.

Quick run (managed):

```powershell
dotnet run --project benchmarks/Pico.DI.Benchmarks -c Release
```

Publish as Native AOT (example for win-x64):

```powershell
dotnet publish --project benchmarks/Pico.DI.Benchmarks -c Release -r win-x64 --self-contained true /p:PublishAot=true
```

When published as Native AOT you will get a standalone executable in `bin/Release/net10.0/win-x64/publish`.

Note: BenchmarkDotNet may run additional processes and generate reports; ensure the target runtime supports all required runtime diagnostics.

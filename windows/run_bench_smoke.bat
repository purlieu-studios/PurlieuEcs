@echo off
setlocal
echo Building benchmarks...
dotnet build -c Release benchmarks\PurlieuEcs.Benchmarks.csproj || exit /b 1
echo Running benchmarks (smoke)...
dotnet run -c Release --project benchmarks\PurlieuEcs.Benchmarks.csproj -- --anyCategories * || echo NOTE: Benchmarks exited with non-zero (allowed in smoke)
echo Done. Check console output for numbers and compare to benchmarks\BENCHMARK_BASELINES.json.
endlocal

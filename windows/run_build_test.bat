@echo off
setlocal
echo Restoring...
dotnet restore || exit /b 1
echo Building (Release)...
dotnet build -c Release --no-restore || exit /b 1
echo Running tests...
dotnet test tests\PurlieuEcs.Tests -c Release --no-build || exit /b 1
echo OK: Build + Tests passed.
endlocal

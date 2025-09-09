@echo off
setlocal
set "MAX=%~1"
if "%MAX%"=="" set "MAX=20"
echo Checking staged diff (git) against max lines: %MAX%
for /f "delims=" %%I in ('powershell -NoProfile -Command "$n=0; git diff --numstat --cached ^| ForEach-Object { $p=$_ -split \"`t\"; if($p.Length -ge 2){ $n += [int]$p[0] + [int]$p[1] } }; $n"') do set TOTAL=%%I

if "%TOTAL%"=="" set TOTAL=0
echo Changed lines (staged): %TOTAL%
if %TOTAL% LEQ %MAX% (
  echo OK: within limit.
  exit /b 0
) else (
  echo FAIL: %TOTAL% lines changed exceeds %MAX% limit.
  exit /b 2
)
endlocal

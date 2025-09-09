@echo off
setlocal enabledelayedexpansion
:: Usage: roadmap_template.bat  "What do we implement next to reach v0?"
if "%~1"=="" (
  echo Usage: %~nx0 "What do we implement next to reach v0 Definition of Done?"
  exit /b 1
)
set "QUESTION=%~1"
set "SCOPE=%~2"
if "%SCOPE%"=="" set "SCOPE=src/PurlieuEcs"

for /f %%I in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyy-MM-dd\")"') do set TODAY=%%I
set "OUT=ecsmind_roadmap_%TODAY%.txt"

> "%OUT%" echo /project:ecs-mind
>> "%OUT%" echo question: "%QUESTION%"
>> "%OUT%" echo phase: roadmap
>> "%OUT%" echo mode: full
>> "%OUT%" echo scope: %SCOPE%

type "%OUT%" | clip
echo Created and copied: %OUT%
endlocal

@echo off
setlocal enabledelayedexpansion
:: Usage: decide_template.bat "Your decision question" [scope]
if "%~1"=="" (
  echo Usage: %~nx0 "Lock v0 entity handle and lifecycle semantics?" [scope]
  exit /b 1
)
set "QUESTION=%~1"
set "SCOPE=%~2"
if "%SCOPE%"=="" set "SCOPE=src/PurlieuEcs"

for /f %%I in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyy-MM-dd\")"') do set TODAY=%%I

set "OUT=ecsmind_decide_%TODAY%.txt"

> "%OUT%" echo /project:ecs-mind
>> "%OUT%" echo question: "%QUESTION%"
>> "%OUT%" echo phase: decide
>> "%OUT%" echo rounds: 3
>> "%OUT%" echo mode: full
>> "%OUT%" echo diffs: off
>> "%OUT%" echo scope: %SCOPE%
>> "%OUT%" echo weights: determinism=3,testability=3,performance=3,delivery=2,complexity=1,dx=2

type "%OUT%" | clip
echo Created and copied: %OUT%
endlocal

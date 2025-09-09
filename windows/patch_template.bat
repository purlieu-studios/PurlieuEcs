@echo off
setlocal enabledelayedexpansion
:: Usage: patch_template.bat DECISION_REF "Implement X" files_semi_colon_separated [max_lines]
if "%~1"=="" (
  echo Usage: %~nx0 2025-09-08-entity-handle "Implement Entity and World.Create/Destroy" src\PurlieuEcs\Core\Entity.cs;src\PurlieuEcs\Core\World.cs [max_lines]
  exit /b 1
)
set "DECISION_REF=%~1"
set "QUESTION=%~2"
set "FILES_SEMI=%~3"
set "MAX_LINES=%~4"
if "%QUESTION%"=="" set "QUESTION=Implement decision: %DECISION_REF%"
if "%FILES_SEMI%"=="" (
  echo ERROR: Provide files as semicolon-separated list.
  exit /b 1
)
if "%MAX_LINES%"=="" set "MAX_LINES=20"

:: Convert semicolon list to YAML list
set "FILES_YAML="
for %%F in (%FILES_SEMI:;= %) do (
  set "FILES_YAML=!FILES_YAML!  - %%F\r\n"
)

for /f %%I in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyy-MM-dd\")"') do set TODAY=%%I
set "OUT=ecsmind_patch_%TODAY%.txt"

> "%OUT%" echo /project:ecs-mind
>> "%OUT%" echo question: "%QUESTION%"
>> "%OUT%" echo phase: patch
>> "%OUT%" echo decision_ref: "%DECISION_REF%"
>> "%OUT%" echo files:
>> "%OUT%" echo !FILES_YAML!
>> "%OUT%" echo max_lines: %MAX_LINES%
>> "%OUT%" echo mode: full
>> "%OUT%" echo diffs: on
>> "%OUT%" echo code_context:
for %%F in (%FILES_SEMI:;= %) do (
  >> "%OUT%" echo   - %%F
)

type "%OUT%" | clip
echo Created and copied: %OUT%
endlocal

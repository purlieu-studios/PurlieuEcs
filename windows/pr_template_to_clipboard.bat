@echo off
setlocal
set "PR=.github\PULL_REQUEST_TEMPLATE.md"
if not exist "%PR%" (
  echo Could not find %PR%. Place this repo's PR template at .github\PULL_REQUEST_TEMPLATE.md
  exit /b 1
)
type "%PR%" | clip
echo Copied PR template to clipboard.
endlocal

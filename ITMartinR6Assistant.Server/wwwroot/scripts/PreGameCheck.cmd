@echo off
rem Double-click to run the R6 pre-game check (user 2026-10-07: "easy opening").
rem Fetches the newest PreGameCheck.ps1 from r6.itmartin.dk to TEMP and runs it - no admin, no settings changed.
title R6 PreGameCheck
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Join-Path $env:TEMP PreGameCheck.ps1; Invoke-WebRequest -UseBasicParsing https://r6.itmartin.dk/scripts/PreGameCheck.ps1 -OutFile $f; & $f"
echo.
pause

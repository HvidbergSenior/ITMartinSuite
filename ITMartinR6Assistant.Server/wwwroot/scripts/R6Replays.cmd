@echo off
rem Double-click: sends the stats from Siege's replay files to r6.itmartin.dk and keeps sending each new match.
rem Fetches the newest R6Replays.ps1 to TEMP and runs it - no admin, nothing in the game folder is changed.
title R6 kampstatistik
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f = Join-Path $env:TEMP R6Replays.ps1; Invoke-WebRequest -UseBasicParsing https://r6.itmartin.dk/scripts/R6Replays.ps1 -OutFile $f; & $f"
echo.
pause

@echo off
rem ITMartin Rip CD - dobbeltklik for at starte. Windows spørger om lov til at køre som administrator (cd-drevet kræver det).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0RipCD.ps1"

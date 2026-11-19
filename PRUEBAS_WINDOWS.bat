@echo off
cd /d "%~dp0"
dotnet run --project "tests\FilaCero.SmokeTests\FilaCero.SmokeTests.csproj"
if errorlevel 1 exit /b 1
pause

@echo off
cd /d "%~dp0"
echo === FilaCero API ASP.NET Core 11 ===
echo API: http://localhost:5071/api/health
where dotnet >nul 2>nul
if errorlevel 1 (
 echo No se encontro dotnet.exe. Instala el SDK de .NET 11 y abre una nueva terminal.
 pause
 exit /b 1
)
dotnet run --project "src\FilaCero.Api\FilaCero.Api.csproj" --launch-profile FilaCero.Api
pause
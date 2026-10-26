@echo off
chcp 65001 >nul
echo ========== BUILD OJGEN ==========
dotnet build OjGen.sln -c Release
if %errorlevel% neq 0 (
    echo.
    echo ========== BUILD FAILED ==========
    pause
    exit /b 1
)
echo.
echo ========== DONE ==========
echo GUI: src\OjGen.App\bin\Release\net10.0-windows\OjGen.exe
echo CLI: src\OjGen.Cli\bin\Release\net10.0\ojgen.exe
pause

@echo off
chcp 65001 >nul
echo ========== PUBLISH OJGEN ==========

echo [1/4] Tat ung dung dang chay...
taskkill /F /IM OjGen.exe 2>nul
timeout /t 1 /nobreak >nul

echo [2/4] Don dep ban build cu...
dotnet clean OjGen.sln -c Release

echo [3/4] Publish GUI (1 file exe, khong can cai .NET)...
dotnet publish src\OjGen.App\OjGen.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\gui
if %errorlevel% neq 0 goto :fail

echo [4/4] Publish CLI (ojgen.exe)...
dotnet publish src\OjGen.Cli\OjGen.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\cli
if %errorlevel% neq 0 goto :fail

echo.
echo ========== DONE ==========
echo GUI: publish\gui\OjGen.exe
echo CLI: publish\cli\ojgen.exe
echo.
pause
exit /b 0

:fail
echo.
echo ========== PUBLISH FAILED ==========
pause
exit /b 1

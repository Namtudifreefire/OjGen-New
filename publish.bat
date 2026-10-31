@echo off
setlocal
chcp 65001 >nul

set "GUI_OUT=publish\gui-single"
set "CLI_OUT=publish\cli-single"

echo ========== PUBLISH OJGEN %GUI_OUT% + %CLI_OUT% ==========
echo.
echo [1/2] Publish GUI (win-x64, self-contained, single-file)...
dotnet publish src/OjGen.App/OjGen.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:EnableCompressionInSingleFile=true -o "%GUI_OUT%"
if errorlevel 1 goto :fail

echo [2/2] Publish CLI (win-x64, self-contained, single-file)...
dotnet publish src/OjGen.Cli/OjGen.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -p:EnableCompressionInSingleFile=true -o "%CLI_OUT%"
if errorlevel 1 goto :fail

echo.
echo ========== DONE ==========
echo GUI: %GUI_OUT%\OjGen.exe
echo CLI: %CLI_OUT%\ojgen.exe
endlocal
exit /b 0

:fail
echo.
echo ========== PUBLISH FAILED ==========
endlocal
exit /b 1

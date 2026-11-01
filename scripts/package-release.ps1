[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$OutputDirectory = '',
    [switch]$CreateDraft,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$props = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)
$sourceVersion = [string]$props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($sourceVersion)) { throw 'Directory.Build.props chưa có Version.' }
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $sourceVersion }
if ($Version -ne $sourceVersion) { throw "Version '$Version' khác nguồn chuẩn '$sourceVersion' trong Directory.Build.props." }

$notesPath = Join-Path $repoRoot "RELEASE_NOTES_v$Version.md"
if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) { throw "Thiếu release notes: $notesPath" }
$releaseNotes = Get-Content -LiteralPath $notesPath -Raw
if ($releaseNotes -notmatch "(?m)^# OjGen\s+$([regex]::Escape($Version))\s*$") { throw "Release notes phải có tiêu đề '# OjGen $Version'." }
if ($releaseNotes -match '(?im)^\s*(TODO|TBD|FIXME)\b') { throw 'Release notes còn placeholder TODO/TBD/FIXME.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot 'artifacts\release' }
if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $repoRoot $OutputDirectory }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$packageName = "OjGen-$Version-win-x64"
$packageDirectory = Join-Path $outputRoot $packageName
$zipPath = Join-Path $outputRoot "$packageName.zip"
$zipHashPath = "$zipPath.sha256"
foreach ($path in @($packageDirectory, $zipPath, $zipHashPath)) {
    if (Test-Path -LiteralPath $path) { throw "Đầu ra đã tồn tại, không ghi đè: $path" }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$buildParent = Join-Path $repoRoot 'output'
New-Item -ItemType Directory -Force -Path $buildParent | Out-Null
$buildParent = (Resolve-Path -LiteralPath $buildParent).Path.TrimEnd('\', '/')
$buildDirectory = Join-Path $buildParent ("OjGenRelease-" + [guid]::NewGuid().ToString('N'))
$publishGui = Join-Path $buildDirectory 'publish-gui'
$publishCli = Join-Path $buildDirectory 'publish-cli'
$tempRootCreated = $false

function Invoke-Checked([string]$Command, [string[]]$Arguments, [string]$Description) {
    Write-Host "== $Description"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Description thất bại (exit $LASTEXITCODE)." }
}

try {
    New-Item -ItemType Directory -Path $buildDirectory | Out-Null
    $tempRootCreated = $true

    if (-not $SkipTests) {
        $bash = Get-Command bash -ErrorAction SilentlyContinue
        if ($null -eq $bash) { throw 'Không tìm thấy bash. Chạy bộ hồi quy trước hoặc dùng -SkipTests sau khi CI đã đạt.' }
        Invoke-Checked $bash.Source @((Join-Path $repoRoot 'tests/run_tests.sh')) 'Chạy bộ kiểm tra hồi quy CLI'
    }

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $python) { $python = Get-Command python3 -ErrorAction SilentlyContinue }
    if ($null -eq $python) { throw 'Không tìm thấy Python để chạy kiểm tra tĩnh giao diện.' }
    Invoke-Checked $python.Source @((Join-Path $repoRoot 'tests/check_wpf_static.py')) 'Kiểm tra tĩnh giao diện WPF'

    $guiProject = Join-Path $repoRoot 'src/OjGen.App/OjGen.App.csproj'
    $cliProject = Join-Path $repoRoot 'src/OjGen.Cli/OjGen.Cli.csproj'
    $publishOptions = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '-p:DebugSymbols=false',
        '-p:EnableCompressionInSingleFile=true')
    Invoke-Checked 'dotnet' (@('publish', $guiProject) + $publishOptions + @('-o', $publishGui)) 'Publish GUI win-x64'
    Invoke-Checked 'dotnet' (@('publish', $cliProject) + $publishOptions + @('-o', $publishCli)) 'Publish CLI win-x64'

    $guiExe = Join-Path $publishGui 'OjGen.exe'
    $cliExe = Join-Path $publishCli 'ojgen.exe'
    if (-not (Test-Path -LiteralPath $guiExe -PathType Leaf)) { throw 'Publish không tạo OjGen.exe.' }
    if (-not (Test-Path -LiteralPath $cliExe -PathType Leaf)) { throw 'Publish không tạo ojgen.exe.' }

    $versionOutput = (& $cliExe --version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch [regex]::Escape($Version)) { throw "CLI smoke test --version thất bại: $versionOutput" }
    foreach ($executable in @($guiExe, $cliExe)) {
        $productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion
        if ([string]::IsNullOrWhiteSpace($productVersion) -or $productVersion -notmatch [regex]::Escape($Version)) {
            throw "Metadata phiên bản của $([System.IO.Path]::GetFileName($executable)) không khớp $Version (nhận: '$productVersion')."
        }
    }
    $env:OJGEN_SELFTEST_ROOT = Join-Path $repoRoot 'output/.selftest'
    & $cliExe --selftest
    if ($LASTEXITCODE -ne 0) { throw 'CLI smoke test --selftest thất bại.' }
    & (Join-Path $repoRoot 'tests/smoke_gui.ps1') -ExecutablePath $guiExe
    if ($LASTEXITCODE -ne 0) { throw 'GUI smoke test thất bại.' }

    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'GUI') | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'CLI') | Out-Null
    Copy-Item -LiteralPath $guiExe -Destination (Join-Path $packageDirectory 'GUI/OjGen.exe')
    Copy-Item -LiteralPath $cliExe -Destination (Join-Path $packageDirectory 'CLI/ojgen.exe')
    foreach ($file in @('README.md', 'CHANGELOG.md', 'PLUGIN_GUIDE.md', 'LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination (Join-Path $packageDirectory $file)
    }
    Copy-Item -LiteralPath $notesPath -Destination (Join-Path $packageDirectory "RELEASE_NOTES_v$Version.md")

    $regressionStatus = if ($SkipTests) { 'Skipped in this invocation; run separately or rely on CI result.' } else { 'Passed.' }
    $preflight = @(
        "OjGen release preflight",
        "Version: $Version",
        "Release notes: passed",
        "Regression suite: $regressionStatus",
        "WPF static check: passed",
        "CLI --version and --selftest: passed",
        "GUI smoke launch: passed",
        "GUI executable: GUI/OjGen.exe",
        "CLI executable: CLI/ojgen.exe",
        "Package layout and SHA-256 manifests are verified before this package is returned."
    )
    [System.IO.File]::WriteAllLines((Join-Path $packageDirectory 'RELEASE_PREFLIGHT.txt'), $preflight, [System.Text.UTF8Encoding]::new($false))

    $sumLines = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($packageDirectory, $_.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $sumLines.Add("$hash  $relative")
    }
    [System.IO.File]::WriteAllLines((Join-Path $packageDirectory 'SHA256SUMS.txt'), $sumLines, [System.Text.UTF8Encoding]::new($false))

    $expectedFiles = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse |
        Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
        ForEach-Object { [System.IO.Path]::GetRelativePath($packageDirectory, $_.FullName).Replace('\', '/') } | Sort-Object)
    $manifestFiles = @(Get-Content -LiteralPath (Join-Path $packageDirectory 'SHA256SUMS.txt') |
        Where-Object { $_ -match '^[0-9a-f]{64}  ' } |
        ForEach-Object { $_.Substring(66) } | Sort-Object)
    if (Compare-Object -ReferenceObject $expectedFiles -DifferenceObject $manifestFiles) { throw 'SHA256SUMS.txt không khớp danh sách file của gói.' }
    foreach ($line in Get-Content -LiteralPath (Join-Path $packageDirectory 'SHA256SUMS.txt')) {
        $relative = $line.Substring(66).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $expectedHash = $line.Substring(0, 64).ToLowerInvariant()
        $actualHash = (Get-FileHash -LiteralPath (Join-Path $packageDirectory $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) { throw "SHA-256 không khớp trong gói: $relative" }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageDirectory, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $true)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entryNames = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') } | ForEach-Object FullName)
        foreach ($expected in @("$packageName/GUI/OjGen.exe", "$packageName/CLI/ojgen.exe", "$packageName/SHA256SUMS.txt")) {
            if ($expected -notin $entryNames) { throw "ZIP thiếu file: $expected" }
        }
        if ($entryNames | Where-Object { $_ -notlike "$packageName/*" }) { throw 'ZIP có file nằm ngoài thư mục gốc đã định.' }
        $expectedEntries = @($expectedFiles + @('SHA256SUMS.txt') | ForEach-Object { "$packageName/$_" } | Sort-Object)
        $actualEntries = @($entryNames | Sort-Object)
        if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries) { throw 'Danh sách file trong ZIP khác với thư mục gói.' }
    }
    finally { $archive.Dispose() }
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText($zipHashPath, "$zipHash  $([System.IO.Path]::GetFileName($zipPath))`n", [System.Text.UTF8Encoding]::new($false))
    $sidecar = (Get-Content -LiteralPath $zipHashPath -Raw).Trim()
    if (-not $sidecar.StartsWith("$zipHash  ", [System.StringComparison]::OrdinalIgnoreCase)) { throw 'SHA256 sidecar không khớp ZIP.' }

    Write-Host "Package: $packageDirectory"
    Write-Host "ZIP: $zipPath"
    Write-Host "SHA-256: $zipHash"

    if ($CreateDraft) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($null -eq $gh) { throw 'Không tìm thấy GitHub CLI gh; package đã được tạo nhưng chưa gửi bản nháp.' }
        $releaseArgs = @('release', 'create', "v$Version", $zipPath, $zipHashPath, '--draft', '--title', "OjGen v$Version", '--notes-file', $notesPath)
        if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) { $releaseArgs += @('--target', $env:GITHUB_SHA) }
        Invoke-Checked $gh.Source $releaseArgs 'Tạo GitHub draft release'
    }
}
finally {
    if ($tempRootCreated -and (Test-Path -LiteralPath $buildDirectory)) {
        $fullBuild = [System.IO.Path]::GetFullPath($buildDirectory).TrimEnd('\', '/')
        if ([string]::Equals([System.IO.Path]::GetDirectoryName($fullBuild), $buildParent, [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $fullBuild -Recurse -Force
        }
    }
}

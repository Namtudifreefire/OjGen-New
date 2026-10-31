param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [int]$StartupSeconds = 5
)

$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $ExecutablePath).Path
$process = $null
try {
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds ([Math]::Max(1, $StartupSeconds))
    $process.Refresh()
    if ($process.HasExited) {
        throw "GUI thoát trong lúc smoke test (exit code $($process.ExitCode))."
    }
    Write-Host "OK: GUI còn chạy sau $StartupSeconds giây khởi động."
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

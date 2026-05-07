[CmdletBinding()]
param(
    [switch]$StartServer
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host "`n==> $Name" -ForegroundColor Cyan
    & $Command
}

function Get-PythonCommand {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) {
        return $python.Source
    }

    $python3 = Get-Command python3 -ErrorAction SilentlyContinue
    if ($python3) {
        return $python3.Source
    }

    throw "Python command not found. Install Python or run scripts/verify_stage.py manually."
}

Write-Host "PetPresence Windows smoke-test helper" -ForegroundColor Green
Write-Host "This script runs build/test/verifier checks only. It does not capture the screen, hook input, read browser history, or inject into processes."

Invoke-Step "dotnet restore" { dotnet restore }
Invoke-Step "dotnet build -c Debug" { dotnet build -c Debug }
Invoke-Step "dotnet test -c Debug --no-build" { dotnet test -c Debug --no-build }

$pythonCommand = Get-PythonCommand
foreach ($stage in @("v0", "v1", "v2", "v3", "v4")) {
    Invoke-Step "verify_stage.py $stage" { & $pythonCommand scripts/verify_stage.py $stage }
}

if ($StartServer) {
    Write-Host "`n==> Starting PetPresence.Server in a separate dotnet process" -ForegroundColor Cyan
    $process = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/PetPresence.Server", "-c", "Debug") -PassThru
    Write-Host "Server process started. PID: $($process.Id)"
    Write-Host "Check the server console/log output for the listening URL, then use that URL for PETPRESENCE_SERVER_URL."
}

Write-Host "`nManual desktop overlay checks still require a real Windows desktop session:" -ForegroundColor Yellow
Write-Host "  1. Set `$env:PETPRESENCE_USER_ID='local-user'"
Write-Host "  2. Set `$env:PETPRESENCE_SERVER_URL='' for standalone mode, or the server URL for friend presence"
Write-Host "  3. Run: dotnet run --project src/PetPresence.Desktop -c Debug"
Write-Host "  4. Verify overlay focus behavior, click-through normal mode, edit-mode dragging, local classification, and friend presence."
Write-Host "See docs/SMOKE_TEST.md for the full checklist."

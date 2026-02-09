param(
    [ValidateSet("1","2","3","all","menu")]
    [string]$Mode = "menu"
)

$ErrorActionPreference = "Stop"

function Run-ApiTests {
    Write-Host "==> [1] API Tests"
    dotnet test api\Pocketree.Api.Tests\Pocketree.Api.Tests.csproj -c Release
}

function Run-Maestro {
    Write-Host "==> [2] Maestro Flows (smoke-seq)"
    Write-Host "Maestro will start in 5 seconds. Alt-tab to the emulator now..."
    Start-Sleep -Seconds 5
    $env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21.0.10.7-hotspot"
    $env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
    $maestro = "C:\Users\skido\maestro\maestro\bin\maestro.bat"
    & $maestro test "android\.maestro\flows\smoke-seq.yaml"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Run-MlServiceSim {
    Write-Host "==> [3] ML Service Sim (health check)"
    python -m pip install --upgrade pip
    pip install -r ml-service\requirements.txt -r ml-service\requirements-dev.txt

    $proc = Start-Process -FilePath python -ArgumentList "ml-service\CLIPModelMobile_donotmerge.py" -PassThru
    try {
        $ok = $false
        for ($i = 0; $i -lt 20; $i++) {
            try {
                $resp = Invoke-RestMethod -Uri "http://localhost:8080/health" -TimeoutSec 5
                if ($resp.status -eq "ok") {
                    $ok = $true
                    break
                }
            } catch {
                Start-Sleep -Seconds 2
            }
        }
        if (-not $ok) {
            throw "ML Service health check failed."
        }
        Write-Host "ML Service health check OK."
    } finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force
        }
    }
}

if ($Mode -eq "menu") {
    Write-Host ""
    Write-Host "Select a demo step:"
    Write-Host "  1) API Tests"
    Write-Host "  2) Maestro Flows (smoke-seq)"
    Write-Host "  3) ML Service Sim (health check)"
    Write-Host "  all) Run 1, 2, 3 in order"
    $choice = Read-Host "Enter choice"
} else {
    $choice = $Mode
}

switch ($choice) {
    "1" { Run-ApiTests }
    "2" { Run-Maestro }
    "3" { Run-MlServiceSim }
    "all" { Run-ApiTests; Run-Maestro; Run-MlServiceSim }
    default { Write-Host "Unknown choice: $choice"; exit 1 }
}

param(
    [ValidateSet("1","2","3","all","menu")]
    [string]$Mode = "menu"
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "INFO" { "Cyan" }
        "WARN" { "Yellow" }
        "ERROR" { "Red" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$ts][$Level] $Message" -ForegroundColor $color
}

function Assert-Command {
    param([string]$Name, [string]$Hint = "")
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing dependency: $Name. $Hint"
    }
}

function Ensure-Venv {
    param([string]$Path = ".venv")
    if (-not (Test-Path $Path)) {
        Write-Log "Creating virtualenv at $Path"
        python -m venv $Path
    }
    $activate = Join-Path $Path "Scripts\Activate.ps1"
    . $activate
}

function Wait-ForHealth {
    param([string]$Url, [int]$Attempts = 20, [int]$DelaySeconds = 2)
    for ($i = 0; $i -lt $Attempts; $i++) {
        try {
            $resp = Invoke-RestMethod -Uri $Url -TimeoutSec 5
            if ($resp.status -eq "ok") { return $true }
        } catch {}
        Start-Sleep -Seconds $DelaySeconds
    }
    return $false
}

function Write-Summary {
    param([hashtable]$Results)
    Write-Host ""
    Write-Host "===== Demo Summary =====" -ForegroundColor Cyan
    foreach ($k in $Results.Keys) {
        $status = $Results[$k]
        $color = if ($status -eq "PASS") { "Green" } elseif ($status -eq "FAIL") { "Red" } else { "Yellow" }
        Write-Host ("{0}: {1}" -f $k, $status) -ForegroundColor $color
    }
    Write-Host "========================" -ForegroundColor Cyan
}

function Cleanup-Process {
    param([System.Diagnostics.Process]$Proc)
    if ($Proc -and -not $Proc.HasExited) {
        Stop-Process -Id $Proc.Id -Force
    }
}

function Run-ApiTests {
    Write-Log "==> [1] API Tests"
    Assert-Command dotnet "Install .NET SDK 9.x"
    dotnet test api\Pocketree.Api.Tests\Pocketree.Api.Tests.csproj -c Release
}

function Run-Maestro {
    param([ValidateSet("smoke","full")] [string]$Suite = "smoke")
    Write-Log "==> [2] Maestro Flows ($Suite)"
    Write-Log "Maestro will start in 5 seconds. Alt-tab to the emulator now..."
    Start-Sleep -Seconds 5
    $env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21.0.10.7-hotspot"
    $env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
    $maestro = "C:\Users\skido\maestro\maestro\bin\maestro.bat"
    $flow = if ($Suite -eq "full") { "android\.maestro\flows\full-seq.yaml" } else { "android\.maestro\flows\smoke-seq.yaml" }
    & $maestro test $flow
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Run-MlServiceSim {
    param([int]$Port = 8080, [int]$Attempts = 20, [int]$DelaySeconds = 2)
    Write-Log "==> [3] ML Service Sim (health check)"
    Assert-Command python "Install Python 3.10+"
    Ensure-Venv
    python -m pip install --upgrade pip
    pip install -r ml-service\requirements.txt -r ml-service\requirements-dev.txt

    $env:PORT = "$Port"
    $proc = Start-Process -FilePath python -ArgumentList "ml-service\CLIPModelMobile_donotmerge.py" -PassThru
    try {
        $ok = Wait-ForHealth -Url "http://localhost:$Port/health" -Attempts $Attempts -DelaySeconds $DelaySeconds
        if (-not $ok) {
            throw "ML Service health check failed."
        }
        Write-Log "ML Service health check OK."
    } finally {
        Cleanup-Process -Proc $proc
    }
}

if ($Mode -eq "menu") {
    Write-Host ""
    Write-Host "Select a demo step:" -ForegroundColor Cyan
    Write-Host "  1) API Tests"
    Write-Host "  2) Maestro Flows (smoke-seq)"
    Write-Host "  2f) Maestro Flows (full-seq)"
    Write-Host "  3) ML Service Sim (health check)"
    Write-Host "  all) Run 1, 2, 3 in order"
    $choice = Read-Host "Enter choice"
} else {
    $choice = $Mode
}

$results = @{}
switch ($choice) {
    "1" { Run-ApiTests; $results["API Tests"] = "PASS" }
    "2" { Run-Maestro -Suite "smoke"; $results["Maestro Smoke"] = "PASS" }
    "2f" { Run-Maestro -Suite "full"; $results["Maestro Full"] = "PASS" }
    "3" { Run-MlServiceSim; $results["ML Service Sim"] = "PASS" }
    "all" {
        Run-ApiTests; $results["API Tests"] = "PASS"
        Run-Maestro -Suite "smoke"; $results["Maestro Smoke"] = "PASS"
        Run-MlServiceSim; $results["ML Service Sim"] = "PASS"
    }
    default { Write-Host "Unknown choice: $choice"; exit 1 }
}

if ($results.Count -gt 0) { Write-Summary -Results $results }

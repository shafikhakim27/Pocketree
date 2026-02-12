param(
    [ValidateSet("1", "2", "3", "4", "all", "menu")]
    [string]$Mode = "menu"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

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

function Run-Script {
    param([string]$Label, [scriptblock]$Action)
    Write-Log "==> $Label"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

function Deploy-MlMainVertex {
    param(
        [string]$ProjectId = "pocketree-ml-service",
        [string]$Region = "asia-southeast1",
        [string]$ArtifactRepo = "pocketree-ml",
        [string]$ImageName = "pocketree-ml",
        [string]$ModelDisplayName = "pocketree-ml",
        [string]$EndpointDisplayName = "pocketree-ml-endpoint",
        [string]$Tag = "",
        [string]$MachineType = "n1-standard-4",
        [string]$AcceleratorType = "nvidia-tesla-t4",
        [int]$AcceleratorCount = 1,
        [int]$MinReplicaCount = 1,
        [int]$MaxReplicaCount = 1,
        [string]$ContainerEnvVars = "ENABLE_LLM_WARMUP=0,GPT4ALL_MODEL_PATH=/app/models/Phi-3-mini-4k-instruct.Q4_0.gguf"
    )
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "manual-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $imageUri = "$Region-docker.pkg.dev/$ProjectId/$ArtifactRepo/${ImageName}:$Tag"
    Write-Host "Using image: $imageUri"

    gcloud config set project $ProjectId | Out-Null
    gcloud artifacts repositories describe $ArtifactRepo --location $Region | Out-Null
    gcloud auth configure-docker "$Region-docker.pkg.dev" --quiet
    docker build -f "ml-service/Dockerfile" -t $imageUri "ml-service"
    docker push $imageUri

    $endpointId = gcloud ai endpoints list --region $Region --filter "display_name=$EndpointDisplayName" --limit 1 --format "value(name)"
    if ([string]::IsNullOrWhiteSpace($endpointId)) {
        $endpointId = gcloud ai endpoints create --region $Region --display-name $EndpointDisplayName --format "value(name)"
    }

    $modelId = gcloud ai models upload `
        --region $Region `
        --display-name $ModelDisplayName `
        --container-image-uri $imageUri `
        --container-predict-route "/predict" `
        --container-health-route "/health" `
        --container-ports 8080 `
        --container-env-vars $ContainerEnvVars `
        --format "value(name)"

    $deployed = gcloud ai endpoints describe $endpointId --region $Region --format "value(deployedModels[].id)"
    if (-not [string]::IsNullOrWhiteSpace($deployed)) {
        $deployed -split ";" | ForEach-Object {
            $id = $_.Trim()
            if ($id) {
                gcloud ai endpoints undeploy-model $endpointId --region $Region --deployed-model-id $id
            }
        }
    }

    gcloud ai endpoints deploy-model $endpointId `
        --region $Region `
        --model $modelId `
        --display-name "manual-deploy-$Tag" `
        --machine-type $MachineType `
        --accelerator "type=$AcceleratorType,count=$AcceleratorCount" `
        --min-replica-count $MinReplicaCount `
        --max-replica-count $MaxReplicaCount `
        --traffic-split "0=100"
}

function Deploy-MlChatCloudRun {
    param(
        [string]$ProjectId = "pocketree-ml-service",
        [string]$Region = "asia-southeast1",
        [string]$ArtifactRepo = "pocketree-ml",
        [string]$ImageName = "pocketree-ml-v2",
        [string]$ServiceName = "pocketree-ml",
        [string]$Tag = ""
    )
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "manual-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $imageUri = "$Region-docker.pkg.dev/$ProjectId/$ArtifactRepo/${ImageName}:$Tag"
    Write-Host "Using image: $imageUri"

    gcloud config set project $ProjectId | Out-Null
    gcloud artifacts repositories describe $ArtifactRepo --location $Region | Out-Null
    gcloud auth configure-docker "$Region-docker.pkg.dev" --quiet
    docker build -f "ml-service-chat/Dockerfile" -t $imageUri "ml-service-chat"
    docker push $imageUri

    gcloud run deploy $ServiceName `
      --project $ProjectId `
      --region $Region `
      --platform managed `
      --image $imageUri `
      --port 8080 `
      --cpu 4 `
      --memory 12Gi `
      --timeout 1200 `
      --concurrency 2 `
      --min-instances 1 `
      --max-instances 3 `
      --cpu-boost `
      --ingress all `
      --set-env-vars "GUNICORN_CMD_ARGS=--timeout 600 --preload --workers 1" `
      --allow-unauthenticated
}

function Deploy-MlClassifyAzure {
    param(
        [string]$ResourceGroup = "pocketree-rg",
        [string]$WebAppName = "pocketree-ml-service",
        [string]$AcrName = "pockettreeacr",
        [string]$ImageName = "pocketree-ml-v3",
        [string]$Tag = ""
    )
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "manual-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $loginServer = "$AcrName.azurecr.io"
    $fullImage = "$loginServer/$ImageName`:$Tag"

    az acr login -n $AcrName
    docker build -f "ml-service-classify/Dockerfile" -t "$ImageName`:$Tag" "ml-service-classify"
    docker tag "$ImageName`:$Tag" $fullImage
    docker push $fullImage
    az webapp config container set -g $ResourceGroup -n $WebAppName --container-image-name $fullImage
    az webapp restart -g $ResourceGroup -n $WebAppName
}

function Deploy-ApiAzure {
    param(
        [string]$ResourceGroup = "pocketree-rg",
        [string]$WebAppName = "pocketree-api",
        [string]$AcrName = "pockettreeacr",
        [string]$ImageName = "pocketree-api",
        [string]$Tag = ""
    )
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = "manual-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    }
    $loginServer = "$AcrName.azurecr.io"
    $fullImage = "$loginServer/$ImageName`:$Tag"

    az acr login -n $AcrName
    docker build -f "api/Dockerfile" -t "$ImageName`:$Tag" "."
    docker tag "$ImageName`:$Tag" $fullImage
    docker push $fullImage
    az webapp config container set -g $ResourceGroup -n $WebAppName --container-image-name $fullImage
    az webapp restart -g $ResourceGroup -n $WebAppName
}

if ($Mode -eq "menu") {
    Write-Host ""
    Write-Log "Select deployment target:"
    Write-Host "  1) ML Main (Vertex AI)"
    Write-Host "  2) ML Chat (Cloud Run)"
    Write-Host "  3) ML Classify (Azure App Service)"
    Write-Host "  4) API Backend (Azure App Service)"
    Write-Host "  all) Run all deployments in order"
    $choice = Read-Host "Enter choice"
}
else {
    $choice = $Mode
}

$results = @{}
switch ($choice) {
    "1" {
        Run-Script -Label "ML Main Deploy" -Action { Deploy-MlMainVertex }
        $results["ML Main Deploy"] = "PASS"
    }
    "2" {
        Run-Script -Label "ML Chat Deploy" -Action { Deploy-MlChatCloudRun }
        $results["ML Chat Deploy"] = "PASS"
    }
    "3" {
        Run-Script -Label "ML Classify Deploy" -Action { Deploy-MlClassifyAzure }
        $results["ML Classify Deploy"] = "PASS"
    }
    "4" {
        Run-Script -Label "API Deploy" -Action { Deploy-ApiAzure }
        $results["API Deploy"] = "PASS"
    }
    "all" {
        Run-Script -Label "ML Main Deploy" -Action { Deploy-MlMainVertex }
        $results["ML Main Deploy"] = "PASS"
        Run-Script -Label "ML Chat Deploy" -Action { Deploy-MlChatCloudRun }
        $results["ML Chat Deploy"] = "PASS"
        Run-Script -Label "ML Classify Deploy" -Action { Deploy-MlClassifyAzure }
        $results["ML Classify Deploy"] = "PASS"
        Run-Script -Label "API Deploy" -Action { Deploy-ApiAzure }
        $results["API Deploy"] = "PASS"
    }
    default {
        throw "Unknown choice: $choice"
    }
}

Write-Host ""
Write-Log "===== Deploy Summary ====="
foreach ($k in $results.Keys) {
    Write-Log "${k}: $($results[$k])" "SUCCESS"
}
Write-Log "=========================="

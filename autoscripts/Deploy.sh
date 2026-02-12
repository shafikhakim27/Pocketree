#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-menu}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

log() {
  local level="${1:-INFO}"
  local msg="${2:-}"
  local color=""
  local reset="\033[0m"
  case "$level" in
    INFO) color="\033[36m" ;;
    WARN) color="\033[33m" ;;
    ERROR) color="\033[31m" ;;
    SUCCESS) color="\033[32m" ;;
    *) color="" ;;
  esac
  printf '%b[%s][%s] %s%b\n' "$color" "$(date '+%Y-%m-%d %H:%M:%S')" "$level" "$msg" "$reset"
}

run_script() {
  local label="$2"
  local func="$1"
  log INFO "==> ${label}"
  "${func}"
}

deploy_ml_main_vertex() {
  local project_id="${PROJECT_ID:-pocketree-ml-service}"
  local region="${REGION:-asia-southeast1}"
  local ar_repo="${AR_REPO:-pocketree-ml}"
  local image_name="${IMAGE_NAME:-pocketree-ml}"
  local model_display_name="${MODEL_DISPLAY_NAME:-pocketree-ml}"
  local endpoint_display_name="${ENDPOINT_DISPLAY_NAME:-pocketree-ml-endpoint}"
  local tag="${TAG:-manual-$(date -u +%Y%m%d-%H%M%S)}"
  local machine_type="${MACHINE_TYPE:-n1-standard-4}"
  local accelerator_type="${ACCELERATOR_TYPE:-nvidia-tesla-t4}"
  local accelerator_count="${ACCELERATOR_COUNT:-1}"
  local min_replica_count="${MIN_REPLICA_COUNT:-1}"
  local max_replica_count="${MAX_REPLICA_COUNT:-1}"
  local container_env_vars="${CONTAINER_ENV_VARS:-ENABLE_LLM_WARMUP=0,GPT4ALL_MODEL_PATH=/app/models/Phi-3-mini-4k-instruct.Q4_0.gguf}"

  local image_uri="${region}-docker.pkg.dev/${project_id}/${ar_repo}/${image_name}:${tag}"
  echo "Using image: ${image_uri}"
  gcloud config set project "${project_id}" >/dev/null
  gcloud artifacts repositories describe "${ar_repo}" --location "${region}" >/dev/null
  gcloud auth configure-docker "${region}-docker.pkg.dev" --quiet
  docker build -f ml-service/Dockerfile -t "${image_uri}" ml-service
  docker push "${image_uri}"

  local endpoint_id
  endpoint_id="$(gcloud ai endpoints list --region "${region}" --filter="display_name=${endpoint_display_name}" --limit=1 --format="value(name)")"
  if [ -z "${endpoint_id}" ]; then
    endpoint_id="$(gcloud ai endpoints create --region "${region}" --display-name "${endpoint_display_name}" --format="value(name)")"
  fi

  local model_id
  model_id="$(gcloud ai models upload \
    --region "${region}" \
    --display-name "${model_display_name}" \
    --container-image-uri "${image_uri}" \
    --container-predict-route "/predict" \
    --container-health-route "/health" \
    --container-ports 8080 \
    --container-env-vars "${container_env_vars}" \
    --format="value(name)")"

  local deployed_ids
  deployed_ids="$(gcloud ai endpoints describe "${endpoint_id}" --region "${region}" --format="value(deployedModels[].id)")"
  if [ -n "${deployed_ids}" ]; then
    IFS=';' read -r -a ids <<< "${deployed_ids}"
    for id in "${ids[@]}"; do
      id="$(echo "$id" | xargs)"
      [ -z "$id" ] && continue
      gcloud ai endpoints undeploy-model "${endpoint_id}" --region "${region}" --deployed-model-id "${id}"
    done
  fi

  gcloud ai endpoints deploy-model "${endpoint_id}" \
    --region "${region}" \
    --model "${model_id}" \
    --display-name "manual-deploy-${tag}" \
    --machine-type "${machine_type}" \
    --accelerator "type=${accelerator_type},count=${accelerator_count}" \
    --min-replica-count "${min_replica_count}" \
    --max-replica-count "${max_replica_count}" \
    --traffic-split "0=100"
}

deploy_ml_chat_cloudrun() {
  local project_id="${PROJECT_ID:-pocketree-ml-service}"
  local region="${REGION:-asia-southeast1}"
  local ar_repo="${AR_REPO:-pocketree-ml}"
  local image_name="${IMAGE_NAME:-pocketree-ml-v2}"
  local service_name="${SERVICE_NAME:-pocketree-ml}"
  local tag="${TAG:-manual-$(date -u +%Y%m%d-%H%M%S)}"

  local image_uri="${region}-docker.pkg.dev/${project_id}/${ar_repo}/${image_name}:${tag}"
  echo "Using image: ${image_uri}"
  gcloud config set project "${project_id}" >/dev/null
  gcloud artifacts repositories describe "${ar_repo}" --location "${region}" >/dev/null
  gcloud auth configure-docker "${region}-docker.pkg.dev" --quiet
  docker build -f ml-service-chat/Dockerfile -t "${image_uri}" ml-service-chat
  docker push "${image_uri}"

  gcloud run deploy "${service_name}" \
    --project "${project_id}" \
    --region "${region}" \
    --platform managed \
    --image "${image_uri}" \
    --port 8080 \
    --cpu 4 \
    --memory 12Gi \
    --timeout 1200 \
    --concurrency 2 \
    --min-instances 1 \
    --max-instances 3 \
    --cpu-boost \
    --ingress all \
    --set-env-vars "GUNICORN_CMD_ARGS=--timeout 600 --preload --workers 1" \
    --allow-unauthenticated
}

deploy_ml_classify_azure() {
  local resource_group="${RESOURCE_GROUP:-pocketree-rg}"
  local webapp_name="${WEBAPP_NAME:-pocketree-ml-service}"
  local acr_name="${ACR_NAME:-pockettreeacr}"
  local image_name="${IMAGE_NAME:-pocketree-ml-v3}"
  local tag="${TAG:-manual-$(date -u +%Y%m%d-%H%M%S)}"
  local login_server="${acr_name}.azurecr.io"
  local full_image="${login_server}/${image_name}:${tag}"

  az acr login -n "${acr_name}"
  docker build -f ml-service-classify/Dockerfile -t "${image_name}:${tag}" ml-service-classify
  docker tag "${image_name}:${tag}" "${full_image}"
  docker push "${full_image}"
  az webapp config container set -g "${resource_group}" -n "${webapp_name}" --container-image-name "${full_image}"
  az webapp restart -g "${resource_group}" -n "${webapp_name}"
}

deploy_api_azure() {
  local resource_group="${RESOURCE_GROUP:-pocketree-rg}"
  local webapp_name="${WEBAPP_NAME:-pocketree-api}"
  local acr_name="${ACR_NAME:-pockettreeacr}"
  local image_name="${IMAGE_NAME:-pocketree-api}"
  local tag="${TAG:-manual-$(date -u +%Y%m%d-%H%M%S)}"
  local login_server="${acr_name}.azurecr.io"
  local full_image="${login_server}/${image_name}:${tag}"

  az acr login -n "${acr_name}"
  docker build -f api/Dockerfile -t "${image_name}:${tag}" .
  docker tag "${image_name}:${tag}" "${full_image}"
  docker push "${full_image}"
  az webapp config container set -g "${resource_group}" -n "${webapp_name}" --container-image-name "${full_image}"
  az webapp restart -g "${resource_group}" -n "${webapp_name}"
}

if [ "$MODE" = "menu" ]; then
  echo ""
  log INFO "Select deployment target:"
  echo "  1) ML Main (Vertex AI)"
  echo "  2) ML Chat (Cloud Run)"
  echo "  3) ML Classify (Azure App Service)"
  echo "  4) API Backend (Azure App Service)"
  echo "  all) Run all deployments in order"
  read -r -p "Enter choice: " choice
else
  choice="$MODE"
fi

declare -A results
case "$choice" in
  1)
    run_script "deploy_ml_main_vertex" "ML Main Deploy"
    results["ML Main Deploy"]="PASS"
    ;;
  2)
    run_script "deploy_ml_chat_cloudrun" "ML Chat Deploy"
    results["ML Chat Deploy"]="PASS"
    ;;
  3)
    run_script "deploy_ml_classify_azure" "ML Classify Deploy"
    results["ML Classify Deploy"]="PASS"
    ;;
  4)
    run_script "deploy_api_azure" "API Deploy"
    results["API Deploy"]="PASS"
    ;;
  all)
    run_script "deploy_ml_main_vertex" "ML Main Deploy"
    results["ML Main Deploy"]="PASS"
    run_script "deploy_ml_chat_cloudrun" "ML Chat Deploy"
    results["ML Chat Deploy"]="PASS"
    run_script "deploy_ml_classify_azure" "ML Classify Deploy"
    results["ML Classify Deploy"]="PASS"
    run_script "deploy_api_azure" "API Deploy"
    results["API Deploy"]="PASS"
    ;;
  *)
    echo "Unknown choice: ${choice}" >&2
    exit 1
    ;;
esac

echo ""
log INFO "===== Deploy Summary ====="
for key in "${!results[@]}"; do
  log SUCCESS "${key}: ${results[$key]}"
done
log INFO "=========================="

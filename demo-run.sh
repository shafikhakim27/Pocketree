#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-menu}"

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

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing dependency: $1. ${2:-}" >&2
    exit 1
  fi
}

ensure_venv() {
  if [ ! -d ".venv" ]; then
    log INFO "Creating virtualenv at .venv"
    python -m venv .venv
  fi
  # shellcheck disable=SC1091
  source .venv/Scripts/activate
}

wait_for_health() {
  local url="$1"
  local attempts="${2:-20}"
  local delay="${3:-2}"
  for _ in $(seq 1 "$attempts"); do
    if curl -sS "$url" | grep -q '"status"'; then
      return 0
    fi
    sleep "$delay"
  done
  return 1
}

run_api_tests() {
  log INFO "==> [1] API Tests"
  require_cmd dotnet "Install .NET SDK 9.x"
  dotnet test api/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj -c Release
}

run_maestro() {
  local suite="${1:-smoke}"
  log INFO "==> [2] Maestro Flows ($suite)"
  log INFO "Maestro will start in 5 seconds. Alt-tab to the emulator now..."
  sleep 5
  export JAVA_HOME="${JAVA_HOME:-/c/Program Files/Eclipse Adoptium/jdk-21.0.10.7-hotspot}"
  export PATH="$JAVA_HOME/bin:$PATH"
  local flow="android/.maestro/flows/smoke-seq.yaml"
  if [ "$suite" = "full" ]; then
    flow="android/.maestro/flows/full-seq.yaml"
  fi
  /c/Users/skido/maestro/maestro/bin/maestro.bat test "$flow"
}

run_ml_service_sim() {
  local port="${PORT:-8080}"
  local attempts="${ATTEMPTS:-20}"
  local delay="${DELAY_SECONDS:-2}"
  log INFO "==> [3] ML Service Sim (health check)"
  require_cmd python "Install Python 3.10+"
  ensure_venv
  python -m pip install --upgrade pip
  pip install -r ml-service/requirements.txt -r ml-service/requirements-dev.txt

  PORT="$port" python ml-service/CLIPModelMobile_donotmerge.py &
  pid=$!
  trap 'kill $pid 2>/dev/null || true' EXIT

  if ! wait_for_health "http://localhost:${port}/health" "$attempts" "$delay"; then
    echo "ML Service health check failed."
    exit 1
  fi

  log INFO "ML Service health check OK."
}

if [ "$MODE" = "menu" ]; then
  echo ""
  log INFO "Select a demo step:"
  echo "  1) API Tests"
  echo "  2) Maestro Flows (smoke-seq)"
  echo "  2f) Maestro Flows (full-seq)"
  echo "  3) ML Service Sim (health check)"
  echo "  all) Run 1, 2, 3 in order"
  read -r -p "Enter choice: " choice
else
  choice="$MODE"
fi

declare -A results
case "$choice" in
  1) run_api_tests; results["API Tests"]="PASS" ;;
  2) run_maestro "smoke"; results["Maestro Smoke"]="PASS" ;;
  2f) run_maestro "full"; results["Maestro Full"]="PASS" ;;
  3) run_ml_service_sim; results["ML Service Sim"]="PASS" ;;
  all) run_api_tests; results["API Tests"]="PASS"; run_maestro "smoke"; results["Maestro Smoke"]="PASS"; run_ml_service_sim; results["ML Service Sim"]="PASS" ;;
  *) echo "Unknown choice: $choice"; exit 1 ;;
esac

if [ "${#results[@]}" -gt 0 ]; then
  echo ""
  log INFO "===== Demo Summary ====="
  for k in "${!results[@]}"; do
    if [ "${results[$k]}" = "PASS" ]; then
      log SUCCESS "$k: ${results[$k]}"
    elif [ "${results[$k]}" = "FAIL" ]; then
      log ERROR "$k: ${results[$k]}"
    else
      log WARN "$k: ${results[$k]}"
    fi
  done
  log INFO "========================"
fi

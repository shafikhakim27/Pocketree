#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-menu}"

run_api_tests() {
  echo "==> [1] API Tests"
  dotnet test api/Pocketree.Api.Tests/Pocketree.Api.Tests.csproj -c Release
}

run_maestro() {
  echo "==> [2] Maestro Flows (smoke-seq)"
  echo "Maestro will start in 5 seconds. Alt-tab to the emulator now..."
  sleep 5
  export JAVA_HOME="${JAVA_HOME:-/c/Program Files/Eclipse Adoptium/jdk-21.0.10.7-hotspot}"
  export PATH="$JAVA_HOME/bin:$PATH"
  /c/Users/skido/maestro/maestro/bin/maestro.bat test "android/.maestro/flows/smoke-seq.yaml"
}

run_ml_service_sim() {
  echo "==> [3] ML Service Sim (health check)"
  python -m pip install --upgrade pip
  pip install -r ml-service/requirements.txt -r ml-service/requirements-dev.txt

  python ml-service/CLIPModelMobile_donotmerge.py &
  pid=$!
  trap 'kill $pid 2>/dev/null || true' EXIT

  ok=false
  for _ in {1..20}; do
    if curl -sS http://localhost:8080/health | grep -q '"status"'; then
      ok=true
      break
    fi
    sleep 2
  done

  if [ "$ok" != "true" ]; then
    echo "ML Service health check failed."
    exit 1
  fi

  echo "ML Service health check OK."
}

if [ "$MODE" = "menu" ]; then
  echo ""
  echo "Select a demo step:"
  echo "  1) API Tests"
  echo "  2) Maestro Flows (smoke-seq)"
  echo "  3) ML Service Sim (health check)"
  echo "  all) Run 1, 2, 3 in order"
  read -r -p "Enter choice: " choice
else
  choice="$MODE"
fi

case "$choice" in
  1) run_api_tests ;;
  2) run_maestro ;;
  3) run_ml_service_sim ;;
  all) run_api_tests; run_maestro; run_ml_service_sim ;;
  *) echo "Unknown choice: $choice"; exit 1 ;;
esac

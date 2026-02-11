import os

import httpx
import pytest


BASE_URL = os.getenv("ML_SERVICE_URL")


def _require_base_url() -> str:
    if not BASE_URL:
        pytest.skip("Set ML_SERVICE_URL (e.g., http://localhost:8080) to run integration tests.")
    return BASE_URL.rstrip("/")


def test_health_ok():
    base_url = _require_base_url()
    with httpx.Client(base_url=base_url, timeout=30.0) as client:
        resp = client.get("/health")
    assert resp.status_code == 200
    data = resp.json()
    assert data.get("status") == "ok"


def test_predict_bundle_shape():
    base_url = _require_base_url()
    payload = {
        "user_id": 1,
        "totalScore": 250,
        "preferredDifficulty": "Normal",
        "preferredCategory": "nature",
        "tasks": ["Recycle a plastic bottle"],
        "not_attempted": 0,
        "failed_verifications": 0,
        "earliest_login_date": None,
        "last_activity_date": None,
    }

    with httpx.Client(base_url=base_url, timeout=120.0) as client:
        resp = client.post("/predict", json=payload)

    assert resp.status_code == 200
    data = resp.json()
    assert "user_tier" in data
    assert "tasks" in data
    assert isinstance(data["tasks"], list)
    assert len(data["tasks"]) == 3

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


def test_chat_response_shape():
    base_url = _require_base_url()
    payload = {
        "user_id": "test_user",
        "message": "Hello, can you share a quick sustainability tip?",
    }

    with httpx.Client(base_url=base_url, timeout=120.0) as client:
        resp = client.post("/chat", json=payload)

    assert resp.status_code == 200
    data = resp.json()
    assert data.get("bot") == "PockeTree"
    assert "response" in data

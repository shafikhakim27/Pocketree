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


def test_classify_response_shape():
    base_url = _require_base_url()
    # Minimal 1x1 JPEG image bytes.
    image_bytes = (
        b"\xff\xd8\xff\xe0\x00\x10JFIF\x00\x01\x01\x00\x00\x01\x00\x01\x00\x00"
        b"\xff\xdb\x00C\x00\x08\x06\x06\x07\x06\x05\x08\x07\x07\x07\x09\x09\x08"
        b"\x0a\x0c\x14\x0d\x0c\x0b\x0b\x0c\x19\x12\x13\x0f"
        b"\xff\xc0\x00\x0b\x08\x00\x01\x00\x01\x01\x01\x11\x00\xff\xc4\x00\x14"
        b"\x00\x01\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x08\xff"
        b"\xda\x00\x08\x01\x01\x00\x00?\x00\xd2\xcf \xff\xd9"
    )

    with httpx.Client(base_url=base_url, timeout=120.0) as client:
        resp = client.post(
            "/classify",
            data={"keyword": "tree"},
            files={"file": ("test.jpg", image_bytes, "image/jpeg")},
        )

    assert resp.status_code == 200
    data = resp.json()
    assert "verified" in data
    assert "confidence" in data

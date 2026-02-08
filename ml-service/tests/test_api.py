import base64
from io import BytesIO

from fastapi.testclient import TestClient
from PIL import Image

import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parents[1]))

import CLIPModelMobile_donotmerge as app_module


def _b64_image() -> str:
    img = Image.new("RGB", (1, 1), color=(255, 255, 255))
    buf = BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")


def test_health_ok():
    client = TestClient(app_module.app)
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}


def test_predict_invalid_base64_returns_400():
    client = TestClient(app_module.app)
    resp = client.post("/predict", json={"keyword": "tree", "image_base64": "not_base64"})
    assert resp.status_code == 400
    assert resp.json()["detail"] == "Invalid image_base64"


def test_predict_calls_classifier(monkeypatch):
    def fake_classify(_img, keyword):
        return {"verified": True, "score": 0.9, "raw_sim": 0.42, "keyword": keyword}

    monkeypatch.setattr(app_module, "_classify_image", fake_classify)

    client = TestClient(app_module.app)
    resp = client.post("/predict", json={"keyword": "tree", "image_base64": _b64_image()})
    assert resp.status_code == 200
    data = resp.json()
    assert data["verified"] is True
    assert data["score"] == 0.9
    assert data["raw_sim"] == 0.42
    assert data["keyword"] == "tree"

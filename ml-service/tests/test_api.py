from importlib import util
from pathlib import Path

from fastapi.testclient import TestClient


def _load_main_model_module():
    module_path = Path(__file__).resolve().parents[1] / "MainModelML.py"
    spec = util.spec_from_file_location("main_model_ml", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load module from {module_path}")
    module = util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


MainModelML = _load_main_model_module()


def _ensure_tree_fitted():
    X = [
        [0, 5, 0, 0, 0],
        [300, 20, 0, 2, 1],
        [1000, 80, 0, 1, 0],
    ]
    y = [0, 1, 2]
    MainModelML.models["clf"].fit(X, y)


def test_health_ok():
    client = TestClient(MainModelML.app)
    resp = client.get("/health")

    assert resp.status_code == 200
    data = resp.json()
    assert data["status"] == "ok"
    assert "llm_loaded" in data


def test_predict_empty_instances_returns_400():
    client = TestClient(MainModelML.app)
    resp = client.post("/predict", json={"instances": []})

    assert resp.status_code == 400
    assert "instances must not be empty" in resp.json()["detail"]


def test_predict_returns_vertex_predictions_shape(monkeypatch):
    _ensure_tree_fitted()

    async def fake_generate_single_task(difficulty, category, history, reward):
        return {
            "Description": "Test task.",
            "Difficulty": difficulty,
            "CoinReward": reward,
            "RequiresEvidence": False,
            "Keyword": [category],
            "NegativeKeyword": ["person"],
            "Category": category,
        }

    monkeypatch.setattr(MainModelML, "generate_single_task", fake_generate_single_task)

    client = TestClient(MainModelML.app)
    payload = {
        "instances": [
            {
                "user_id": 1,
                "totalScore": 250,
                "preferredDifficulty": "Normal",
                "preferredCategory": "nature",
                "tasks": ["Recycle a bottle"],
                "not_attempted": 0,
                "failed_verifications": 0,
                "last_activity_date": "2026-02-12T00:00:00Z",
            }
        ]
    }

    resp = client.post("/predict", json=payload)
    assert resp.status_code == 200
    data = resp.json()
    assert "predictions" in data
    assert len(data["predictions"]) == 1
    prediction = data["predictions"][0]
    assert "user_tier" in prediction
    assert "tasks" in prediction
    assert len(prediction["tasks"]) == 1


def test_predict_llm_failure_returns_empty_tasks(monkeypatch):
    _ensure_tree_fitted()

    async def failing_generate_single_task(*args, **kwargs):
        raise RuntimeError("boom")

    monkeypatch.setattr(MainModelML, "generate_single_task", failing_generate_single_task)

    client = TestClient(MainModelML.app)
    payload = {
        "instances": [
            {
                "user_id": 2,
                "totalScore": 1200,
                "preferredDifficulty": "Normal",
                "preferredCategory": "food",
                "tasks": [],
                "not_attempted": 0,
                "failed_verifications": 0,
                "last_activity_date": "2026-02-12T00:00:00Z",
            }
        ]
    }

    resp = client.post("/predict", json=payload)
    assert resp.status_code == 200
    prediction = resp.json()["predictions"][0]
    assert prediction["tasks"] == []

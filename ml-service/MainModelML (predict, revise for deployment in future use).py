import os
import random
import re
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from threading import Lock
from typing import List, Optional

import anyio
from fastapi import FastAPI, HTTPException, Request
from gpt4all import GPT4All
from pydantic import BaseModel, Field
from sklearn.tree import DecisionTreeClassifier


ALLOWED_CATEGORIES = ["reuse", "reduce", "recycle", "food", "nature", "exercise"]

tiers = {
    0: "Newbie",
    1: "Consistent",
    2: "Pro",
    3: "Casual",
    4: "Returning",
    5: "Hibernating",
}

models = {
    "clf": DecisionTreeClassifier(),
    "llm": None,
}

_llm_lock = Lock()


def _env_flag(name: str, default: bool = False) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def _llm_model_path() -> str:
    return os.getenv(
        "GPT4ALL_MODEL_PATH",
        "/app/models/Phi-3-mini-4k-instruct.Q4_0.gguf",
    )


def _ensure_llm_loaded() -> None:
    if models["llm"] is not None:
        return

    with _llm_lock:
        if models["llm"] is not None:
            return

        model_path = _llm_model_path()
        if not os.path.exists(model_path):
            raise FileNotFoundError(
                f"GPT4All model not found at '{model_path}'. "
                "Set GPT4ALL_MODEL_PATH or include the .gguf in the image."
            )

        models["llm"] = GPT4All(model_path, device="cpu")
        print(f"LLM loaded: {model_path}")


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Features: [TotalCoins, Total#ofTasks, DaysSinceLastActivity, %NotAttempted, %FailedVerification]
    X = [
        [0, 5, 0, 0, 0],
        [100, 10, 0, 5, 0],
        [200, 15, 1, 10, 5],
        [300, 20, 0, 2, 1],
        [500, 40, 0, 3, 1],
        [600, 50, 1, 5, 2],
        [1000, 80, 0, 1, 0],
        [1200, 100, 0, 1, 0],
        [5000, 500, 0, 0, 0],
        [300, 50, 2, 30, 5],
        [500, 100, 5, 40, 10],
        [600, 100, 15, 5, 2],
        [1000, 200, 20, 2, 1],
        [400, 200, 45, 10, 5],
        [1000, 500, 60, 5, 2],
    ]
    y = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 4, 5, 5]
    models["clf"].fit(X, y)

    if _env_flag("ENABLE_LLM_WARMUP", default=False):
        try:
            _ensure_llm_loaded()
        except Exception as exc:
            # Startup stays alive by default; inference can still return fallback response.
            print(f"LLM warmup skipped: {exc}")

    print("Model service startup completed.")
    yield


app = FastAPI(lifespan=lifespan)


@app.middleware("http")
async def log_requests(request: Request, call_next):
    if _env_flag("ENABLE_REQUEST_LOGGING", default=False):
        body = await request.body()
        print(f"DEBUG incoming body: {body.decode(errors='replace')}")
    return await call_next(request)


class TaskData(BaseModel):
    user_id: int
    totalScore: int
    not_attempted: int
    failed_verifications: int
    last_activity_date: str
    preferredDifficulty: str
    preferredCategory: str
    tasks: List[str]


class VertexRequest(BaseModel):
    instances: List[TaskData]


class TaskRequest(BaseModel):
    user_id: int
    total_coins: int = Field(..., alias="totalScore")
    not_attempted: int = 0
    failed_verifications: int = 0
    earliest_login_date: Optional[str] = None
    last_activity_date: Optional[str] = None
    preferred_difficulty: str = Field("Normal", alias="preferredDifficulty")
    preferred_category: str = Field("General", alias="preferredCategory")
    historical_tasks: List[str] = Field(default_factory=list, alias="tasks")

    class Config:
        populate_by_name = True


async def get_llm_response(prompt: str) -> str:
    _ensure_llm_loaded()
    llm = models["llm"]
    if llm is None:
        raise RuntimeError("LLM is not loaded.")
    return await anyio.to_thread.run_sync(llm.generate, prompt)


async def generate_single_task(
    difficulty: str, category: str, history: List[str], reward: int
):
    category_data = {
        "reuse": [
            "glass jar",
            "container",
            "cardboard box",
            "old cloth",
            "reusable shopping bag",
            "fabric",
            "old furniture",
            "old wood",
        ],
        "reduce": [
            "light switch",
            "water tap",
            "faucet",
            "power plug",
            "led bulb",
            "electronics",
            "public transport",
            "EZ-Link card",
            "disposables",
            "food waste",
            "reusable container",
        ],
        "food": [
            "apple core",
            "compost bin",
            "vegetable scraps",
            "reusable plate",
            "leftovers",
            "local produce",
            "empty plate",
            "reusable container",
            "homecooked meal",
            "reusable cutleries",
            "reusable straws",
        ],
        "nature": [
            "green leaf",
            "flower",
            "tree trunk",
            "public park",
            "soil",
            "grass",
            "park",
            "greenery",
            "animal",
            "binocular",
            "local birds",
            "leaves",
            "local plants",
        ],
        "exercise": [
            "walking shoes",
            "bicycle",
            "water bottle",
            "sneakers",
            "helmet",
            "outdoor",
            "people exercising",
            "people meditate",
        ],
        "recycle": [
            "plastic bottle",
            "aluminum can",
            "newspaper",
            "recycling bin",
            "paper",
            "bottles",
            "jars",
            "glass",
        ],
    }

    hints = {
        "exercise": "Focus on human-powered transport like walking or cycling instead of driving.",
        "nature": "Focus on observing or appreciating local biodiversity without harming it.",
        "reduce": "Focus on conservation of energy, water, or resources.",
        "reuse": "Focus on giving a second life to household items or daily necessities.",
        "food": "Focus on sustainable eating habits or composting.",
        "recycle": "Focus on the correct sorting and disposal of waste.",
    }

    prompt = f"""
    You are an Eco-Task Architect. Your goal is to generate safe, legal, and pro-environmental tasks.
    Category Definition: {hints.get(category, "Eco-friendly action.")}
    Write a short {difficulty} eco-task, no more than 255 characters, for the category '{category}'.
    Format: Task description | 3-4 specific visual objects to photograph related to the task description (comma separated).
    Avoid these: {", ".join(history) if history else "None"}
    Example: Pick up a discarded plastic bottle | plastic bottle
    Task:"""

    raw_response = await get_llm_response(prompt)
    clean_text = re.sub(r"<<.*?>>|\[.*?\]|\bSYS\b|\bTASK\b", "", raw_response)
    desc_only = clean_text.replace('"', "").split("|")[0].strip()
    desc_split = re.split(r"[\n.]", desc_only)
    desc_final = next(
        (sentence.strip() for sentence in desc_split if len(sentence.strip()) > 5),
        "Perform an eco-friendly action.",
    )
    final_description = desc_final.rstrip(".") + "."

    all_potential = category_data.get(category, ["nature"])
    matched_kws = []
    desc_lower = final_description.lower()
    for keyword in all_potential:
        keyword_parts = keyword.lower().split()
        if any(part[:4] in desc_lower for part in keyword_parts if len(part) > 2):
            matched_kws.append(keyword)

    if not matched_kws:
        matched_kws = random.sample(all_potential, min(len(all_potential), 2))

    final_keywords = list(set(matched_kws + [category]))

    return {
        "Description": final_description[:255],
        "Difficulty": difficulty,
        "CoinReward": reward,
        "RequiresEvidence": difficulty == "Hard",
        "Keyword": final_keywords,
        "NegativeKeyword": ["person", "blur", "text", "screenshot"],
        "Category": category,
    }


@app.get("/")
def root():
    return {"status": "ok"}


@app.get("/health")
def health():
    return {
        "status": "ok",
        "llm_loaded": models["llm"] is not None,
        "llm_warmup_enabled": _env_flag("ENABLE_LLM_WARMUP", default=False),
    }


@app.post("/predict")
async def predict(request: VertexRequest):
    if not request.instances:
        raise HTTPException(status_code=400, detail="instances must not be empty")

    data = request.instances[0]
    internal_request = TaskRequest(**data.model_dump())
    result = await predict_bundle(internal_request)
    return {"predictions": [result]}


async def predict_bundle(req: TaskRequest):
    now = datetime.now(timezone.utc).replace(tzinfo=None)

    try:
        if req.last_activity_date:
            dt = datetime.fromisoformat(req.last_activity_date.replace("Z", "+00:00"))
            last_act_dt = dt.replace(tzinfo=None)
        else:
            last_act_dt = now
    except Exception as exc:
        print(f"Date parsing error: {exc}")
        last_act_dt = now

    days_since_act = (now - last_act_dt).days
    calc_window = min(max(1, req.total_coins // 1500), 30)
    total_tasks_pot = calc_window * 3
    pct_not_attempted = (req.not_attempted / max(1, total_tasks_pot)) * 100
    pct_failed = (req.failed_verifications / max(1, calc_window)) * 100

    features = [[req.total_coins, total_tasks_pot, days_since_act, pct_not_attempted, pct_failed]]
    pred = int(models["clf"].predict(features)[0])
    user_tier = tiers.get(pred, "Newbie")

    if req.total_coins >= 1000 and pct_failed < 5:
        user_tier = "Pro"
    elif req.total_coins >= 250 and user_tier == "Newbie":
        user_tier = "Consistent"

    pref = req.preferred_category.lower()
    target_pref = pref if pref in ALLOWED_CATEGORIES else "nature"

    try:
        ai_task = await generate_single_task(
            difficulty="Normal",
            category=target_pref,
            history=req.historical_tasks,
            reward=500,
        )
    except Exception as exc:
        print(f"AI model error: {exc}")
        return {
            "user_tier": user_tier,
            "tasks": [],
        }

    return {
        "user_tier": user_tier,
        "tasks": [ai_task],
    }


if __name__ == "__main__":
    import uvicorn

    port = int(os.environ.get("PORT", 8080))
    print(f"Starting server on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port, log_level="info")

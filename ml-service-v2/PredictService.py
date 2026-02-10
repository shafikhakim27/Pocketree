import anyio
import random
import re

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from fastapi import FastAPI, HTTPException
from gpt4all import GPT4All
from pydantic import BaseModel, Field
from sklearn.tree import DecisionTreeClassifier
from typing import List, Optional


# Allowed categories
ALLOWED_CATEGORIES = ["reuse", "reduce", "recycle", "food", "nature", "exercise"]

# Model registry
models = {
    "clf": DecisionTreeClassifier(),
    "llm": None,
}

tiers = {0: "Newbie", 1: "Consistent", 2: "Pro", 3: "Casual", 4: "Returning", 5: "Hibernating"}


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Pre-train Decision Tree
    X = [
        # --- NEWBIES (Tier 0) ---
        [0, 5, 0, 0, 0], [100, 10, 0, 5, 0], [200, 15, 1, 10, 5],
        # --- CONSISTENT (Tier 1) ---
        [300, 20, 0, 2, 1], [500, 40, 0, 3, 1], [600, 50, 1, 5, 2],
        # --- PRO (Tier 2) ---
        [1000, 80, 0, 1, 0], [1200, 100, 0, 1, 0], [5000, 500, 0, 0, 0],
        # --- CASUAL (Tier 3) ---
        [300, 50, 2, 30, 5], [500, 100, 5, 40, 10],
        # --- RETURNING (Tier 4) ---
        [600, 100, 15, 5, 2], [1000, 200, 20, 2, 1],
        # --- HIBERNATING (Tier 5) ---
        [400, 200, 45, 10, 5], [1000, 500, 60, 5, 2]
    ]

    y = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 4, 5, 5]
    models["clf"].fit(X, y)

    # Load LLM
    models["llm"] = GPT4All("Meta-Llama-3.1-8B-Instruct-128k-Q4_0.gguf", device="cpu")
    print("Predict models loaded & trained!")
    yield


app = FastAPI(lifespan=lifespan)


class TaskRequest(BaseModel):
    user_id: int
    # Accept "totalScore" but use "total_coins" in code
    total_coins: int = Field(..., alias="totalScore")

    # "Optional" guardrails
    not_attempted: int = 0
    failed_verifications: int = 0

    # To prevent 422 errors
    earliest_login_date: Optional[str] = None
    last_activity_date: Optional[str] = None

    preferred_difficulty: str = Field("Normal", alias="preferredDifficulty")
    preferred_category: str = Field("General", alias="preferredCategory")

    historical_tasks: List[str] = Field(default_factory=list, alias="tasks")

    class Config:
        populate_by_name = True


async def get_llm_response(prompt: str):
    return await anyio.to_thread.run_sync(models["llm"].generate, prompt)  # type: ignore[union-attr]


async def generate_single_task(difficulty: str, category: str, history: list, reward: int):
    category_data = {
        "reuse": ["glass jar", "container", "cardboard box", "old cloth", "reusable shopping bag", "fabric", "old furniture", "old wood"],
        "reduce": ["light switch", "water tap", "faucet", "power plug", "led bulb", "electronics", "public transport", "EZ-Link card", "disposables", "food waste", "reusable container"],
        "food": ["apple core", "compost bin", "vegetable scraps", "reusable plate", "leftovers", "local produce", "empty plate", "reusable container", "homecooked meal", "reusable cutleries", "reusable straws"],
        "nature": ["green leaf", "flower", "tree trunk", "public park", "soil", "grass", "park", "greenery", "animal", "binocular", "local birds", "leaves", "local plants"],
        "exercise": ["walking shoes", "bicycle", "water bottle", "sneakers", "helmet", "outdoor", "people exercising", "people meditate"],
        "recycle": ["plastic bottle", "aluminum can", "newspaper", "recycling bin", "paper", "bottles", "jars", "glass"],
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

    clean_text = re.sub(r"<<.*?>>|\\[.*?\\]|\\bSYS\\b|\\bTASK\\b", "", raw_response)
    clean_text = clean_text.replace('"', '').split("|")[0]

    desc_only = clean_text.replace('"', '').split("|")[0].strip()
    desc_split = re.split(r"[\\n.]", desc_only)
    desc_final = next((s.strip() for s in desc_split if len(s.strip()) > 5), "Perform an eco-friendly action.")
    final_description = "*" + desc_final.rstrip(".") + "."

    all_potential = category_data.get(category, ["nature"])

    matched_kws = []
    desc_lower = final_description.lower()
    for k in all_potential:
        keyword_parts = k.lower().split()
        if any(part[:4] in desc_lower for part in keyword_parts if len(part) > 2):
            matched_kws.append(k)

    if not matched_kws:
        matched_kws = random.sample(all_potential, min(len(all_potential), 2))

    final_keywords = list(set(matched_kws + [category]))

    return {
        "Description": final_description[:255],
        "Difficulty": difficulty,
        "CoinReward": reward,
        "RequiresEvidence": (difficulty == "Hard"),
        "Keyword": final_keywords,
        "NegativeKeyword": ["person", "blur", "text", "screenshot"],
        "Category": category,
    }


@app.post("/predict")
async def predict_bundle(req: TaskRequest):
    if models.get("llm") is None:
        raise HTTPException(status_code=503, detail="LLM disabled for speed test")

    now = datetime.now(timezone.utc)
    try:
        last_act_dt = datetime.fromisoformat(req.last_activity_date.replace("Z", "+00:00")) if req.last_activity_date else now
    except Exception:
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
    remaining_cats = [c for c in ALLOWED_CATEGORIES if c != target_pref]
    random.shuffle(remaining_cats)

    bundle_plan = [
        {"diff": "Hard", "reward": 750, "cat": remaining_cats[1]},
        {"diff": "Normal", "reward": 500, "cat": target_pref},
        {"diff": "Easy", "reward": 250, "cat": remaining_cats[0]},
    ]

    daily_bundle = []
    for plan in bundle_plan:
        task = await generate_single_task(
            difficulty=plan["diff"],
            category=plan["cat"],
            history=req.historical_tasks,
            reward=plan["reward"],
        )
        daily_bundle.append(task)

    return {"user_tier": user_tier, "tasks": daily_bundle}


@app.get("/health")
def health():
    return {"status": "ok"}


if __name__ == "__main__":
    import uvicorn
    import os

    try:
        port = int(os.environ.get("PORT", 8080))
        uvicorn.run(app, host="0.0.0.0", port=port, log_level="info")
        print(f"Starting server on port {port}")
    except KeyboardInterrupt:
        print("\nShutting down PockeTree gracefully... Bye!")

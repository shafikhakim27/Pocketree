import anyio
import httpx
import io, torch, time, base64, json
import numpy as np
import open_clip, pymysql
import pickle
import re
import random

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from dbutils.pooled_db import PooledDB
from fastapi import FastAPI, Form, UploadFile, File, HTTPException, Request
from gpt4all import GPT4All
from io import BytesIO
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.document_loaders import PyPDFLoader
from PIL import Image, ImageOps
from pydantic import BaseModel, Field
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from sklearn.tree import DecisionTreeClassifier
from typing import List, Optional
from typing import List
from transformers import pipeline

# Allowed categories
ALLOWED_CATEGORIES = ["reuse", "reduce", "recycle", "food", "nature", "exercise"]

# Global placeholders
preprocess, tokenizer = None, None
chat_histories = {}
user_profiles = {}

# Best available engine
if torch.cuda.is_available():
    device = "cuda" # Google Cloud GPU
elif torch.backends.mps.is_available():
    device = "mps"  # Mac M1/M2/M3 GPU acceleration
else:
    device = "cpu"  # Local fallback

models = {
    #"clip": None, 
    "clf": DecisionTreeClassifier(), 
    "llm": None, 
    #"sustain_bot": None
    }

tiers = {0: "Newbie", 1: "Consistent", 2: "Pro", 3: "Casual", 4: "Returning", 5: "Hibernating"}


# --- WARM UP ---

@asynccontextmanager
async def lifespan(app: FastAPI):

    global models

    #models["clip"] = CLIPService()
    #models["clip"].ensure_model()
    
    # Features: [TotalCoins, Total#ofTasks, DaysSinceLastActivity, %NotAttempted, %FailedVerification]
    # Current taks reward: 250, 500, 750

    # Pre-train Decision Tree
    # X = [
    # # --- NEWBIES (Seedling: < 250) ---
    # [0, 3, 0, 0, 0],         # 0: Newbie
    # [150, 15, 0, 10, 5],     # 0: Newbie (Active)

    # # --- CONSISTENT (Sapling: 250) ---
    # [250, 20, 0, 5, 2],      # 1: Consistent (Just became Sapling)
    # [600, 60, 1, 8, 3],      # 1: Consistent (Mid-Sapling)

    # # --- PRO (Mighty Oak: 500) ---
    # [500, 80, 0, 2, 1],      # 2: Pro (Just became Mighty Oak)
    # [1200, 150, 0, 1, 0],    # 2: Pro (Veteran Tree)

    # # --- CASUAL / RETURNING ---
    # [300, 100, 2, 40, 10],   # 3: Casual (Sapling level but lazy)
    # [800, 120, 15, 5, 2],    # 4: Returning (Tree level but away 15 days)
    # [800, 1000, 60, 2, 1],   # 4: Returning (High coins and huge gap)

    # # --- HIBERNATE ---
    # [400, 200, 45, 10, 5],   # 5: Hibernating (Away > 31 days)
    # [250, 500, 32, 0.1, 0],  # 5: Hibernating (Low coins)
    # ]

    # y = [0, 0, 1, 1, 2, 2, 3, 4, 4, 5, 5]

    X = [
        # --- NEWBIES (Tier 0) ---
        [0, 5, 0, 0, 0], [100, 10, 0, 5, 0], [200, 15, 1, 10, 5],
        
        # --- CONSISTENT (Tier 1) ---
        [300, 20, 0, 2, 1], [500, 40, 0, 3, 1], [600, 50, 1, 5, 2],
        
        # --- PRO (Tier 2) - Reinforce this section! ---
        [1000, 80, 0, 1, 0], [1200, 100, 0, 1, 0], [5000, 500, 0, 0, 0],
        
        # --- CASUAL (Tier 3) ---
        [300, 50, 2, 30, 5], [500, 100, 5, 40, 10],
        
        # --- RETURNING (Tier 4) ---
        [600, 100, 15, 5, 2], [1000, 200, 20, 2, 1],
        
        # --- HIBERNATING (Tier 5) ---
        [400, 200, 45, 10, 5], [1000, 500, 60, 5, 2]
    ]

    y = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 4, 5, 5]

    # y Labels Logic: 
    # 0: Newbie: < 250 Coins: Just starting. Low friction data.
    # 1: Consistent: 250 - 500 Coins: Active Sapling. Low NotAttempted and FailedVerification.
    # 2: Pro: > 500 Coins: Mighty Oak, less than 5% of NotAttempted and FailedVerification.
    # 3: Casual: Any Level: More than 10% of NotCompleted tasks, but low DaysSinceLastCompletion
    # 4: Returning: Any Level: DaysSinceLastCompletion is between 7 and 30.
    # 5: Hibernating: Any Level: DaysSinceLastCompletion is >31

    models["clf"].fit(X, y)
    
    # Load LLM
    #models["llm"] = GPT4All("Meta-Llama-3.1-8B-Instruct-128k-Q4_0.gguf", device='cpu')
    models["llm"] = GPT4All("Phi-3-mini-4k-instruct.Q4_0.gguf", device='cpu')
    #models["llm"] = GPT4All("Qwen/Qwen2.5-1.5B-Instruct", device='cpu')
    
    #brainBot = EmbeddingBrain()
    #await brainBot.cloud_warmup(SUSTAINABILITY_REPORTS)
    
    #models["sustain_bot"] = PockeTreeBot(brainBot)
    
    print("All models loaded & trained!")
    yield
    #db_pool.destroy()

app = FastAPI(lifespan=lifespan)

@app.middleware("http")
async def log_requests(request: Request, call_next):
    # Log the incoming body to the terminal
    body = await request.body()
    print(f"DEBUG - Incoming Body: {body.decode()}")
    
    response = await call_next(request)
    return response

# --- DATABASE SETUP ---

#DB_CONFIG = {
#    "host": "127.0.0.1",
#    "port": 3306,
#    "user": "root",
#    "password": "password", 
#    "database": "PocketreeDb",
#    "cursorclass": pymysql.cursors.DictCursor
#}

#db_pool = PooledDB(
#    creator=pymysql, 
#    mincached=2, 
#    maxcached=5, 
#    **DB_CONFIG
#)

### --- USE CASE 2: DYNAMIC TASK GENERATION (DECISION TREE TRAINING & LLM) --- ###
class TaskData(BaseModel):
    user_id: int
    totalScore: int
    not_attempted: int
    failed_verifications: int
    last_activity_date: str
    preferredDifficulty: str
    preferredCategory: str
    tasks: List[str]

# 2. Create the wrapper that Vertex AI sends
class VertexRequest(BaseModel):
    instances: List[TaskData]
    
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

# models = {
#     "clip": CLIPService(),
#     "clf": DecisionTreeClassifier(),
#     "llm": None
# }

async def get_llm_response(prompt: str):
    # This prevents the CPU-heavy LLM from blocking the FastAPI event loop
    return await anyio.to_thread.run_sync(models["llm"].generate, prompt) # pyright: ignore[reportAttributeAccessIssue]

async def generate_single_task(difficulty: str, category: str, history: list, reward: int):

    category_data = {
        "reuse": ["glass jar", "container", "cardboard box", "old cloth", "reusable shopping bag", "fabric", "old furniture", "old wood"],
        "reduce": ["light switch", "water tap", "faucet", "power plug", "led bulb", "electronics", "public transport", "EZ-Link card", "disposables", "food waste", "reusable container"],
        "food": ["apple core", "compost bin", "vegetable scraps", "reusable plate", "leftovers", "local produce", "empty plate", "reusable container", "homecooked meal", "reusable cutleries", "reusable straws"],
        "nature": ["green leaf", "flower", "tree trunk", "public park", "soil", "grass", "park", "greenery", "animal", "binocular", "local birds", "leaves", "local plants"],
        "exercise": ["walking shoes", "bicycle", "water bottle", "sneakers", "helmet", "outdoor", "people exercising", "people meditate"],
        "recycle": ["plastic bottle", "aluminum can", "newspaper", "recycling bin", "paper", "bottles", "jars", "glass"]
    }

    hints = {
        "exercise": "Focus on human-powered transport like walking or cycling instead of driving.",
        "nature": "Focus on observing or appreciating local biodiversity without harming it.",
        "reduce": "Focus on conservation of energy, water, or resources.",
        "reuse": "Focus on giving a second life to household items or daily necessities.",
        "food": "Focus on sustainable eating habits or composting.",
        "recycle": "Focus on the correct sorting and disposal of waste."
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

    clean_text = re.sub(r'<<.*?>>|\[.*?\]|\bSYS\b|\bTASK\b', '', raw_response)
    clean_text = clean_text.replace('"', '').split('|')[0]

    desc_only = clean_text.replace('"', '').split('|')[0].strip()
    desc_split = re.split(r'[\n.]', desc_only)
    desc_final = next((s.strip() for s in desc_split if len(s.strip()) > 5), "Perform an eco-friendly action.")
    final_description = desc_final.rstrip('.') + "."

    all_potential = category_data.get(category, ["nature"])

    matched_kws = []
    desc_lower = final_description.lower()
    for k in all_potential:
        # Split "led bulb" into ["led", "bulb"]
        keyword_parts = k.lower().split()
        
        # Check if any significant word (length > 4) is in the description
        if any(part[:4] in desc_lower for part in keyword_parts if len(part) > 2):
            matched_kws.append(k)

    if not matched_kws:
        # If no words match, pick 2 random ones + category as a safety net
        matched_kws = random.sample(all_potential, min(len(all_potential), 2))

    final_keywords = list(set(matched_kws + [category]))

    task_data = {
        "Description": final_description[:255],
        "Difficulty": difficulty,
        "CoinReward": reward,
        "RequiresEvidence": (difficulty == "Hard"),
        "Keyword": final_keywords,
        "NegativeKeyword": ["person", "blur", "text", "screenshot"],
        "Category": category
    }

    # DB Logic
    #conn = db_pool.connection()
    #try:
    #    with conn.cursor() as cursor:
    #        sql = """INSERT INTO Tasks 
    #                (Description, Difficulty, CoinReward, RequiresEvidence, Keyword, Category, NegativeKeyword, SourceType) 
    #                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
    #        cursor.execute(sql, (
    #            task_data['Description'], task_data['Difficulty'], task_data['CoinReward'],
    #            task_data['RequiresEvidence'], json.dumps(task_data['Keyword']),
    #            task_data['Category'], json.dumps(task_data['NegativeKeyword']), "ML"
    #        ))
    #        conn.commit()
    #        task_data["task_id"] = cursor.lastrowid
    #finally:
    #    conn.close()

    return task_data
   
@app.post("/predict")
async def predict(request: VertexRequest):

    # Vertex sends a list, so we take the first item
    data = request.instances[0]

    # 2. Convert Vertex TaskData to your internal TaskRequest model
    # This ensures aliases like 'totalScore' map correctly to 'total_coins'
    internal_request = TaskRequest(**data.model_dump())

    # 3. Call your actual logic
    result = await predict_bundle(internal_request)

    # 4. VERTEX REQUIREMENT: Return inside a "predictions" list
    # Your result dictionary already contains "user_tier" and "tasks"
    return {
        "predictions": [result]
    }

async def predict_bundle(req: TaskRequest):

    # Decision Tree for Background Analytics
    now = datetime.now(timezone.utc).replace(tzinfo=None)
    
    try:
        if req.last_activity_date:
            # 2. Parse and then STRIP timezone info immediately
            dt = datetime.fromisoformat(req.last_activity_date.replace("Z", "+00:00"))
            last_act_dt = dt.replace(tzinfo=None)
        else:
            last_act_dt = now
    except Exception as e:
        print(f"Date parsing error: {e}")
        last_act_dt = now
        
    days_since_act = (now - last_act_dt).days
    calc_window = min(max(1, req.total_coins // 1500), 30)
    total_tasks_pot = calc_window * 3
    
    pct_not_attempted = (req.not_attempted / max(1, total_tasks_pot)) * 100
    pct_failed = (req.failed_verifications / max(1, calc_window)) * 100

    features = [[req.total_coins, total_tasks_pot, days_since_act, pct_not_attempted, pct_failed]]
    pred = int(models["clf"].predict(features)[0])
    user_tier = tiers.get(pred, "Newbie")

    # Tier Guardrails
    if req.total_coins >= 1000 and pct_failed < 5:
        user_tier = "Pro"
    elif req.total_coins >= 250 and user_tier == "Newbie":
        user_tier = "Consistent"

    pref = req.preferred_category.lower()
    target_pref = pref if pref in ALLOWED_CATEGORIES else "nature"
    remaining_cats = [c for c in ALLOWED_CATEGORIES if c != target_pref]

    random.shuffle(remaining_cats)

    try:
        ai_task = await generate_single_task(
            difficulty="Normal",
            category=target_pref,
            history=req.historical_tasks,
            reward=500
        )
    except Exception as e:
        print(f"AI Model Error: {e}")
        return {
            "user_tier": user_tier,
            "tasks": []  # "empty" return
        }

    return {
        "user_tier": user_tier,
        "tasks": [ai_task]
    }

'''
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
            reward=plan["reward"]
        )
        daily_bundle.append(task)

    return {
        "user_tier": user_tier,
        "tasks": daily_bundle
    }
'''

### --- MISC --- ###

@app.get("/health")
def health():
    return {"status": "ok"}

if __name__ == "__main__":
    import uvicorn
    import os
    try:
        port = int(os.environ.get("PORT", 8080))
        #uvicorn.run(app, host="127.0.0.1", port=port, log_level="info")
        uvicorn.run(app, host="127.0.0.1", port=port)
        print(f"Starting server on port {port}")
    except KeyboardInterrupt:
        print("\nShutting down PockeTree gracefully... Bye!")
    uvicorn.run(app, host="127.0.0.1", port=port)




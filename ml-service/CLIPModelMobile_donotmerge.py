import anyio
import io, torch, time, base64, json
import open_clip, pymysql
import re

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from dbutils.pooled_db import PooledDB
from fastapi import FastAPI, Form, UploadFile, File, HTTPException
from gpt4all import GPT4All
from PIL import Image, ImageOps
from sklearn.tree import DecisionTreeClassifier
from pydantic import BaseModel, Field
from typing import List, Optional

# Allowed categories
ALLOWED_CATEGORIES = ["reuse", "reduce", "recycle", "food", "nature", "exercise"]

# Global placeholders
preprocess, tokenizer = None, None

device = "cuda" if torch.cuda.is_available() else "cpu"
tiers = {0: "Newbie", 1: "Consistent", 2: "Pro", 3: "Casual", 4: "Returning", 5: "Hibernating"}

# --- WARM UP ---

@asynccontextmanager
async def lifespan(app: FastAPI):

    # Warm up CLIP
    models["clip"].ensure_model()
    
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
    # 3: Casual: Any Level:	More than 10% of NotCompleted tasks, but low DaysSinceLastCompletion
    # 4: Returning: Any Level: DaysSinceLastCompletion is between 7 and 30.
    # 5: Hibernating: Any Level: DaysSinceLastCompletion is >31

    models["clf"].fit(X, y)
    
    # Load LLM
    models["llm"] = GPT4All("Meta-Llama-3.1-8B-Instruct-128k-Q4_0.gguf", device=device)
    
    print("All models loaded and Decision Tree trained!")
    yield
    db_pool.destroy()

app = FastAPI(lifespan=lifespan)

# --- DATABASE SETUP ---

DB_CONFIG = {
    "host": "127.0.0.1",
    "port": 3306,
    "user": "root",
    "password": "password", 
    "database": "PocketreeDb",
    "cursorclass": pymysql.cursors.DictCursor
}

db_pool = PooledDB(
    creator=pymysql, 
    mincached=2, 
    maxcached=5, 
    **DB_CONFIG
)

### --- USE CASE 1: IMAGE VERIFICATION (CLIP) --- ###

class CLIPService:
    def __init__(self):
        self.device = "cpu"
        self.model, self.preprocess, self.tokenizer = None, None, None
        self.text_cache = {}
        self.pos_threshold = 0.150
        self.margin = 0.05

    def ensure_model(self):
        if self.model is None:
            m, _, p = open_clip.create_model_and_transforms('MobileCLIP2-S0', pretrained='dfndr2b')
            self.model = m.to(self.device).eval()
            self.preprocess = p
            self.tokenizer = open_clip.get_tokenizer('MobileCLIP2-S0')
            print("MobileCLIP2 Loaded!")

    def _prepare_image(self, img_bytes: bytes):
        img = Image.open(io.BytesIO(img_bytes))
        img = ImageOps.exif_transpose(img).convert("RGB")
        img.thumbnail((224, 224))
        return self.preprocess(img).unsqueeze(0).to(self.device)

    def _get_features(self, phrases):
        tokens = self.tokenizer(phrases).to(self.device)
        with torch.inference_mode():
            feat = self.model.encode_text(tokens)
            return feat / feat.norm(dim=-1, keepdim=True)

    # --- MODEL 1: Simple Mobile CLIP ---
    def classify_simple(self, image_input, keyword: str):
        with torch.inference_mode():
            image_feat = self.model.encode_image(image_input)
            image_feat /= image_feat.norm(dim=-1, keepdim=True)

            text_feat = self._get_features([f"a photo of a {keyword}", "object"])
            
            raw_sim = float(image_feat @ text_feat[0].T)
            probs = (100.0 * image_feat @ text_feat.T).softmax(dim=-1).cpu().numpy()[0]

        verified = bool(probs.argmax() == 0 and probs[0] >= 0.55 and raw_sim > 0.15)
        return {"verified": verified, "score": float(probs[0]), "method": "simple"}

    # --- MODEL 2: With Pos/Neg Keywords ---
    def classify_advanced(self, image_input, pos_list: list, neg_list: list):
        # Create prompts
        pos_prompts = [f"a photo of {p}" for p in pos_list]
        neg_prompts = [f"a photo of {n}" for n in neg_list]

        with torch.inference_mode():
            image_feat = self.model.encode_image(image_input)
            image_feat /= image_feat.norm(dim=-1, keepdim=True)

            pos_feat = self._get_features(pos_prompts)
            pos_sims = (image_feat @ pos_feat.T).squeeze(0)
            best_pos = float(pos_sims.max().item())

            best_neg = -1.0
            if neg_prompts:
                neg_feat = self._get_features(neg_prompts)
                neg_sims = (image_feat @ neg_feat.T).squeeze(0)
                best_neg = float(neg_sims.max().item())

        verified = (best_pos > self.pos_threshold) and (best_pos > best_neg + self.margin)
        return {"verified": bool(verified), "score": best_pos, "method": "advanced"}

    # --- MODEL 3: Softmax Comparison (Normal CLIP) ---
    def classify_softmax(self, image_input, keyword: str):
        labels = [f"a {keyword}", "a blurry background", "a random object"]
        with torch.inference_mode():
            image_feat = self.model.encode_image(image_input)
            image_feat /= image_feat.norm(dim=-1, keepdim=True)
            
            text_feat = self._get_features(labels)
            
            logits = (image_feat @ text_feat.T) * 100
            probs = logits.softmax(dim=-1).cpu().numpy()[0]
            
        verified = bool(probs.argmax() == 0 and probs[0] >= 0.70)
        return {"verified": verified, "score": float(probs[0]), "method": "softmax"}

# Initialize Service
clip_service = CLIPService()

@app.post("/classify")
async def classify(
    keyword: str = Form(...), 
    negative_keyword: Optional[str] = Form(None), 
    file: UploadFile = File(...)):

    clip_service.ensure_model()
    
    # 1. Image & Keyword Prep
    content = await file.read()
    image_tensor = clip_service._prepare_image(content)

    def clean_keyword(k):
        if not k: return []
        cleaned = k.replace('@"', '"').strip()
        if cleaned.startswith("["):
            try: return json.loads(cleaned)
            except: return [cleaned]
        return [cleaned]

    pos_list = clean_keyword(keyword)
    neg_list = clean_keyword(negative_keyword)
    primary_keyword = pos_list[0] if pos_list else keyword

    # Run All 3 
    res1 = clip_service.classify_simple(image_tensor, primary_keyword)
    res2 = clip_service.classify_advanced(image_tensor, pos_list, neg_list)
    res3 = clip_service.classify_softmax(image_tensor, primary_keyword)

    all_results = [res1, res2, res3]
    avg_score = (res1["score"] * 0.25) + (res2["score"] * 0.50) + (res3["score"] * 0.25)

    # # Hybrid Consensus: 
    # # Verify if 2/3 agree AND the average confidence is decent.
    # verified_count = sum(1 for r in all_results if r["verified"])
    # final_verified = verified_count >= 2 and avg_score > 0.18

    # # Ensemble "The Best" 
    # best_score = max(r["score"] for r in all_results)
    
    # # Majority Voting: If 2 out of 3 say verified, we verify.
    # verified_count = sum(1 for r in all_results if r["verified"])
    # final_verified = verified_count >= 2

    # return {
    #     "verified": final_verified,
    #     "best_score": best_score,
    #     "details": {
    #         "simple": res1,
    #         "advanced": res2,
    #         "softmax": res3
    #     },
    #     "consensus": f"{verified_count}/3 models verified"
    # }

    # PESSIMISTIC LOGIC: 
    # Even if other models agree, if the Advanced model (Negative Keywords) fails, we reject the image.
    advanced_passed = res2["verified"]
    majority_agreed = (sum(1 for r in all_results if r["verified"]) >= 2)

    final_verified = advanced_passed and majority_agreed

    return {
        "verified": final_verified,
        "final_confidence": avg_score,
        "details": all_results
    }

### --- USE CASE 2: DYNAMIC TASK GENERATION (DECISION TREE TRAINING & LLM) --- ###

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

models = {
    "clip": CLIPService(),
    "clf": DecisionTreeClassifier(),
    "llm": None
}

async def get_llm_response(prompt: str):
    # This prevents the CPU-heavy LLM from blocking the FastAPI event loop
    return await anyio.to_thread.run_sync(models["llm"].generate, prompt)
    
@app.post("/predict")
async def generate_task(req: TaskRequest):

    # Estimate lifetime of the player.
    now = datetime.now(timezone.utc)

    # Estimate days played.
    try:
        if req.last_activity_date:
            last_act_dt = datetime.fromisoformat(req.last_activity_date.replace("Z", "+00:00"))
        else:
            last_act_dt = now
    except ValueError:
            last_act_dt = now
        
    days_since_act = (now - last_act_dt).days

    # Estimate days played.
    # Current taks reward: 250, 500, 750
    estimated_days_played = max(1, req.total_coins // (250+500+750))

    # Account Age: Either their coin-based age or the time since we last saw them.
    lifetime_days = max(estimated_days_played, days_since_act, 1)
    # Limit the denominator to the last 30 days of potential play
    # This ensures percentages remain meaningful even for 3-year-old accounts
    calculation_window = min(lifetime_days, 30)
    total_tasks_potential = calculation_window * 3
    total_hard_potential = calculation_window * 1

    pct_not_attempted = (req.not_attempted / total_tasks_potential) * 100
    pct_failed = (req.failed_verifications / max(1, total_hard_potential)) * 100

    # Ensure the preferred category is one of the 6 allowed
    target_category = req.preferred_category.lower()
    if target_category not in ALLOWED_CATEGORIES:
        target_category = "nature"

    # # Features: [TotalCoins, Total#ofTasks, DaysSinceLastActivity, %NotAttempted, %FailedVerification]
    # # Current taks reward: 250, 500, 750
    # # Prediction using pre-trained model & logic mapping
    # features = [[req.total_coins, total_tasks_potential, days_since_act, pct_not_attempted, pct_failed]]
    # pred = int(models["clf"].predict(features)[0])
    # user_tier = tiers.get(pred, "Newbie")
    
    # assigned_diff = "Hard" if user_tier == "Pro" else "Easy" if user_tier in ["Newbie", "Struggling"] else "Normal"
    # reward = {"Easy": 250, "Normal": 500, "Hard": 750}[assigned_diff]

    # history_str = ", ".join(req.historical_tasks) if req.historical_tasks else "None"
    
    # # LLM Generation
    # prompt = f"""
    #     [INST] <<SYS>>
    #     You are a JSON generator. Do not include introductory text.
    #     <</SYS>>
    #     Generate ONE eco-friendly task for:
    #     - Difficulty: {assigned_diff}
    #     - Category: {target_category} (STRICT: Must be one of {ALLOWED_CATEGORIES})
        
    #     RULES:
    #     - Description must be unique and NOT like these: [{history_str}]
    #     - Keyword must be a physical object for CLIP verification.
    #     - SourceType will be "ML".
        
    #     Return ONLY valid JSON:
    #     {{
    #     "Description": "...",
    #     "Keyword": ["..."],
    #     "NegativeKeyword": ["..."],
    #     "Category": "{target_category}"
    #     }}
    #     """
        
    # response = await get_llm_response(prompt)
    # print(f"DEBUG: LLM Raw Response: {response}")

    # try:
    #     match = re.search(r"(\{.*\})", response, re.DOTALL)
    #     if match:
    #         task_data = json.loads(match.group(1))
    #     else:
    #         raise ValueError("No JSON found")
        
    # # Force lowercase category
    #     task_data['Category'] = task_data.get('Category', target_cat).lower()
    #     if task_data['Category'] not in ALLOWED_CATEGORIES:
    #         task_data['Category'] = target_cat

    # except Exception as e:
    #     print(f"Extraction Error: {e} | Raw Response: {response}")
    #     raise HTTPException(status_code=500, detail="ML Parsing Error")
    
    # # try:
    # #     # Extract and Parse JSON
    # #     clean_json = response[response.find('{'):response.rfind('}')+1]
    # #     task_data = json.loads(clean_json)
        
    # #     # Double-check category is valid before DB injection
    # #     if task_data.get('Category', '').lower() not in ALLOWED_CATEGORIES:
    # #         task_data['Category'] = target_category
            
    # # except Exception as e:
    # #     print(f"LLM Parse Error: {e}")
    # #     raise HTTPException(status_code=500, detail="LLM Output Error")

    # # Database Injection
    # conn = db_pool.connection()
    # try:
    #     with conn.cursor() as cursor:
    #         sql = """INSERT INTO Tasks 
    #                  (Description, Difficulty, CoinReward, RequiresEvidence, Keyword, Category, NegativeKeyword, SourceType) 
    #                  VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
    #         cursor.execute(sql, (
    #             task_data['Description'], 
    #             assigned_diff, 
    #             reward, 
    #             (assigned_diff == "Hard"),
    #             json.dumps(task_data.get('Keyword', [])), 
    #             task_data['Category'],
    #             json.dumps(task_data.get('NegativeKeyword', [])), 
    #             "ML"
    #         ))
    #         conn.commit()
    #         task_id = cursor.lastrowid
    # finally:
    #     conn.close()

    # return {"task_id": task_id, "tier": user_tier, "task": task_data}

    features = [[req.total_coins, total_tasks_potential, days_since_act, pct_not_attempted, pct_failed]]
    pred = int(models["clf"].predict(features)[0])
    user_tier = tiers.get(pred, "Newbie")

    # Guardrail by forcing "Pro" if they have high coins and good stats, regardless of ML output
    if req.total_coins >= 1000 and pct_failed < 5:
        user_tier = "Pro"
    elif req.total_coins >= 250 and user_tier == "Newbie":
        user_tier = "Consistent"
    
    assigned_diff = "Hard" if user_tier == "Pro" else "Easy" if user_tier in ["Newbie", "Struggling"] else "Normal"
    reward = {"Easy": 250, "Normal": 500, "Hard": 750}[assigned_diff]
    
    # Ensure category is safe
    target_cat = req.preferred_category.lower() if req.preferred_category.lower() in ALLOWED_CATEGORIES else "nature"

    # LLM Only Generates the Description
    history_str = ", ".join(req.historical_tasks)

    # Adjust instruction based on difficulty
    if assigned_diff == "Hard":
        diff_instruction = "Give a complex, multi-step task involving specific types of items."
    elif assigned_diff == "Normal":
        diff_instruction = "Give a standard, clear eco-friendly activity."
    else:
        diff_instruction = "Give a very simple, beginner-friendly eco-action."

    # Category Hints for variety
    category_hints = {
        "reuse": "Focus on repurposing glass jars, containers, or old clothes.",
        "reduce": "Focus on saving energy, water, or reducing single-use plastics.",
        "food": "Focus on composting, plant-based meals, or reducing food waste.",
        "nature": "Focus on plants, birds, or cleaning up local green spaces.",
        "exercise": "Focus on walking, biking, or outdoor physical activities.",
        "recycle": "Focus on sorting materials like paper, metal, and plastic."
    }

    hint = category_hints.get(target_cat, "")
    
    prompt = f"""
    {diff_instruction} {hint}
    Write a short {assigned_diff} eco-task, no more than 255 characters, for the category '{target_cat}'. 
    RULES: Task must be SAFE (no dangerous plants or hazardous waste).
    Format: Task description | 3-4 specific visual objects to photograph (comma separated).
    Avoid: {history_str}
    Example: Pick up a discarded plastic bottle | plastic bottle
    Task:"""

    raw_response = await get_llm_response(prompt)

    # # Logic-Based Extraction Old
    # if "|" in raw_response:
    #     parts = raw_response.split("|")
    #     description = parts[0].strip().replace('"', '')
        
    #     # CLEANING: Take only the first line of the keyword to prevent hallucinations
    #     keyword_raw = parts[1].strip().split('\n')[0].lower()
    #     keyword_main = keyword_raw if keyword_raw else target_cat
    # else:
    #     description = raw_response.split('\n')[0].strip().replace('"', '')
    #     keyword_main = target_cat

    # Logic-Based Extraction
    if "|" in raw_response:
        parts = raw_response.split("|")
        description = parts[0].strip().replace('"', '')
        
        # Get keywords from LLM, split by comma, and flatten
        raw_keywords = parts[1].strip().split('\n')[0].split(',')
        keywords_list = [k.strip().lower() for k in raw_keywords if k.strip()]
        
        # Deduplicate and add category fallback using a set
        unique_keywords = list(set(keywords_list + [target_cat.lower()]))
    else:
        description = raw_response.split('\n')[0].strip().replace('"', '')
        unique_keywords = [target_cat.lower()]

    # Build the JSON object 
    task_data = {
        "Description": description[:255], 
        "Keyword": unique_keywords,
        "NegativeKeyword": ["person", "blur", "text", "screenshot"],
        "Category": target_cat
    }

    # Create the JSON string and handle the DB limit (255 chars)
    keyword_json = json.dumps(task_data['Keyword'])
    
    if len(keyword_json) > 255:
        # PESSIMISTIC FALLBACK: If the list is too long for the DB column, keep the category and only the first keyword
        short_list = list(set([unique_keywords[0], target_cat.lower()]))
        keyword_json = json.dumps(short_list)

    # Database Persistence
    conn = db_pool.connection()
    try:
        with conn.cursor() as cursor:
            sql = """INSERT INTO Tasks 
                    (Description, Difficulty, CoinReward, RequiresEvidence, Keyword, Category, NegativeKeyword, SourceType) 
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
            cursor.execute(sql, (
                task_data['Description'], 
                assigned_diff, 
                reward, 
                (assigned_diff == "Hard"),
                keyword_json, 
                task_data['Category'],
                json.dumps(task_data['NegativeKeyword']), 
                "ML"
            ))
            conn.commit()
            task_id = cursor.lastrowid
    finally:
        conn.close()

    return {"task_id": task_id, "tier": user_tier, "task": task_data}

### --- MISC --- ###

@app.get("/health")
def health():
    return {"status": "ok"}

if __name__ == "__main__":
    import uvicorn
    import os
    port = int(os.environ.get("PORT", 8080))
    print(f"Starting server on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)
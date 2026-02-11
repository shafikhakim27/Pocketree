import anyio
import httpx
import io, torch, time, base64, json
import numpy as np
import open_clip, pymysql
import pickle

from contextlib import asynccontextmanager
from datetime import datetime, timezone
from dbutils.pooled_db import PooledDB
from fastapi import FastAPI, Form, UploadFile, File, HTTPException
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
    "clip": None, 
    "clf": DecisionTreeClassifier(), 
    "llm": None, 
    "sustain_bot": None}

tiers = {0: "Newbie", 1: "Consistent", 2: "Pro", 3: "Casual", 4: "Returning", 5: "Hibernating"}

# --- Prepare for Use Case 3 ---

class EmbeddingBrain:
    def __init__(self):

        self.encoder = SentenceTransformer('all-MiniLM-L6-v2', device=device)
        self.chunks = []
        self.vectors = None

    async def cloud_warmup(self, urls):
        if self.vectors is not None:
            return

        all_docs = []
        headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
        }

        for url in urls:
            try:
                # Pass the headers here
                loader = PyPDFLoader(url, headers=headers)
                docs = await anyio.to_thread.run_sync(loader.load)
                all_docs.extend(docs)
            except Exception as e:
                print(f"Skipping {url} due to error: {e}")

        splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)

        self.chunks = [doc.page_content for doc in splitter.split_documents(all_docs)]
        
        if self.chunks:
            self.vectors = await anyio.to_thread.run_sync(
                lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
            )

    def search(self, query, k=2):
        if self.vectors is None: return ""
        query_vec = self.encoder.encode([query], normalize_embeddings=True)
        scores = np.dot(self.vectors, query_vec.T).flatten()
        top_indices = np.argsort(scores)[-k:][::-1]
        return "\n".join([self.chunks[i] for i in top_indices])
    
class PockeTreeBot:
    def __init__(self, brain):
        self.brain = brain
        pipeline_device = 0 if device == "cuda" else -1
        self.generator = pipeline(
            "text-generation", 
            model="Qwen/Qwen2.5-1.5B-Instruct", 
            device=pipeline_device,
            torch_dtype="auto" 
        )

    def extract_and_greet(self, text, user_id):
        text_lower = text.lower()
        profile = user_profiles.get(user_id, {"name": None, "history": []})
        
        # Simple extraction logic
        if "my name is" in text_lower:
            name = text.split("is")[-1].strip().strip(".!?")
            profile["name"] = name
        elif "i am " in text_lower and len(text_lower.split()) < 5:
            name = text.split("am")[-1].strip().strip(".!?")
            profile["name"] = name
            
        user_profiles[user_id] = profile
        return profile["name"]
    
    def handle_small_talk(self, text, user_id):
        
        name = self.extract_and_greet(text, user_id)
        text_lower = text.lower().strip()
        words = text_lower.split()

        if len(words) < 4 and any(greet in text_lower for greet in ["hi", "hello", "morning", "afternoon", "evening", "yo", "what's up"]):
            name = self.extract_and_greet(text, user_id)
            greet_name = f", {name}" if name else ""
            return f"Hello{greet_name}! PockeTree here. Ready to go green today?"
        
        if "who am i" in text_lower or "my name" in text_lower:
            if name: return f"You are {name} lah, we just talked what!"
            return "I don't know your name yet.... What should I call you?"
        
        if "how are you" in text_lower:
            return "I'm steady lah! Just busy thinking how to save the planet. You?"
        
        if any(sad in text_lower for sad in ["lonely", "sad", "bored"]):
            return "I'm sorry to hear that, let's go walk at the park and see some greenery. It will make you feel better!"
            
        if "funny" in text_lower or "joke" in text_lower:
            return "I have only one: why did the recycling bin break up with the trash can? Because he found out she was 'wasted'! Hahaha!"

        return None

    def _parse_to_two_sentences(self, text: str):
        import re
        # Cleanup labels and artifacts
        text = re.sub(r'(Sentence \d:|Assistant:|Note:|\*\*)', '', text, flags=re.IGNORECASE).strip()
        sentences = re.split(r'(?<=[.!?])\s+', text)
        sentences = [s for s in sentences if len(s) > 5]
        
        if not sentences:
            return "I'm not sure about that one. Check the NEA website for more info lah!" 

        clean_response = " ".join(sentences[:2])
        if not clean_response.endswith(('.', '!', '?')):
            clean_response = clean_response.rsplit(' ', 1)[0] + "."
        return clean_response
    
    def get_response(self, user_text: str, user_id: str):
        # 1. Handle name and small talk
        small_talk = self.handle_small_talk(user_text, user_id)
        if small_talk: return small_talk

        # 2. Get Profile & History
        profile = user_profiles.get(user_id, {"name": "Friend", "history": []})
        name = profile.get("name") or "Friend"
        hist_str = "\n".join([f"User: {m['u']}\nBot: {m['b']}" for m in profile["history"][-2:]])

        # 3. Build Prompt
        facts = [v for k, v in SG_EXPERT_FACTS.items() if k in user_text.lower()]
        pdf_context = self.brain.search(user_text, k=1)
        
        system_instr = (
            f"You are PockeTree, a wise and friendly Singaporean eco-mentor talking to {name}. "
            "Talk like a Singaporean local. You may use Singlish. "
            "Give the answer in EXACTLY two short sentences. "
            "Sentence 1: The facts. Sentence 2: Action."
            "Refer to the Chat History if the user asks follow-up questions."
        )
        
        prompt = f"<|im_start|>system\n{system_instr}\nHistory:\n{hist_str}\nContext:\n{pdf_context}\n{facts}<|im_end|>\n"
        prompt += f"<|im_start|>user\n{user_text}<|im_end|>\n<|im_start|>assistant\n"

        output = self.generator(prompt, max_new_tokens=60, do_sample=True, temperature=0.7)
        reply = output[0]['generated_text'].split("<|im_start|>assistant\n")[-1].strip()
        
        # 4. Save to history
        profile["history"].append({"u": user_text, "b": reply})
        user_profiles[user_id] = profile
        
        return self._parse_to_two_sentences(reply)
    
    # def get_response(self, user_text: str, history: list = []): # Added history param
    #     # 1. Check small talk
    #     chat_reply = self.handle_small_talk(user_text)
    #     if chat_reply:
    #         return chat_reply

    #     # 2. Format the history for the LLM
    #     history_context = ""
    #     for turn in history:
    #         history_context += f"User: {turn['u']}\nBot: {turn['b']}\n"

    #     # 3. RAG Search
    #     facts = [v for k, v in SG_EXPERT_FACTS.items() if k in user_text.lower()]
    #     pdf_context = self.brain.search(user_text, k=2) 
    #     combined_context = "\n".join(facts) + "\n" + pdf_context
        
    #     system_instr = (
    #         "You are PockeTree, a wise and friendly Singaporean eco-mentor. "
    #         "Talk like a Singaporean local. You may use Singlish. "
    #         "Give the answer in EXACTLY two short sentences. "
    #         "Sentence 1: The facts. Sentence 2: Action."
    #         "Refer to the Chat History if the user asks follow-up questions."
    #     )
        
    #     # Inject History into the prompt
    #     prompt = f"<|im_start|>system\n{system_instr}\nCHAT HISTORY:\n{history_context}\nCONTEXT:\n{combined_context}<|im_end|>\n"
    #     prompt += f"<|im_start|>user\n{user_text}<|im_end|>\n<|im_start|>assistant\n"

    #     output = self.generator(prompt, max_new_tokens=80, do_sample=True, temperature=0.7)
    #     full_reply = output[0]['generated_text'].split("<|im_start|>assistant\n")[-1].strip()

    #     return self._parse_to_two_sentences(full_reply)

# --- WARM UP ---

@asynccontextmanager
async def lifespan(app: FastAPI):

    global models

    models["clip"] = CLIPService()
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
    # 3: Casual: Any Level: More than 10% of NotCompleted tasks, but low DaysSinceLastCompletion
    # 4: Returning: Any Level: DaysSinceLastCompletion is between 7 and 30.
    # 5: Hibernating: Any Level: DaysSinceLastCompletion is >31

    models["clf"].fit(X, y)
    
    # Load LLM
    models["llm"] = GPT4All("Meta-Llama-3.1-8B-Instruct-128k-Q4_0.gguf", device='cpu')

    brainBot = EmbeddingBrain()
    await brainBot.cloud_warmup(SUSTAINABILITY_REPORTS)
    
    models["sustain_bot"] = PockeTreeBot(brainBot)
    
    print("All models loaded & trained!")
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
        self.device = device
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
        img = ImageOps.exif_transpose(img).convert("RGB") # pyright: ignore[reportOptionalMemberAccess]
        img.thumbnail((224, 224))
        return self.preprocess(img).unsqueeze(0).to(self.device) # pyright: ignore[reportAttributeAccessIssue, reportCallIssue, reportOptionalCall]

    def _get_features(self, phrases):
        tokens = self.tokenizer(phrases).to(self.device) # pyright: ignore[reportOptionalCall]
        with torch.inference_mode():
            feat = self.model.encode_text(tokens) # pyright: ignore[reportOptionalMemberAccess, reportCallIssue]
            return feat / feat.norm(dim=-1, keepdim=True)

    # --- MODEL 1: Simple Mobile CLIP ---
    def classify_simple(self, image_input, keyword: str):
        with torch.inference_mode():
            image_feat = self.model.encode_image(image_input) # pyright: ignore[reportOptionalMemberAccess, reportCallIssue]
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
            image_feat = self.model.encode_image(image_input) # pyright: ignore[reportOptionalMemberAccess, reportCallIssue]
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
            image_feat = self.model.encode_image(image_input) # pyright: ignore[reportOptionalMemberAccess, reportCallIssue]
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

    import random
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
    conn = db_pool.connection()
    try:
        with conn.cursor() as cursor:
            sql = """INSERT INTO Tasks 
                    (Description, Difficulty, CoinReward, RequiresEvidence, Keyword, Category, NegativeKeyword, SourceType) 
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
            cursor.execute(sql, (
                task_data['Description'], task_data['Difficulty'], task_data['CoinReward'],
                task_data['RequiresEvidence'], json.dumps(task_data['Keyword']),
                task_data['Category'], json.dumps(task_data['NegativeKeyword']), "ML"
            ))
            conn.commit()
            task_data["task_id"] = cursor.lastrowid
    finally:
        conn.close()

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
    now = datetime.now(timezone.utc)
    try:
        last_act_dt = datetime.fromisoformat(req.last_activity_date.replace("Z", "+00:00")) if req.last_activity_date else now
    except:
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

    import random
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
            reward=plan["reward"]
        )
        daily_bundle.append(task)

    return {
        "user_tier": user_tier,
        "tasks": daily_bundle
    }

### --- USE CASE 3: POCKETREE BOT --- ###

SUSTAINABILITY_REPORTS = [

    "https://isomer-user-content.by.gov.sg/50/3db854a5-473a-4d18-ae26-6991464a17a1/ssbcombined-cover-text.pdf",
    "https://isomer-user-content.by.gov.sg/23/21ea81ce-ac0a-4351-bac9-41f65c426a72/zero-waste-sg-report-transparent-bin-pilot.pdf",
    "https://isomer-user-content.by.gov.sg/23/2e32645e-d2fd-4f65-95d6-2473c36b5dbf/climate-action-plan.pdf",
    "https://www.greenplan.gov.sg/files/SGP2023_overview.pdf",

    "https://unstats.un.org/sdgs/report/2025/The-Sustainable-Development-Goals-Report-2025.pdf"

]

SG_EXPERT_FACTS = {
    "recycling": "In Singapore, we use 'Commingled Recycling'. Use the blue bins for paper, plastic, glass, and metal. Items MUST be clean and dry!",
    "vouchers": "The Climate Friendly Households Programme provides $300 in vouchers for HDB households for 10 types of energy/water-saving appliances.",
    "food": "Food waste is one of SG's largest waste streams. Use the 'UglyFood' app or donate excess to The Food Bank Singapore.",
    "aircon": "Setting your aircon to 25°C instead of 20°C can save you up to $250 a year in Singapore!",
    "parks": "Target 2026: Develop 130ha of new parks. By 2030, every home will be within a 10-min walk of a park!",
    "landfill": "Target 2026: Reduce per capita waste to landfill by 20% to extend Semakau Landfill's life.",
    "solar": "Singapore is hitting 1.5GWp of solar deployment this year (2025/26), meeting 2 percent of our energy needs.",
    "ev": "By the end of 2025, all HDB carparks are officially EV-ready with charging points!",
    "vouchers": "HDB and private property households can claim a total of $400 in Climate Vouchers via go.gov.sg/cv-claim using Singpass lah!"
}

class ChatReq(BaseModel):
    user_id: str = "default_user"
    message: str

@app.post("/chat")
async def chat(req: ChatReq):
    # Ensure user_id exists in request
    user_id = getattr(req, 'user_id', 'anon_user')
    reply = await anyio.to_thread.run_sync(models["sustain_bot"].get_response, req.message, user_id)
    return {"bot": "PockeTree", "response": reply}

### --- MISC --- ###

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
    uvicorn.run(app, host="0.0.0.0", port=port)


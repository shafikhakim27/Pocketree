import anyio
import argparse
import asyncio
import httpx
import io
import numpy as np
import os
import pickle
import re
import torch
from contextlib import asynccontextmanager
from datetime import datetime
from fastapi import FastAPI, HTTPException
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.document_loaders import PyMuPDFLoader
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from transformers import pipeline

# -----------------------------
# ENV / GLOBALS
# -----------------------------
DISABLE_WARMUP = os.getenv("DISABLE_WARMUP", "0") == "1"
CACHE_PATH = os.getenv("BRAIN_CACHE_PATH", "/tmp/brain_cache.pkl")
MAX_CONCURRENT_CHATS = int(os.getenv("MAX_CONCURRENT_CHATS", "1"))
DOWNLOAD_TIMEOUT_S = float(os.getenv("PDF_DOWNLOAD_TIMEOUT_S", "60.0"))

chat_semaphore = anyio.Semaphore(MAX_CONCURRENT_CHATS)

# Best available engine (Cloud Run CPU usually; GPU only if you really have one)
if torch.cuda.is_available():
    DEVICE = "cuda"
elif getattr(torch.backends, "mps", None) and torch.backends.mps.is_available():
    DEVICE = "mps"
else:
    DEVICE = "cpu"

# Global placeholders
chat_histories = {}
user_profiles = {}
models = {"sustain_bot": None}

# Allowed categories (kept)
ALLOWED_CATEGORIES = ["reuse", "reduce", "recycle", "food", "nature", "exercise"]

SG_EXPERT_FACTS = {
    "recycling": "In Singapore, we use 'Commingled Recycling'. Use the blue bins for paper, plastic, glass, and metal. Items MUST be clean and dry!",
    "food": "Food waste is one of SG's largest waste streams. Use the 'UglyFood' app or donate excess to The Food Bank Singapore.",
    "aircon": "Setting your aircon to 25°C instead of 20°C can save you up to $250 a year in Singapore!",
    "parks": "Target 2026: Develop 130ha of new parks. By 2030, every home will be within a 10-min walk of a park!",
    "landfill": "Target 2026: Reduce per capita waste to landfill by 20% to extend Semakau Landfill's life.",
    "solar": "Singapore is hitting 1.5GWp of solar deployment this year (2025/26), meeting 2 percent of our energy needs.",
    "ev": "By the end of 2025, all HDB carparks are officially EV-ready with charging points!",
    "vouchers": "HDB and private property households can claim a total of $400 in Climate Vouchers via go.gov.sg/cv-claim using Singpass!"
}

SUSTAINABILITY_REPORTS = [
    "https://isomer-user-content.by.gov.sg/50/3db854a5-473a-4d18-ae26-6991464a17a1/ssbcombined-cover-text.pdf",
    "https://isomer-user-content.by.gov.sg/23/21ea81ce-ac0a-4351-bac9-41f65c426a72/zero-waste-sg-report-transparent-bin-pilot.pdf",
    "https://isomer-user-content.by.gov.sg/23/2e32645e-d2fd-4f65-95d6-2473c36b5dbf/climate-action-plan.pdf",
    "https://www.greenplan.gov.sg/files/SGP2023_overview.pdf",
    "https://unstats.un.org/sdgs/report/2025/The-Sustainable-Development-Goals-Report-2025.pdf"
]

class ChatReq(BaseModel):
    user_id: str = "default_user"
    message: str

class EmbeddingBrain:
    def __init__(self):
        embed_device = os.getenv("EMBED_DEVICE", DEVICE)
        if embed_device not in ("cpu", "cuda", "mps"):
            embed_device = "cpu"

        self.encoder = SentenceTransformer("sentence-transformers/all-MiniLM-L6-v2", device=embed_device)
        self.ready = False
        self.chunks = []
        self.vectors = None
        self._lock = asyncio.Lock()

    def _try_load_cache(self, path: str) -> bool:
        if not os.path.exists(path):
            return False
        try:
            print(f"Loading brain cache from {path} ...")
            with open(path, "rb") as f:
                data = pickle.load(f)
            self.chunks = data["chunks"]
            self.vectors = data["vectors"]
            self.ready = True
            print(f"Brain restored. Version: {data.get('version', 'unknown')}")
            return True
        except Exception as e:
            print(f"Cache load failed from {path}: {repr(e)}")
            return False

    async def smart_load(self, urls):
        if self._try_load_cache(CACHE_PATH):
            return

        local_candidate = os.getenv("LOCAL_BRAIN_CACHE", "brain_cache.pkl")
        if self._try_load_cache(local_candidate):
            try:
                with open(CACHE_PATH, "wb") as f:
                    pickle.dump({"version": "copied-from-local", "chunks": self.chunks, "vectors": self.vectors}, f)
            except Exception as e:
                print("Could not copy local cache to /tmp:", repr(e))
            return

        print("No valid cache found. Starting fallback PDF download...")
        await self.cloud_warmup(urls)

    async def cloud_warmup(self, urls):
        async with self._lock:
            if self.ready:
                return

            fact_chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()]
            all_docs = []

            async with httpx.AsyncClient(timeout=DOWNLOAD_TIMEOUT_S, follow_redirects=True) as client:
                for url in urls:
                    try:
                        r = await client.get(url, headers={"User-Agent": "Mozilla/5.0"})
                        r.raise_for_status()

                        with io.BytesIO(r.content) as f:
                            import tempfile
                            with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
                                tmp.write(f.read())
                                tmp_path = tmp.name

                        loader = PyMuPDFLoader(tmp_path)
                        docs = await anyio.to_thread.run_sync(loader.load)
                        all_docs.extend(docs)

                        try:
                            os.remove(tmp_path)
                        except Exception:
                            pass

                    except Exception as e:
                        print(f"Error loading {url}: {e}")

            if all_docs:
                splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
                pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
                self.chunks = fact_chunks + pdf_chunks

                vectors_raw = await anyio.to_thread.run_sync(
                    lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
                )
                self.vectors = vectors_raw.astype("float16")

                # Save to /tmp (Cloud Run writable)
                try:
                    with open(CACHE_PATH, "wb") as f:
                        pickle.dump(
                            {"version": f"runtime-{datetime.now().strftime('%Y%m%d-%H%M%S')}",
                            "chunks": self.chunks,
                            "vectors": self.vectors},
                            f
                        )
                    print(f"Saved runtime cache to {CACHE_PATH}")
                except Exception as e:
                    print("Failed to write cache to /tmp:", repr(e))

            self.ready = True
            print("Cloud warmup complete.")

    def bake(self, custom_version=None):
        version_tag = custom_version or f"v-{datetime.now().strftime('%Y%m%d')}"
        print(f"--- Starting SG_ --bake ({version_tag}) ---")

        pdf_folder = "./data"
        if not os.path.exists(pdf_folder):
            os.makedirs(pdf_folder)
            print(f"Created folder '{pdf_folder}'. Please put your PDFs in it and run --bake again.")
            return

        all_files = [f for f in os.listdir(pdf_folder) if f.endswith(".pdf")]
        sg_files = sorted([f for f in all_files if f.startswith("SG_")])
        other_files = sorted([f for f in all_files if not f.startswith("SG_")])
        priority_files = sg_files + other_files

        all_docs = []
        for filename in priority_files:
            prefix = "CORE SG DATA" if filename.startswith("SG_") else "GLOBAL DATA"
            print(f"[{prefix}] Processing: {filename}...")
            loader = PyMuPDFLoader(os.path.join(pdf_folder, filename))
            all_docs.extend(loader.load())

        splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
        self.chunks =   [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()] + \
                        [d.page_content for d in splitter.split_documents(all_docs)]

        print(f"Vectorizing {len(self.chunks)} chunks...")
        vectors_raw = self.encoder.encode(self.chunks, normalize_embeddings=True)

        out_path = "brain_cache.pkl"
        with open(out_path, "wb") as f:
            pickle.dump(
                {
                    "version": version_tag,
                    "chunks": self.chunks,
                    "vectors": vectors_raw.astype("float16"),
                },
                f,
            )
        print(f"Done! {out_path} created with {len(self.chunks)} chunks.")

    def search(self, query, k=2):
        if not self.ready or self.vectors is None or len(self.vectors) == 0:
            return ""

        query_vec = self.encoder.encode([query], normalize_embeddings=True)
        scores = np.dot(self.vectors, query_vec.T).flatten()

        k = min(k, len(self.chunks))
        top_indices = np.argsort(scores)[-k:][::-1]
        return "\n".join([self.chunks[i] for i in top_indices])

class PockeTreeBot:
    def __init__(self, brain: EmbeddingBrain):
        self.brain = brain
        self.model_id = "Qwen/Qwen2.5-1.5B-Instruct"

        # Cloud Run fix: lazy-load the generator so startup is fast
        self.generator = None

    def _ensure_generator(self):
        if self.generator is not None:
            return

        pipeline_device = 0 if DEVICE == "cuda" else -1
        # torch_dtype="auto" is safer across CPU/GPU
        self.generator = pipeline(
            "text-generation",
            model=self.model_id,
            device=pipeline_device,
            torch_dtype="auto",
        )

    def extract_name(self, text, user_id):
        text_lower = text.lower().strip()
        profile = user_profiles.get(user_id, {"name": None, "history": []})

        patterns = ["my name is", "i am ", "call me ", "im "]
        for p in patterns:
            if p in text_lower:
                name = text_lower.split(p)[-1].strip().split()[0].strip(".!?")
                profile["name"] = name.capitalize()
                user_profiles[user_id] = profile
                return profile["name"]

        return profile.get("name")

    def extract_and_greet(self, text, user_id):
        text_lower = text.lower()
        profile = user_profiles.get(user_id, {"name": None, "history": []})

        if "my name is" in text_lower:
            name = text.split("is")[-1].strip().strip(".!?")
            profile["name"] = name
        elif "i am " in text_lower and len(text_lower.split()) < 5:
            name = text.split("am")[-1].strip().strip(".!?")
            profile["name"] = name
            
        user_profiles[user_id] = profile
        return profile["name"]

    def handle_small_talk(self, text, user_id):
        noise_words = ["zzz", "tsk", "hmmm", "uhm", "err", "haiz", "tch"]
        text_clean = text.lower().strip().replace("?", "").replace("!", "").replace(".", "")
        for word in noise_words:
            text_clean = text_clean.replace(word, "").strip()

        profile = user_profiles.get(user_id, {"name": None, "history": []})
        name = self.extract_name(text_clean, user_id)

        text_lower = text_clean
        words = text_clean.split()
        last_bot_msg = profile["history"][-1]["b"] if profile["history"] else ""

        support_keywords = ["support", "contact", "help me", "talk to human", "email",
                            "issue", "problem", "bug", "error", "not working"]
        if any(k in text_clean for k in support_keywords):
            if any(bug in text_clean for bug in ["bug", "error", "issue", "not working", "problem"]):
                return "I'm sorry to hear that. You can reach our support team at https://pocketree-api.azurewebsites.net/"
            return "Need a hand? You can reach our support team at https://pocketree-api.azurewebsites.net/"

        if "what is your name" in last_bot_msg.lower():
            if not profile.get("name") and words:
                new_name = words[-1].capitalize()
                profile["name"] = new_name
                user_profiles[user_id] = profile
                return f"So good to know you, {new_name}!"

        if any(word in text_lower for word in ["stats", "statistics", "coins", "level", "progress", "plant health"]):
            return ("I'm sorry, I don't have access to that information yet right now. "
                    "But if you are referring to your personal stats at PocketreeBot, feel free to check your app or visit https://pocketree-api.azurewebsites.net/")

        bot_compliments = ["smart", "genius", "amazing", "helpful", "good bot"]
        if any(comp in text_lower for comp in bot_compliments):
            if any(you in text_lower for you in ["you are", "you're", "youre", "ur "]):
                return f"That is very kind of you, {name or 'Friend'}!"

        identity_queries = ["who are you", "what are you", "who you", "your name", "are you alive"]
        if any(q in text_clean for q in identity_queries):
            if "alive" in text_clean:
                return "I am an AI eco-friend. I am very much 'alive' with passion for the planet!"
            return "I am PockeTree, your friendly eco-buddy. I'm here to help you live more sustainably!"

        if "how are you" in text_lower:
            return "I'm good! Just busy thinking how to save the planet. You?"

        if "who am i" in text_lower or "my name" in text_lower:
            if name:
                return f"You are {name}. So good to know you!"
            return "I don't know your name yet.... What should I call you?"

        meeting_phrases = ["nice to meet you", "good to know you", "pleasure"]
        if any(phrase in text_clean for phrase in meeting_phrases):
            return "The pleasure is all mine! Ready to learn more about Singapore's green efforts?"

        user_greetings = ["hi", "hello", "yo", "what's up", "wassup", "whassup", "whatsup",
                            "wzzup", "wazzup", "good day", "good morning", "good afternoon",
                            "good evening", "heya", "mornin"]
        if words and words[0] in user_greetings and len(words) < 4:
            if not name:
                return "Hello! PockeTree here. Before we begin, what is your name?"
            return f"Hello {name}! PockeTree here. Ready to go green today?"

        if any(q in text_lower for q in ["who are you", "who you", "your name"]):
            return "Hello! This is PocketreeBot speaking. How are you today?"

        if words and words[0] in ["thanks", "thank", "thx", "thank you", "tx"]:
            return "You're most welcome! Do you have any other questions about sustainability?"
    
        if words and words[0] in ["ok", "oki", "okays", "yea", "yes"] and len(words) > 2:
            return None
        
        casual_fillers = ["same old", "not much", "im good", "am good", "me too", "just chilling", "cool", "ok", "sure", "nice"]
        
        if any(f == text_clean for f in casual_fillers):
            return "Great! Do you have any specific questions about green efforts in Singapore today?"

        if len(words) > 4:
            return None

        if any(sad in text_lower for sad in ["lonely", "sad", "bored"]):
            return "I'm sorry to hear that, let's go walk at the park and see some greenery. It will make you feel better!"

        if "funny" in text_lower or "joke" in text_lower:
            return "Sorry, I have only one: why did the recycling bin break up with the trash can? Because he found out she was 'wasted'! Hahaha!"

        if "do you" in text_lower or "is it" in text_lower:
            return None

        return None

    def _parse_to_two_sentences(self, text: str):
        text = re.sub(r"(Sentence \d:|Assistant:|Note:|\*\*)", "", text, flags=re.IGNORECASE).strip()
        sentences = re.split(r"(?<=[.!?])\s+", text)
        sentences = [s for s in sentences if len(s) > 5]

        if not sentences:
            return "I'm not sure. Why don't we check https://www.mse.gov.sg/resources/sgp-2030/ together?"

        clean_response = " ".join(sentences[:2])
        if not clean_response.endswith((".", "!", "?")):
            clean_response = clean_response.rsplit(" ", 1)[0] + "."
        return clean_response

    def _needs_history(self, text: str) -> bool:
        t = text.lower()
        return any(
            keyword in t
            for keyword in ["that", "those", "it", "again", "earlier", "previous", "you said", "follow up"]
        )

    def get_response(self, user_text: str, user_id: str):
        # 1) small talk
        reply = self.handle_small_talk(user_text, user_id)
        if reply:
            self._save_history(user_text, reply, user_id)
            return reply

        # 2) ensure model is loaded (lazy)
        self._ensure_generator()

        # 3) build context + prompt
        with torch.inference_mode():
            profile = user_profiles.get(user_id, {"name": "Friend", "history": []})
            name = profile.get("name") or "Friend"
            context = self.brain.search(user_text, k=2)
            if not context or len(context.strip()) < 10:
                context = "Have you ever learned about the Singapore Green Plan 2030?"
            hist_str = ""
            if self._needs_history(user_text):
                hist_str = "\n".join([f"User: {m['u']}\nBot: {m['b']}" for m in profile["history"][-2:]])

            system_instr = (
                f"You are PockeTree, a wise and friendly Singaporean eco-mentor talking to {name}. "
                "Give the answer in EXACTLY two short sentences. "
                "Sentence 1: The facts. Sentence 2: Action. "
                "Refer to the Chat History if the user asks follow-up questions. "
                "If the user asks about their personal app data (like coins, stats, or plant health), "
                "tell them to check the app dashboard. Otherwise, use the context. "
                "If you mention a report or plan, always provide the specific link: "
                "For the Green Plan, use https://www.greenplan.gov.sg. "
                "For general SG sustainability, use https://www.mse.gov.sg/resources/sgp-2030/."
            )

            prompt = f"<|im_start|>system\n{system_instr}\nContext:\n{context}\n"
            if hist_str:
                prompt += f"History:\n{hist_str}\n"
            prompt += (
                "<|im_end|>\n"
                f"<|im_start|>user\n{user_text}<|im_end|>\n"
                "<|im_start|>assistant\n"
            )
        
            output = self.generator(
                prompt,
                max_new_tokens=80,
                do_sample=False,
                return_full_text=False,
                pad_token_id=getattr(self.generator.tokenizer, "eos_token_id", None),
            )
            raw = output[0]["generated_text"].strip()
            reply = self._parse_to_two_sentences(raw)

        self._save_history(user_text, reply, user_id)
        return reply

    def _save_history(self, user_text: str, reply: str, user_id: str):
        profile = user_profiles.get(user_id, {"name": None, "history": []})
        profile["history"].append({"u": user_text, "b": reply})
        user_profiles[user_id] = profile

@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.bot_ready = False
    app.state.brain_ready = False

    async def init_bot():
        try:
            brain = EmbeddingBrain()
            await brain.smart_load(SUSTAINABILITY_REPORTS)
            app.state.brain_ready = True

            # Bot shell (generator lazy-loads on first use)
            models["sustain_bot"] = PockeTreeBot(brain)
            app.state.bot_ready = True
            print("Bot ready.")
        except Exception as e:
            print("Bot init failed:", repr(e))

    if not DISABLE_WARMUP:
        asyncio.create_task(init_bot())

    yield


app = FastAPI(lifespan=lifespan)


@app.get("/health")
def health():
    # Always 200 once container is alive
    return {"status": "ok"}


@app.get("/ready")
def ready():
    # 503 until brain + bot shells are ready
    if not getattr(app.state, "bot_ready", False):
        raise HTTPException(status_code=503, detail="bot not ready")
    return {
        "ready": True,
        "brain_ready": getattr(app.state, "brain_ready", False),
        "device": DEVICE,
        "cache_path": CACHE_PATH,
    }


@app.post("/chat")
async def chat(req: ChatReq):
    bot = models.get("sustain_bot")
    if bot is None:
        raise HTTPException(status_code=503, detail="PockeTreeBot not ready")

    # If brain still loading, respond gracefully (feature preserved)
    if not bot.brain.ready:
        return {"response": "I'm still studying my sustainability reports! Please give me a moment 😅"}

    # Concurrency guard (Cloud Run safe)
    async with chat_semaphore:
        reply = await anyio.to_thread.run_sync(bot.get_response, req.message, req.user_id)
        return {"bot": "PockeTree", "response": reply}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--bake", action="store_true", help="Process local PDFs in ./data into brain_cache.pkl")
    parser.add_argument("--version", type=str, default=None, help="Optional version tag for bake()")
    args = parser.parse_args()

    if args.bake:
        brain = EmbeddingBrain()
        brain.bake(custom_version=args.version)
    else:
        import uvicorn
        port = int(os.environ.get("PORT", 8080))
        uvicorn.run(app, host="0.0.0.0", port=port, timeout_keep_alive=600, log_level="info")

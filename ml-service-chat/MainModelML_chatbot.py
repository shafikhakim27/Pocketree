import anyio
import httpx
import io, torch, time, base64, json
import numpy as np
import os # for local
import pickle # for local
import asyncio
from contextlib import asynccontextmanager
from fastapi import FastAPI, Form, UploadFile, File, HTTPException
from io import BytesIO
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.document_loaders import PyPDFLoader
from pydantic import BaseModel, Field
from sentence_transformers import SentenceTransformer
from typing import List, Optional
from transformers import pipeline

# --- GLOBALS & SETUP --- #
DISABLE_WARMUP = os.getenv("DISABLE_WARMUP", "0") == "1"
CHAT_TIMEOUT_SECONDS = float(os.getenv("CHAT_TIMEOUT_SECONDS", "55"))

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Start serving immediately; load bot in background
    app.state.bot_ready = False

    async def init_bot():
        try:
            brainBot = EmbeddingBrain()
            await brainBot.cloud_warmup(SUSTAINABILITY_REPORTS)
            models["sustain_bot"] = PockeTreeBot(brainBot)
            app.state.bot_ready = True
            print("PockeTreeBot is loaded!")
        except Exception as e:
            print("Bot init failed:", repr(e))

    if not DISABLE_WARMUP:
        asyncio.create_task(init_bot())

    yield

app = FastAPI(lifespan=lifespan)

### --- USE CASE 3: POCKETREE BOT --- ###

# Allowed categories
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

# Global placeholders
preprocess, tokenizer = None, None
chat_histories = {}
user_profiles = {}
models = {}

# Best available engine
if torch.cuda.is_available():
    device = "cuda" # Google Cloud GPU
elif torch.backends.mps.is_available():
    device = "mps"  # Mac M1/M2/M3 GPU acceleration
else:
    device = "cpu"  # Local fallback

models = {"sustain_bot": None}
chat_semaphore = anyio.Semaphore(1)

# --- Prepare for Use Case 3 ---

class EmbeddingBrain:
    def __init__(self):
        self.encoder = SentenceTransformer('all-MiniLM-L6-v2', device=device)
        self.chunks = []
        self.vectors = None

    async def cloud_warmup(self, urls):
        
        cache_path = "/tmp/brain_cache.pkl"
        if os.path.exists(cache_path):   # for local
            with open("brain_cache.pkl", "rb") as f:
                data = pickle.load(f)
                self.chunks = data["chunks"]
                self.vectors = data["vectors"]
            return

        fact_chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()]
        all_docs = []
    
        async with httpx.AsyncClient(timeout=30.0, follow_redirects=True) as client:
                    for url in urls:
                        try:
                            response = await client.get(url, headers={"User-Agent": "Mozilla/5.0"})
                            with io.BytesIO(response.content) as f:
                                import tempfile
                                with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
                                    tmp.write(f.read())
                                    tmp_path = tmp.name
                                loader = PyPDFLoader(tmp_path)
                                docs = await anyio.to_thread.run_sync(loader.load) # pyright: ignore[reportAttributeAccessIssue]
                                all_docs.extend(docs)
                                os.remove(tmp_path)
                        except Exception as e: print(f"Error loading {url}: {e}")

        splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
        pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
        self.chunks = fact_chunks + pdf_chunks
                
        if self.chunks:
            self.vectors = await anyio.to_thread.run_sync( # pyright: ignore[reportAttributeAccessIssue]
                lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
            )
            with open("brain_cache.pkl", "wb") as f:
                pickle.dump({"chunks": self.chunks, "vectors": self.vectors}, f)

        # headers = {
        #     "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
        # }

        # for url in urls:
        #     try:
        #         loader = PyPDFLoader(url, headers=headers)
        #         docs = await anyio.to_thread.run_sync(loader.load) # pyright: ignore[reportAttributeAccessIssue]
        #         all_docs.extend(docs)
        #     except Exception as e:
        #         print(f"Skipping {url} due to error: {e}")

        # splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)

        # self.chunks = [doc.page_content for doc in splitter.split_documents(all_docs)]
        
        # if self.chunks:
        #     self.vectors = await anyio.to_thread.run_sync( # pyright: ignore[reportAttributeAccessIssue]
        #         lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
        #     )

    # def search(self, query, k=2):
    #     if self.vectors is None: return ""
    #     query_vec = self.encoder.encode([query], normalize_embeddings=True)
    #     scores = np.dot(self.vectors, query_vec.T).flatten()
    #     top_indices = np.argsort(scores)[-k:][::-1]
    #     return "\n".join([self.chunks[i] for i in top_indices])

    def search(self, query, k=1):
        if self.vectors is None or len(self.vectors) == 0: 
            return ""
        query_vec = self.encoder.encode([query], normalize_embeddings=True)
        # Cosine similarity on normalized vectors is just the dot product
        scores = np.dot(self.vectors, query_vec.T).flatten()
        
        # Safety check
        k = min(k, len(self.chunks))
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

    def extract_name(self, text, user_id):
        text_lower = text.lower().strip()
        profile = user_profiles.get(user_id, {"name": None, "history": []})
        
        # Common patterns for names
        patterns = ["my name is", "i am ", "call me ", "im "]
        
        for p in patterns:
            if p in text_lower:
                # Grab the part after the pattern
                name = text_lower.split(p)[-1].strip().split()[0].strip(".!?")
                profile["name"] = name.capitalize()
                user_profiles[user_id] = profile
                return profile["name"]
                
        return profile.get("name")

    # def extract_and_greet(self, text, user_id):
    #     text_lower = text.lower()
    #     profile = user_profiles.get(user_id, {"name": None, "history": []})
        
    #     # Simple extraction logic
    #     if "my name is" in text_lower:
    #         name = text.split("is")[-1].strip().strip(".!?")
    #         profile["name"] = name
    #     elif "i am " in text_lower and len(text_lower.split()) < 5:
    #         name = text.split("am")[-1].strip().strip(".!?")
    #         profile["name"] = name
            
    #     user_profiles[user_id] = profile
    #     return profile["name"]
    
    def handle_small_talk(self, text, user_id):
        profile = user_profiles.get(user_id, {"name": None, "history": []})
        name = self.extract_name(text, user_id)
        text_lower = text.lower().strip()
        text_clean = text.lower().strip().replace("?", "").replace("!", "").replace(".", "")
        words = text_clean.split()
        last_bot_msg = profile["history"][-1]["b"] if profile["history"] else ""
        
        if "what is your name" in last_bot_msg.lower():
            if not profile.get("name") and words:
                new_name = words[-1].capitalize()
                profile["name"] = new_name
                user_profiles[user_id] = profile 
                return f"So good to know you, {new_name}!"

        if any(word in text_lower for word in ["stats", "statistics", "coins", "level", "progress", "plant health"]):
            return ("I'm sorry, I don't have access to that information yet right now. "
                    "But if you are referring to your personal stats at PockeTree, feel free to check your app or visit https://pocketree-api.azurewebsites.net/" )
        
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
            if name: return f"You are {name}. So good to know you!"
            return "I don't know your name yet.... What should I call you?"
        
        user_greetings = ["hi", "hello", "yo", "what's up", "wassup", "whassup", "whatsup", "wzzup", "wazzup", "good day", "good morning", "good afternoon", "good evening", "heya", "mornin"]
        if words and words[0] in user_greetings and len(words) < 4:
            if not name:
                return "Hello! PockeTree here. Before we begin, what is your name?"
            return f"Hello {name}! PockeTree here. Ready to go green today?"
        
        if any(q in text_lower for q in ["who are you", "who you", "your name"]):
            return "Hello! This is PockeTree speaking. How are you today?"
        
        if words and words[0] in ["thanks", "thank", "thx", "thank you", "tx"]:
            return "You're most welcome! Do you have any other questions about sustainability?"
        
        if words and words[0] in ["ok", "oki", "okays", "yea", "yes"] and len(words) > 2:
            return None
        
        casual_fillers = ["same old", "not much", "im good", "am good", "me too", "just chilling", "cool", "ok", "sure", "nice"]
        if any(f == text_clean for f in casual_fillers): 
            return "Great! Do you have any specific questions about green efforts in Singapore today?"
        
        if len(words) > 4: return None 
        
        if any(sad in text_lower for sad in ["lonely", "sad", "bored"]):
            return "I'm sorry to hear that, let's go walk at the park and see some greenery. It will make you feel better!"
            
        if "funny" in text_lower or "joke" in text_lower:
            return "Sorry, I have only one: why did the recycling bin break up with the trash can? Because he found out she was 'wasted'! Hahaha!"
        
        if "do you" in text_lower or "is it" in text_lower:
            return None

        return None

    def _parse_to_two_sentences(self, text: str):
        import re
        # Cleanup labels and artifacts
        text = re.sub(r'(Sentence \d:|Assistant:|Note:|\*\*)', '', text, flags=re.IGNORECASE).strip()
        sentences = re.split(r'(?<=[.!?])\s+', text)
        sentences = [s for s in sentences if len(s) > 5]
        
        if not sentences:
            return "I'm not sure. Why don't we check https://www.mse.gov.sg/resources/sgp-2030/ together?" 

        clean_response = " ".join(sentences[:2])
        if not clean_response.endswith(('.', '!', '?')):
            clean_response = clean_response.rsplit(' ', 1)[0] + "."
        return clean_response
    
    def get_response(self, user_text: str, user_id: str):

        reply = self.handle_small_talk(user_text, user_id)

        if not reply:
            small_talk = self.handle_small_talk(user_text, user_id)
            if small_talk: return small_talk

            profile = user_profiles.get(user_id, {"name": "Friend", "history": []})
            name = profile.get("name") or "Friend"
            hist_str = "\n".join([f"User: {m['u']}\nBot: {m['b']}" for m in profile["history"][-2:]])
            context = self.brain.search(user_text, k=2)

            system_instr = (
                f"You are PockeTree, a wise and friendly Singaporean eco-mentor talking to {name}. "
                "Give the answer in EXACTLY two short sentences. "
                "Sentence 1: The facts. Sentence 2: Action."
                "Refer to the Chat History if the user asks follow-up questions."
                "If the user asks about their personal app data (like coins, stats, or plant health), "
                "tell them to check the app dashboard. Otherwise, use the context. "
            )

            prompt = f"<|im_start|>system\n{system_instr}\nContext:\n{context}\nHistory:\n{hist_str}<|im_end|>\n"
            prompt += f"<|im_start|>user\n{user_text}<|im_end|>\n<|im_start|>assistant\n"

            try:
                output = self.generator(
                    prompt,
                    max_new_tokens=80,
                    stop_sequence="<|im_end|>"
                )
                raw_reply = output[0]['generated_text'].split("<|im_start|>assistant\n")[-1].strip()
                reply = self._parse_to_two_sentences(raw_reply)
            except Exception as e:
                print("Generation failed:", repr(e))
                reply = (
                    "I hit a temporary delay while thinking about that question. "
                    "Please try again in a moment."
                )

        profile = user_profiles.get(user_id, {"name": None, "history": []})
        profile["history"].append({"u": user_text, "b": reply})
        user_profiles[user_id] = profile

        return reply
    
    # def get_response(self, user_text: str, user_id: str):
    #     # 1. Handle name and small talk
    #     small_talk = self.handle_small_talk(user_text, user_id)
    #     if small_talk: return small_talk

    #     # 2. Get Profile & History
    #     profile = user_profiles.get(user_id, {"name": "Friend", "history": []})
    #     name = profile.get("name") or "Friend"
    #     hist_str = "\n".join([f"User: {m['u']}\nBot: {m['b']}" for m in profile["history"][-2:]])

    #     # 3. Build Prompt
    #     # facts = [v for k, v in SG_EXPERT_FACTS.items() if k in user_text.lower()]
    #     # pdf_context = self.brain.search(user_text, k=1)

    #     context = self.brain.search(user_text, k=2)
        
    #     system_instr = (
    #         f"You are PockeTree, a wise and friendly Singaporean eco-mentor talking to {name}. "
    #         "Give the answer in EXACTLY two short sentences. "
    #         "Sentence 1: The facts. Sentence 2: Action."
    #         "Refer to the Chat History if the user asks follow-up questions."
    #     )
        
    #     prompt = f"<|im_start|>system\n{system_instr}\nContext:\n{context}\nHistory:\n{hist_str}<|im_end|>\n"
    #     prompt += f"<|im_start|>user\n{user_text}<|im_end|>\n<|im_start|>assistant\n"

    #     output = self.generator(prompt, max_new_tokens=60, do_sample=True, temperature=0.7, stop_sequence=["<|im_end|>"])
    #     reply = output[0]['generated_text'].split("<|im_start|>assistant\n")[-1].strip()
        
    #     # 4. Save to history
    #     profile["history"].append({"u": user_text, "b": reply})
    #     user_profiles[user_id] = profile
        
    #     return self._parse_to_two_sentences(reply)
    
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

class ChatReq(BaseModel):
    user_id: str = "default_user"
    message: str

@app.post("/chat")
async def chat(req: ChatReq):
    bot = models.get("sustain_bot")
    if bot is None: # pyright: ignore[reportAttributeAccessIssue]
        raise HTTPException(status_code=503, detail="PockeTreeBot not ready")
    user_id = req.user_id 
    user_id = getattr(req, 'user_id', 'anon_user')
    try:
        async with chat_semaphore:
            with anyio.fail_after(CHAT_TIMEOUT_SECONDS):
                reply = await anyio.to_thread.run_sync(bot.get_response, req.message, user_id) # pyright: ignore[reportAttributeAccessIssue]
    except TimeoutError:
        reply = (
            "That question needs more processing time than usual. "
            "Please ask a shorter follow-up or try again."
        )
    except Exception as e:
        print("Chat endpoint failed:", repr(e))
        reply = (
            "I ran into a temporary issue answering that. "
            "Please try again shortly."
        )
    return {"bot": "PockeTree", "response": reply}

### --- MISC --- ###

@app.get("/health")
def health():
    return {"status": "ok", "bot_ready": getattr(app.state, "bot_ready", False)}

if __name__ == "__main__":
    import uvicorn
    import os
    try:
        port = int(os.environ.get("PORT", 8080))
        uvicorn.run(app, host="0.0.0.0", port=port, log_level="info")
        print(f"Starting server on port {port}")
    except KeyboardInterrupt:
        print("\nShutting down gracefully... Bye!")

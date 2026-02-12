import anyio
import argparse
import asyncio
import httpx
import io, torch, time, base64, json
import numpy as np
import os # for local
import pickle # for local
import sys
import threading

from contextlib import asynccontextmanager
from datetime import datetime
from fastapi import FastAPI, Form, UploadFile, File, HTTPException
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.document_loaders import PyMuPDFLoader
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from typing import List, Optional
from transformers import pipeline, AutoModelForCausalLM, AutoTokenizer

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

class ChatReq(BaseModel):
    user_id: str = "default_user"
    message: str

class EmbeddingBrain:
    def __init__(self):
        self.encoder = SentenceTransformer("sentence-transformers/all-MiniLM-L6-v2", device="cpu")
        self.ready = False
        self.chunks = []
        self.vectors = None
        self._lock = asyncio.Lock()

    async def smart_load(self, urls):
        # Step 1: Try to find the pre-baked pickle file
        if os.path.exists("brain_cache.pkl"):
            try:
                print("Found brain_cache.pkl. Loading...")
                with open("brain_cache.pkl", "rb") as f:
                    data = pickle.load(f)
                    self.chunks = data["chunks"]
                    self.vectors = data["vectors"]
                self.ready = True
                print(f"Brain restored. Version: {data.get('version', 'unknown')}")
                return 
            except Exception as e:
                print(f"Pickle corrupted: {e}")

        # Step 2: Fallback (Redo as per normal)
        print("No valid cache found. Starting fallback PDF download...")
        await self.cloud_warmup(urls)

    async def cloud_warmup(self, urls):

        async with self._lock:
            if self.ready: return
            
            all_docs = []

            async with httpx.AsyncClient(timeout=60.0, follow_redirects=True) as client:
                for url in urls:
                    try:
                        response = await client.get(url, headers={"User-Agent": "Mozilla/5.0"})
                        response.raise_for_status()
                        with io.BytesIO(response.content) as f:
                            import tempfile
                            with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
                                tmp.write(f.read())
                                tmp_path = tmp.name
                            loader = PyMuPDFLoader(tmp_path)
                            docs = await anyio.to_thread.run_sync(loader.load) # pyright: ignore[reportAttributeAccessIssue]
                            all_docs.extend(docs)
                            os.remove(tmp_path)
                    except Exception as e: print(f"Error loading {url}: {e}")

            if all_docs:
                splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
                fact_chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()]
                pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
                self.chunks = fact_chunks + pdf_chunks
                
                vectors_raw = await anyio.to_thread.run_sync( # pyright: ignore[reportAttributeAccessIssue]
                    lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
                )
                self.vectors = vectors_raw.astype('float16')
                self.ready = True
                print("Cloud Warmup Complete.")

    # async def cloud_warmup(self, urls):
    #     with self._lock:
    #         if self.ready: return

    #     if os.path.exists("brain_cache.pkl"):   
    #         print("Loading brain from cache...")
    #         with open("brain_cache.pkl", "rb") as f:
    #             data = pickle.load(f)
    #             self.chunks = data["chunks"]
    #             self.vectors = data["vectors"].astype('float16')
    #         self.ready = True
    #         return
        
    #     all_docs = []
    #     for filename in os.listdir(folder_path):
    #         if filename.endswith(".pdf"):
    #             loader = PyMuPDFLoader(os.path.join(folder_path, filename))
    #             docs = await anyio.to_thread.run_sync(loader.load)
    #             all_docs.extend(docs)

    #     # 3. Split and Embed
    #     splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
    #     pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
        
    #     fact_chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()]
    #     self.chunks = fact_chunks + pdf_chunks
                
    #     if self.chunks:
    #         vectors_raw = await anyio.to_thread.run_sync(
    #             lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
    #         )
    #         self.vectors = vectors_raw.astype('float16')
    #         with open("brain_cache.pkl", "wb") as f:
    #             pickle.dump({"chunks": self.chunks, "vectors": self.vectors}, f)
        
    #     self.ready = True

    #     async with httpx.AsyncClient(timeout=30.0, follow_redirects=True) as client:
    #                 for url in urls:
    #                     try:
    #                         response = await client.get(url, headers={"User-Agent": "Mozilla/5.0"})
    #                         response.raise_for_status()

    #                         with io.BytesIO(response.content) as f:
    #                             import tempfile
    #                             with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
    #                                 tmp.write(f.read())
    #                                 tmp_path = tmp.name

    #                             # loader = PyPDFLoader(tmp_path)
    #                             loader = PyMuPDFLoader(tmp_path)
    #                             docs = await anyio.to_thread.run_sync(loader.load) # pyright: ignore[reportAttributeAccessIssue]
    #                             all_docs.extend(docs)
    #                             os.remove(tmp_path)

    #                     except Exception as e: print(f"Error loading {url}: {e}")

    #     splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
    #     pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
    #     self.chunks = fact_chunks + pdf_chunks
                
    #     if self.chunks:
    #         vectors_raw = await anyio.to_thread.run_sync(
    #             lambda: self.encoder.encode(self.chunks, normalize_embeddings=True)
    #         )
    #         self.vectors = vectors_raw.astype('float16')
    #         with open("brain_cache.pkl", "wb") as f:
    #             pickle.dump({"chunks": self.chunks, "vectors": self.vectors}, f)
        
    #     self.ready = True

    def bake(self, custom_version=None):
        "Processing PDF to the pickle file with SG_ priority"
        version_tag = custom_version or f"v-{datetime.now().strftime('%Y%m%d')}"
        print(f"--- Starting SG_ --bake ({version_tag}) ---")
        
        pdf_folder = "./data"
        if not os.path.exists(pdf_folder):
            os.makedirs(pdf_folder)
            print(f"Created folder '{pdf_folder}'. Please put your PDFs in it and run --bake again.")
            return
        
        # 1. Separate files into SG and others to ensure priority
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

        # 2. Split and Vectorize
        splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
        self.chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()] + \
                      [d.page_content for d in splitter.split_documents(all_docs)]
        
        print(f"Vectorizing {len(self.chunks)} chunks...")
        vectors_raw = self.encoder.encode(self.chunks, normalize_embeddings=True)
        
        # 3. Save as float16 to keep it cloud-friendly
        with open("brain_cache.pkl", "wb") as f:
            pickle.dump({
                "version": version_tag,
                "chunks": self.chunks,
                "vectors": vectors_raw.astype('float16')
            }, f)
        print(f"Done! brain_cache.pkl created with {len(self.chunks)} chunks.")

    # def bake(self, custom_version=None):
    #     "Processing PDF to the pickle file"

    #     version_tag = custom_version or f"v-{datetime.now().strftime('%Y%m%d')}"
    #     print(f"--- Starting Bake Function ({version_tag}) ---")

    #     all_docs = []

    #     from sentence_transformers import SentenceTransformer
    #     self.encoder = SentenceTransformer("sentence-transformers/all-MiniLM-L6-v2")
        
    #     # 1. Load PDFs (Assumes they are in a folder named /data)
    #     pdf_folder = "./data"
    #     if not os.path.exists(pdf_folder):
    #         os.makedirs(pdf_folder)
    #         print(f"Created folder '{pdf_folder}'. Please put your PDFs in it and run --bake again.")
    #         return
        
    #     for filename in os.listdir(pdf_folder):
    #         if filename.endswith(".pdf"):
    #             print(f"Parsing {filename}...")
    #             loader = PyMuPDFLoader(os.path.join(pdf_folder, filename))
    #             all_docs.extend(loader.load())

    #     # 2. Split
    #     splitter = RecursiveCharacterTextSplitter(chunk_size=600, chunk_overlap=100)
    #     pdf_chunks = [d.page_content for d in splitter.split_documents(all_docs)]
    #     fact_chunks = [f"FACT: {v}" for v in SG_EXPERT_FACTS.values()]
    #     self.chunks = fact_chunks + pdf_chunks

    #     # 3. Vectorize
    #     print(f"Vectorizing {len(self.chunks)} chunks...")
    #     vectors_raw = self.encoder.encode(self.chunks, normalize_embeddings=True)
        
    #     # 4. Save
    #     data = {
    #         "version": version_tag,
    #         "model": "all-MiniLM-L6-v2",
    #         "chunks": self.chunks,
    #         "vectors": vectors_raw.astype('float16')
    #     }
    #     with open("brain_cache.pkl", "wb") as f:
    #         pickle.dump(data, f)
    #     print("Done! 'brain_cache.pkl' created.")

    async def load_or_warmup(self):
        "Normal loading logic for FastApi"
        if os.path.exists("brain_cache.pkl"):
            with open("brain_cache.pkl", "rb") as f:
                data = pickle.load(f)
                self.chunks = data["chunks"]
                self.vectors = data["vectors"]
                print(f"Brain Loaded: {data.get('version', 'unknown')}")
            self.ready = True
        else:
            print("No brain_cache.pkl found! Use 'python your_file.py --bake' first.")

    def search(self, query, k=1):
        if not self.ready or self.vectors is None: return ""
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
        model_id = "Qwen/Qwen2.5-1.5B-Instruct"
        self.generator = pipeline(
            "text-generation", 
            model=model_id,
            torch_dtype=torch.bfloat16,
            # device_map="auto"
            device="mps" if torch.backends.mps.is_available() else "cpu"
        )
    
    def extract_name(self, text, user_id):
        text_lower = text.lower().strip()
        patterns = ["my name is ", "i am ", "call me ", "im "]
        
        for p in patterns:
            if p in text_lower:
                # Just grab the next word and capitalize it. No more "forbidden" checks.
                parts = text_lower.split(p)
                if len(parts) > 1:
                    name = parts[1].split()[0].strip(".!?")
                    return name.capitalize()
        return None

    # def extract_name(self, text, user_id):
    #     text_lower = text.lower().strip()
    #     profile = user_profiles.get(user_id, {"name": None, "history": []})
        
    #     # Common patterns for names
    #     patterns = ["my name is", "i am ", "call me ", "im "]
        
    #     for p in patterns:
    #         if p in text_lower:
    #             # Grab the part after the pattern
    #             name = text_lower.split(p)[-1].strip().split()[0].strip(".!?")
    #             profile["name"] = name.capitalize()
    #             user_profiles[user_id] = profile
    #             return profile["name"]
                
    #     return profile.get("name")

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
        noise_words = ["zzz", "tsk", "hmmm", "uhm", "err", "haiz", "tch"]
        text_clean = text.lower().strip().replace("?", "").replace("!", "").replace(".", "")

        for word in noise_words:
            text_clean = text_clean.replace(word, "").strip()
        
        text_lower = text_clean 

        profile = user_profiles.get(user_id, {"name": None, "history": []})
        name = self.extract_name(text_clean, user_id)
        words = text_clean.split()
        last_bot_msg = profile["history"][-1]["b"] if profile["history"] else ""

        support_keywords = ["support", "contact", "help me", "talk to human", "email", "issue", "problem", "bug", "error", "not working"]
        if any(k in text_clean for k in support_keywords):
            if any(bug in text_clean for bug in ["bug", "error", "issue", "not working", "problem"]):
                return ("I'm sorry to hear that. You can reach our support team at https://pocketree-api.azurewebsites.net/")
            
            return ("Need a hand? You can reach our support team at https://pocketree-api.azurewebsites.net/")
        
        if "what is your name" in last_bot_msg.lower():
            if not profile.get("name") and words:
                new_name = words[-1].capitalize()
                profile["name"] = new_name
                user_profiles[user_id] = profile 
                return f"So good to know you, {new_name}!"

        if any(word in text_lower for word in ["stats", "statistics", "coins", "level", "progress", "plant health"]):
            return ("I'm sorry, I don't have access to that information yet right now. "
                    "But if you are referring to your personal stats at Pocketree, feel free to check your app or visit https://pocketree-api.azurewebsites.net/" )
        
        bot_compliments = ["smart", "genius", "amazing", "helpful", "good bot"]
        if any(comp in text_lower for comp in bot_compliments):
            if any(you in text_lower for you in ["you are", "you're", "youre", "ur "]):
                return f"That is very kind of you, {name or 'Friend'}!"
            
        identity_queries = ["who are you", "what are you", "who you", "your name", "are you alive"]
        if any(q in text_clean for q in identity_queries):
            if "alive" in text_clean:
                return "I am an AI eco-friend. I am very much 'alive' with passion for the planet!"
            return "I am PockeTreeBot, your friendly eco-buddy. I'm here to help you live more sustainably!"

        if "how are you" in text_lower:
            return "I'm good! Just busy thinking how to save the planet. You?"

        if "who am i" in text_lower or "my name" in text_lower:
            if name: return f"You are {name}. So good to know you!"
            return "I don't know your name yet.... What should I call you?"
        
        meeting_phrases = ["nice to meet you", "good to know you", "pleasure"]
        if any(p in text_clean for p in meeting_phrases):
            return f"The pleasure is all mine! Ready to learn more about Singapore's green efforts?"
        
        user_greetings = ["hi", "hello", "yo", "what's up", "wassup", "whassup", "whatsup", "wzzup", "wazzup", "good day", "good morning", "good afternoon", "good evening", "heya", "mornin"]
        if words and words[0] in user_greetings and len(words) < 4:
            if not name:
                return "Hello! PockeTreeBot here. Before we begin, what is your name?"
            return f"Hello {name}! PocketreeBot here. Ready to go green today?"
        
        if any(q in text_lower for q in ["who are you", "who you", "your name"]):
            return "Hello! This is PocketreeBot speaking. How are you today?"
        
        if words and words[0] in ["thanks", "thank", "thx", "thank you", "tx"]:
            return "You're most welcome! Do you have any other questions about sustainability?"
        
        if words and words[0] in ["ok", "oki", "okays", "yea", "yes"] and len(words) > 2:
            return None
        
        casual_fillers = ["same old", "not much", "im good", "am good", "me too", "just chilling", "cool", "ok", "sure", "nice"]
        if text_clean in casual_fillers:
            return "Great! Do you have any specific questions about green efforts in Singapore today?"
        
        cool_reactions = ["sounds cool", "cool", "interesting", "wow", "awesome", "great"]
        if text_clean in cool_reactions:
            return "Yea!"
        
        if text_clean in ["ok", "bye", "goodbye", "see ya", "cya"]:
            return "Goodbye! Remember, every small green act counts. Come back if you have more questions!"
        
        if any(sad in text_lower for sad in ["lonely", "sad", "bored"]):
            return "I'm sorry to hear that, let's go walk at the park and see some greenery. It will make you feel better!"
            
        if "funny" in text_lower or "joke" in text_lower:
            return "Sorry, I have only one: why did the recycling bin break up with the trash can? Because he found out she was 'wasted'! Hahaha!"
        
        if "do you" in text_lower or "is it" in text_lower:
            return None
        
        if len(words) > 4: return None 

        return None

    def _parse_to_two_sentences(self, text: str):
        import re
        # Cleanup labels and artifacts
        text = re.sub(r'(?i)(The facts:|Action:|Sentence \d:|Assistant:)', '', text).strip()
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
            with torch.inference_mode():
                profile = user_profiles.get(user_id, {"name": "Friend", "history": []})
                name = profile.get("name") or "Friend"
                hist_str = "\n".join([f"User: {m['u']}\nBot: {m['b']}" for m in profile["history"][-2:]])
                context = self.brain.search(user_text, k=2)

                system_instr = (
                    f"You are PockeTreeBot, a wise and friendly Singaporean eco-mentor talking to {name}. "
                    "Give the answer in EXACTLY two short sentences. "
                    "Sentence 1: The facts. Sentence 2: Action."
                    "Refer to the Chat History if the user asks follow-up questions."
                    "If the user asks about their personal app data (like coins, stats, or plant health), "
                    "tell them to check the app dashboard. Otherwise, use the context. "
                    "If you mention a report or plan, always provide the specific link: "
                    "For the Green Plan, use https://www.greenplan.gov.sg. "
                    "For general SG sustainability, use https://www.mse.gov.sg/resources/sgp-2030/." 
                    "Do NOT use labels like 'The facts' or 'Action' in your response."
                )

                if not context or len(context.strip()) < 10:
                    context = "Have you ever learned about the Singapore Green Plan 2030?"

                prompt = f"<|im_start|>system\n{system_instr}\nContext:\n{context}\nHistory:\n{hist_str}<|im_end|>\n"
                prompt += f"<|im_start|>user\n{user_text}<|im_end|>\n<|im_start|>assistant\n"

                # output = self.generator(prompt, max_new_tokens=80, stop_sequence="<|im_end|>")
                output = self.generator(
                    prompt, 
                    max_new_tokens=120, 
                    do_sample=False,  # Greedy decoding 
                    stop_sequence="<|im_end|>",
                    pad_token_id=self.generator.tokenizer.eos_token_id  # pyright: ignore[reportOptionalMemberAccess]
                )
                raw_reply = output[0]['generated_text'].split("<|im_start|>assistant\n")[-1].strip()
                reply = self._parse_to_two_sentences(raw_reply)

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

@asynccontextmanager
async def lifespan(app: FastAPI):
    # 1. Initialize the Brain & Bot shells immediately
    brainBot = EmbeddingBrain()
    models["sustain_bot"] = PockeTreeBot(brainBot) # pyright: ignore[reportArgumentType]
    
    asyncio.create_task(brainBot.smart_load(SUSTAINABILITY_REPORTS))
    
    print("Server is UP! Brain loading in background...")
    yield

app = FastAPI(lifespan=lifespan)

@app.post("/chat")
async def chat(req: ChatReq):
    bot = models.get("sustain_bot")
    if bot is None: # pyright: ignore[reportAttributeAccessIssue]
        raise HTTPException(status_code=503, detail="PockeTreeBot not ready")
    if not bot.brain.ready:
        return {"response": "I'm still studying my sustainability reports! Thank you for giving me some time to get smart!"}
    user_id = req.user_id 
    user_id = getattr(req, 'user_id', 'anon_user')
    reply = await anyio.to_thread.run_sync(bot.get_response, req.message, user_id) # pyright: ignore[reportAttributeAccessIssue]
    return {"response": reply}

### --- MISC --- ###

@app.get("/health")
def health():
    return {"status": "ok"}

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--bake", action="store_true", help="Process local PDFs")
    args = parser.parse_args()

    if args.bake:
        # Run local baking mode
        brain = EmbeddingBrain()
        brain.bake()
    else:
        # Run normal FastAPI server
        import uvicorn
        import os
        try:
            port = int(os.environ.get("PORT", 8080))
            uvicorn.run(app, host="0.0.0.0", port=port, timeout_keep_alive=600, log_level="info")
            print(f"Starting server on port {port}")
        except KeyboardInterrupt:
            print("\nShutting down gracefully... Bye!")


import anyio
import numpy as np
import re

from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.document_loaders import PyPDFLoader
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from transformers import pipeline


# Global placeholders
chat_histories = {}
user_profiles = {}


class EmbeddingBrain:
    def __init__(self):
        self.encoder = SentenceTransformer("all-MiniLM-L6-v2")
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
        if self.vectors is None:
            return ""
        query_vec = self.encoder.encode([query], normalize_embeddings=True)
        scores = np.dot(self.vectors, query_vec.T).flatten()
        top_indices = np.argsort(scores)[-k:][::-1]
        return "\n".join([self.chunks[i] for i in top_indices])


class PockeTreeBot:
    def __init__(self, brain):
        self.brain = brain
        self.generator = pipeline(
            "text-generation",
            model="Qwen/Qwen2.5-1.5B-Instruct",
            device=-1,
            torch_dtype="auto",
        )

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
        name = self.extract_and_greet(text, user_id)
        text_lower = text.lower().strip()

        if "hello" in text_lower or "hi" in text_lower:
            return f"Hi {name if name else ''}! How can I help you today?"
        if "how are you" in text_lower:
            return "I'm here and ready to help! Ask me about sustainability."
        if "thank" in text_lower:
            return "You're welcome! Happy to help."
        return None

    def _parse_to_two_sentences(self, text):
        clean = re.sub(r"\s+", " ", text).strip()
        parts = re.split(r"(?<=[.!?])\s+", clean)
        if not parts:
            return "Here's something useful: reduce waste and reuse items whenever possible."
        if len(parts) == 1:
            return parts[0]
        return " ".join(parts[:2])

    def get_response(self, user_text, user_id="default"):
        text_lower = user_text.lower().strip()
        small_talk = self.handle_small_talk(text_lower, user_id)
        if small_talk:
            return small_talk

        context = self.brain.search(text_lower, k=2)
        context = context if context else "General sustainability best practices."

        prompt = (
            "You are PockeTree, a concise sustainability assistant. "
            "Answer in 1-2 sentences, practical and specific.\n"
            f"Context:\n{context}\n"
            f"User: {user_text}\n"
            "Assistant:"
        )

        output = self.generator(prompt, max_new_tokens=80, do_sample=True, temperature=0.7)
        full_reply = output[0]["generated_text"].split("Assistant:")[-1].strip()
        return self._parse_to_two_sentences(full_reply)


SUSTAINABILITY_REPORTS = [
    "https://isomer-user-content.by.gov.sg/50/3db854a5-473a-4d18-ae26-6991464a17a1/ssbcombined-cover-text.pdf",
    "https://isomer-user-content.by.gov.sg/23/21ea81ce-ac0a-4351-bac9-41f65c426a72/zero-waste-sg-report-transparent-bin-pilot.pdf",
    "https://isomer-user-content.by.gov.sg/23/2e32645e-d2fd-4f65-95d6-2473c36b5dbf/climate-action-plan.pdf",
    "https://www.greenplan.gov.sg/files/SGP2023_overview.pdf",
    "https://unstats.un.org/sdgs/report/2025/The-Sustainable-Development-Goals-Report-2025.pdf",
]


@asynccontextmanager
async def lifespan(app: FastAPI):
    brain = EmbeddingBrain()
    await brain.cloud_warmup(SUSTAINABILITY_REPORTS)
    app.state.sustain_bot = PockeTreeBot(brain)
    print("Chat bot loaded.")
    yield


app = FastAPI(lifespan=lifespan)


class ChatReq(BaseModel):
    user_id: str = "default_user"
    message: str


@app.post("/chat")
async def chat(req: ChatReq):
    bot = getattr(app.state, "sustain_bot", None)
    if bot is None:
        raise HTTPException(status_code=503, detail="Chat bot disabled")
    reply = await anyio.to_thread.run_sync(bot.get_response, req.message, req.user_id)
    return {"bot": "PockeTree", "response": reply}


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

import io, torch, time, base64, json
import numpy as np
import open_clip
import threading

from contextlib import asynccontextmanager
from fastapi import FastAPI, Form, UploadFile, File, HTTPException, Request
from io import BytesIO
from PIL import Image, ImageOps
from pydantic import BaseModel, Field
from typing import List, Optional
from transformers import pipeline

### --- USE CASE 1: IMAGE VERIFICATION (CLIP) --- ###

# Global placeholders
preprocess, tokenizer = None, None

# Best available engine
if torch.cuda.is_available():
    device = "cuda" # Google Cloud GPU
elif torch.backends.mps.is_available():
    device = "mps"  # Mac M1/M2/M3 GPU acceleration
else:
    device = "cpu"  # Local fallback

models = {"clip": None}

@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.clip = CLIPService()
    yield

app = FastAPI(lifespan=lifespan)

class CLIPService:
    def __init__(self):
        self.device = device
        self.model, self.preprocess, self.tokenizer = None, None, None
        self.text_cache = {}
        self.pos_threshold = 0.150
        self.margin = 0.05
        self._load_lock = threading.Lock()

    def ensure_model(self):
        if self.model is not None:
            return
        with self._load_lock:
            if self.model is not None:
                return
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
    def classify_simple(self, image_feat, keyword: str):

        text_feat = self._get_features([f"a photo of a {keyword}", "object"])
            
        raw_sim = float(image_feat @ text_feat[0].T)
        probs = (100.0 * image_feat @ text_feat.T).softmax(dim=-1).cpu().numpy()[0]

        verified = bool(probs.argmax() == 0 and probs[0] >= 0.55 and raw_sim > 0.15)
        return {"verified": verified, "score": float(probs[0]), "method": "simple"}

    # --- MODEL 2: With Pos/Neg Keywords ---
    def classify_advanced(self, image_feat, pos_list: list, neg_list: list):
        # Create prompts
        pos_prompts = [f"a photo of {p}" for p in pos_list]
        neg_prompts = [f"a photo of {n}" for n in neg_list]

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
    def classify_softmax(self, image_feat, keyword: str):
        labels = [f"a {keyword}", "a blurry background", "a random object"]

        text_feat = self._get_features(labels)
        
        logits = (image_feat @ text_feat.T) * 100
        probs = logits.softmax(dim=-1).cpu().numpy()[0]
            
        verified = bool(probs.argmax() == 0 and probs[0] >= 0.70)
        return {"verified": verified, "score": float(probs[0]), "method": "softmax"}

@app.post("/classify")
def classify(
    request: Request,
    keyword: str = Form(...), 
    negative_keyword: Optional[str] = Form(None), 
    file: UploadFile = File(...)):

    service: CLIPService = request.app.state.clip
    service.ensure_model()
    
    # 1. Image & Keyword Prep
    content =  file.file.read()
    image_tensor = service._prepare_image(content)

    with torch.inference_mode():
        image_feat = service.model.encode_image(image_tensor) # pyright: ignore[reportOptionalMemberAccess, reportCallIssue]
        image_feat /= image_feat.norm(dim=-1, keepdim=True)

    def clean_keyword(k):
        if not k: 
            return []
        
        # 1. Handle the @"[""item""]" format
        if k.startswith('@"'):
            k = k.replace('@"', '').strip('"').replace('""', '"')
        
        # 2. Standardize whitespace
        k = k.strip()
        
        # 3. Parse if it looks like a JSON list
        if k.startswith("["):
            try:
                parsed_k = json.loads(k)
                print(f"DEBUG: Parsed {len(parsed_k)} keywords: {parsed_k}")
                return parsed_k
            except json.JSONDecodeError:
                # Fallback: manual strip for malformed strings
                items = k.strip("[]").split(",")
                return [i.strip().strip('"') for i in items]
                
        # 4. Return as single-item list if it's just a string
        return [k]
    
    def norm_sim(sim: float, low: float, high: float) -> float:
        return float(max(0.0, min(1.0, (sim - low) / (high - low))))

    pos_list = clean_keyword(keyword)
    neg_list = clean_keyword(negative_keyword)
    primary_keyword = pos_list[0] if pos_list else keyword

    # Run All 3 
    res1 = service.classify_simple(image_feat, primary_keyword)
    res2 = service.classify_advanced(image_feat, pos_list, neg_list)
    res3 = service.classify_softmax(image_feat, primary_keyword)

    s1 = float(res1["score"])
    s3 = float(res3["score"])
    s2 = norm_sim(float(res2["score"]), low=service.pos_threshold, high=service.pos_threshold + 0.10)

    # Feature weighting
    all_results = [res1, res2, res3]
    avg_score = 0.25*s1 + 0.50*s2 + 0.25*s3

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

    verified_count = sum(1 for r in all_results if r["verified"])
    majority_agreed = verified_count >= 2
    final_verified = bool(majority_agreed or avg_score > 0.65)

    return {
        "verified": final_verified,
        "confidence": float(avg_score)
    }

### --- MISC --- ###

@app.get("/health")
def health(request: Request):
    service: CLIPService = request.app.state.clip
    return {"status": "ok", "model_loaded": service.model is not None}

if __name__ == "__main__":
    import uvicorn
    import os
    try:
        port = int(os.environ.get("PORT", 8080))
        uvicorn.run(app, host="0.0.0.0", port=port, log_level="info")
        print(f"Starting server on port {port}")
    except KeyboardInterrupt:
        print("\nShutting down PockeTree gracefully... Bye!")
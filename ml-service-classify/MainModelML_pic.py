import io
import json
import threading
from contextlib import asynccontextmanager
from typing import Callable, Dict, List, Optional, Tuple

import open_clip
import torch
from fastapi import FastAPI, File, Form, HTTPException, Request, UploadFile
from PIL import Image, ImageOps, UnidentifiedImageError


def detect_device() -> str:
    if torch.cuda.is_available():
        return "cuda"
    if torch.backends.mps.is_available():
        return "mps"
    return "cpu"


def clean_keyword(value: Optional[str]) -> List[str]:
    if not value:
        return []

    text = value.strip()

    if text.startswith('@"'):
        text = text[2:]
    if text.endswith('"@'):
        text = text[:-2]
    if (text.startswith('"') and text.endswith('"')) or (
        text.startswith("'") and text.endswith("'")
    ):
        text = text[1:-1]
    text = text.replace('""', '"').strip()

    if text.startswith("["):
        try:
            parsed = json.loads(text)
            if isinstance(parsed, list):
                return [str(item).strip() for item in parsed if str(item).strip()]
        except json.JSONDecodeError:
            items = text.strip("[]").split(",")
            return [item.strip().strip('"') for item in items if item.strip()]

    return [text]


def normalize_similarity(sim: float, low: float, high: float) -> float:
    if high <= low:
        return 0.0
    return float(max(0.0, min(1.0, (sim - low) / (high - low))))


class CLIPService:
    def __init__(self) -> None:
        self.device = detect_device()
        self.model = None
        self.preprocess = None
        self.tokenizer: Optional[Callable[[List[str]], torch.Tensor]] = None
        self.pos_threshold = 0.150
        self.margin = 0.05
        self._load_lock = threading.Lock()

        self._text_feat_cache: Dict[Tuple[str, ...], torch.Tensor] = {}
        self._cache_lock = threading.Lock()

    def _resolve_tokenizer(self, model_name: str):
        if hasattr(open_clip, "get_tokenizer"):
            return open_clip.get_tokenizer(model_name)
        if hasattr(open_clip, "tokenize"):
            return open_clip.tokenize

        tok_mod = getattr(open_clip, "tokenizer", None)
        if tok_mod and hasattr(tok_mod, "tokenize"):
            return tok_mod.tokenize

        raise RuntimeError("Tokenizer API not found in open_clip module.")

    def ensure_model(self) -> None:
        if self.model is not None:
            return

        with self._load_lock:
            if self.model is not None:
                return

            model_name = "ViT-B-32"
            pretrained = "openai"
            model, _, preprocess = open_clip.create_model_and_transforms(
                model_name, pretrained=pretrained
            )
            self.model = model.to(self.device).eval()
            self.preprocess = preprocess
            self.tokenizer = self._resolve_tokenizer(model_name)
            print(f"CLIP loaded: {model_name}:{pretrained} on {self.device}")
            print(f"open_clip module: {getattr(open_clip, '__file__', 'unknown')}")

    def _prepare_image(self, img_bytes: bytes):
        if self.preprocess is None:
            raise RuntimeError("Preprocess pipeline is not loaded.")

        try:
            img = Image.open(io.BytesIO(img_bytes))
        except UnidentifiedImageError as e:
            raise ValueError("Uploaded file is not a valid image.") from e

        img = ImageOps.exif_transpose(img).convert("RGB")
        return self.preprocess(img).unsqueeze(0).to(self.device)

    def _get_features(self, phrases: List[str]) -> torch.Tensor:
        if self.tokenizer is None or self.model is None:
            raise RuntimeError("Model/tokenizer not loaded. Did ensure_model() fail?")

        key = tuple(phrases)
        with self._cache_lock:
            cached = self._text_feat_cache.get(key)
        if cached is not None:
            return cached

        tokens = self.tokenizer(phrases).to(self.device)

        with torch.inference_mode():
            feat = self.model.encode_text(tokens)
            feat = feat / feat.norm(dim=-1, keepdim=True)

        with self._cache_lock:
            self._text_feat_cache[key] = feat

        return feat

    def encode_image(self, image_tensor: torch.Tensor) -> torch.Tensor:
        if self.model is None:
            raise RuntimeError("Model not loaded. Did ensure_model() fail?")

        with torch.inference_mode():
            image_feat = self.model.encode_image(image_tensor)
            image_feat = image_feat / image_feat.norm(dim=-1, keepdim=True)
            return image_feat

    def classify_simple(self, image_feat: torch.Tensor, keyword: str) -> dict:
        text_feat = self._get_features([f"a photo of a {keyword}", "object"])

        raw_sim = float((image_feat @ text_feat[0].unsqueeze(-1)).squeeze().item())

        probs = (100.0 * image_feat @ text_feat.T).softmax(dim=-1).cpu().numpy()[0]
        verified = bool(probs.argmax() == 0 and probs[0] >= 0.55 and raw_sim > 0.15)
        return {"verified": verified, "score": float(probs[0]), "method": "simple"}

    def classify_advanced(
        self, image_feat: torch.Tensor, pos_list: List[str], neg_list: List[str]
    ) -> dict:
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

    def classify_softmax(self, image_feat: torch.Tensor, keyword: str) -> dict:
        labels = [f"a {keyword}", "a blurry background", "a random object"]
        text_feat = self._get_features(labels)
        logits = (image_feat @ text_feat.T) * 100
        probs = logits.softmax(dim=-1).cpu().numpy()[0]
        verified = bool(probs.argmax() == 0 and probs[0] >= 0.70)
        return {"verified": verified, "score": float(probs[0]), "method": "softmax"}


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.clip = CLIPService()
    app.state.clip.ensure_model()
    yield


app = FastAPI(lifespan=lifespan)


@app.post("/classify")

async def classify(
    request: Request,
    keyword: str = Form(...),
    negative_keyword: Optional[str] = Form(None),
    file: UploadFile = File(...),
):
    service: CLIPService = request.app.state.clip
    service.ensure_model()

    content = await file.read()
    if not content:
        raise HTTPException(status_code=400, detail="Empty upload.")

    try:
        image_tensor = service._prepare_image(content)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e)) from e

    image_feat = service.encode_image(image_tensor)

    pos_list = clean_keyword(keyword)
    neg_list = clean_keyword(negative_keyword)

    if not pos_list:
        raise HTTPException(status_code=400, detail="keyword must not be empty.")

    primary_keyword = pos_list[0]

    res_simple = service.classify_simple(image_feat, primary_keyword)
    res_advanced = service.classify_advanced(image_feat, pos_list, neg_list)
    res_softmax = service.classify_softmax(image_feat, primary_keyword)

    s1 = float(res_simple["score"])
    s2 = normalize_similarity(
        float(res_advanced["score"]),
        low=service.pos_threshold,
        high=service.pos_threshold + 0.10,
    )
    s3 = float(res_softmax["score"])

    all_results = [res_simple, res_advanced, res_softmax]
    avg_score = (0.25 * s1) + (0.50 * s2) + (0.25 * s3)
    verified_count = sum(1 for result in all_results if result["verified"])
    majority_agreed = verified_count >= 2
    final_verified = bool(majority_agreed or avg_score > 0.65)

    return {"verified": final_verified, "confidence": float(avg_score)}


@app.get("/")
def root():
    return {"status": "ok"}


@app.get("/healthz")
def healthz():
    return {"status": "healthy"}


@app.get("/readyz")
def readyz(request: Request):
    service: CLIPService = request.app.state.clip
    return {"status": "ready", "model_loaded": service.model is not None}


@app.get("/health")
def health(request: Request):
    service: CLIPService = request.app.state.clip
    return {"status": "ok", "model_loaded": service.model is not None}


if __name__ == "__main__":
    import os
    import uvicorn

    try:
        port = int(os.environ.get("PORT", 8080))
        print(f"Starting server on port {port}")
        uvicorn.run(app, host="0.0.0.0", port=port, log_level="info")
    except KeyboardInterrupt:
        print("\nShutting down PockeTree gracefully... Bye!")

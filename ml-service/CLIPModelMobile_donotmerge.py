import io, torch, time, base64
from fastapi import FastAPI, Form, UploadFile, File, HTTPException
from pydantic import BaseModel
from PIL import Image
import open_clip

app = FastAPI()

# Global placeholders - no loading at startup
model, preprocess, tokenizer = None, None, None

def _ensure_model():
    global model, preprocess, tokenizer
    if model is None:
        print("Loading model...")
        m, _, p = open_clip.create_model_and_transforms('MobileCLIP2-S0', pretrained='dfndr2b')
        model = m.to("cpu").eval()
        preprocess = p
        tokenizer = open_clip.get_tokenizer('MobileCLIP2-S0')
        print("Model Loaded!")

def _classify_image(img: Image.Image, keyword: str):
    _ensure_model()
    start = time.time()

    img = img.convert("RGB")
    img.thumbnail((224, 224))
    image_input = preprocess(img).unsqueeze(0).to("cpu")

    with torch.inference_mode():
        image_feat = model.encode_image(image_input)
        image_feat /= image_feat.norm(dim=-1, keepdim=True)

        text_tokens = tokenizer([f"a photo of a {keyword}", "object"]).to("cpu")
        text_feat = model.encode_text(text_tokens)
        text_feat /= text_feat.norm(dim=-1, keepdim=True)

        raw_sim = float(image_feat @ text_feat[0].T)
        probs = (100.0 * image_feat @ text_feat.T).softmax(dim=-1).cpu().numpy()[0]

    verified = bool(probs.argmax() == 0 and probs[0] >= 0.55 and raw_sim > 0.15)
    print(f"Done in {time.time()-start:.2f}s")
    return {"verified": verified, "score": float(probs[0]), "raw_sim": raw_sim}

@app.get("/health")
def health():
    return {"status": "ok"}

@app.post("/classify")
def classify(keyword: str = Form(...), file: UploadFile = File(...)):
    img = Image.open(io.BytesIO(file.file.read()))
    return _classify_image(img, keyword)

class PredictRequest(BaseModel):
    keyword: str
    image_base64: str

@app.post("/predict")
def predict(req: PredictRequest):
    try:
        image_bytes = base64.b64decode(req.image_base64)
        img = Image.open(io.BytesIO(image_bytes))
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid image_base64")
    return _classify_image(img, req.keyword)

if __name__ == "__main__":
    import uvicorn
    import os
    port = int(os.environ.get("PORT", 8080))
    print(f"Starting server on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)

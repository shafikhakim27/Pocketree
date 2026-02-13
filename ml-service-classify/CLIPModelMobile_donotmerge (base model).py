import base64
import io, torch, time
from fastapi import FastAPI, Form, UploadFile, File
from pydantic import BaseModel
from PIL import Image
import open_clip

app = FastAPI()

# Global placeholders - no loading at startup
model, preprocess, tokenizer = None, None, None

class PredictInstance(BaseModel):
    keyword: str
    image_base64: str

class PredictRequest(BaseModel):
    instances: list[PredictInstance]

def classify_image(keyword: str, image_bytes: bytes) -> bool:
    global model, preprocess, tokenizer
    start = time.time()

    if model is None:
        print("Loading model...")
        m, _, p = open_clip.create_model_and_transforms('MobileCLIP2-S0', pretrained='dfndr2b')
        model = m.to("cpu").eval()
        preprocess = p
        tokenizer = open_clip.get_tokenizer('MobileCLIP2-S0')
        print("Model Loaded!")

    img = Image.open(io.BytesIO(image_bytes)).convert("RGB")
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
    return verified

@app.get("/health")
def health():
    return {"status": "ok"}

@app.post("/classify")
def classify(keyword: str = Form(...), file: UploadFile = File(...)):
    verified = classify_image(keyword, file.file.read())
    return {"verified": verified}

@app.post("/predict")
def predict(request: PredictRequest):
    predictions = []
    for inst in request.instances:
        image_bytes = base64.b64decode(inst.image_base64)
        verified = classify_image(inst.keyword, image_bytes)
        predictions.append({"verified": verified})
    return {"predictions": predictions}

if __name__ == "__main__":
    import uvicorn
    import os
    # Get the port from environment variable (Cloud Run provides this)
    # Default to 8080 if not found
    port = int(os.environ.get("PORT", 8080))
    print(f"🚀 Starting server on port {port}")
    uvicorn.run(app, host="0.0.0.0", port=port)

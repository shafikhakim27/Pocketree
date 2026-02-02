from fastapi import FastAPI, File, UploadFile, Form
from pydantic import BaseModel
from typing import List
import torch
from PIL import Image
import io
from transformers import CLIPProcessor, CLIPModel

app = FastAPI()

# --- Load CLIP on MacBook GPU (MPS) ---
model_name = "openai/clip-vit-base-patch32"
model = CLIPModel.from_pretrained(model_name)
processor = CLIPProcessor.from_pretrained(model_name)
device = "mps" if torch.backends.mps.is_available() else "cpu"
model.to(device)

# --- 1. IMAGE VERIFICATION ---
# Matches: MlService:Url -> "http://127.0.0.1:5000/classify"
@app.post("/classify")
async def classify(keyword: str = Form(...), file: UploadFile = File(...)):
    contents = await file.read()
    img = Image.open(io.BytesIO(contents)).convert("RGB")

    # TRANSFORM THE KEYWORD HERE
    # Even if C# sends "bottle", Python turns it into a descriptive prompt
    ai_prompt = f"a {keyword}"
    
    labels = [ai_prompt, "a blurry background", "a random object"]
    
    inputs = processor(text=labels, images=img, return_tensors="pt", padding=True).to(device)
    
    with torch.no_grad():
        outputs = model(**inputs)
    
    probs = outputs.logits_per_image.softmax(dim=1).cpu().numpy()[0]
    
    # --- DEBUG: Print scores to your Mac Terminal ---
    print(f"\n--- New Verification Request: {keyword} ---")
    for label, prob in zip(labels, probs):
        print(f"Label: {label: <30} Confidence: {prob*100:.2f}%")

    best_idx = probs.argmax()
    max_prob = probs[best_idx]
    
    verified = bool(best_idx.item() == 0 and max_prob.item() >= 0.80)
    print(f"VERIFIED: {verified}")
    
    return {"Verified": verified}

if __name__ == "__main__":
    import uvicorn
    # Port 5000 to match your appsettings and Program.cs
    uvicorn.run(app, host="0.0.0.0", port=5000)
from fastapi import FastAPI, UploadFile, File, Form
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Dict, Any
import io
import torch
import numpy as np
from PIL import Image
from transformers import CLIPProcessor, CLIPModel

app = FastAPI()

# --- Load CLIP Model ---
model_name = "openai/clip-vit-base-patch32"
model = CLIPModel.from_pretrained(model_name)
processor = CLIPProcessor.from_pretrained(model_name)
# Use CUDA if available, otherwise CPU (works in Docker)
device = "cuda" if torch.cuda.is_available() else "cpu"
model.to(device)

# Enable CORS so ASP.NET API can call this
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Data models
class UserPreference(BaseModel):
    PreferredCategory: str
    PreferredDifficulty: str

class TaskData(BaseModel):
    TaskID: int
    Description: str
    Difficulty: str
    CoinReward: int
    RequiresEvidence: bool
    Keyword: str
    Category: str

class PredictRequest(BaseModel):
    preferences: List[UserPreference]
    totalScore: int
    tasks: List[TaskData]

@app.get("/")
def home():
    """Health check endpoint"""
    return {
        "status": "ML Service is running",
        "version": "1.0.0",
        "endpoints": {
            "classify": "/classify (POST) - Verify task photos",
            "predict": "/predict (POST) - Recommend tasks"
        }
    }

@app.post("/classify")
async def classify(keyword: str = Form(...), file: UploadFile = File(...)):
    """
    Verify task photo matches keyword using CLIP model
    
    Expected from ASP.NET API:
    - keyword: string to match (e.g., "tree", "bottle", "compost")
    - file: uploaded image (multipart/form-data)
    
    Returns:
    - Verified: boolean (True if image matches keyword with confidence >= 80%)
    """
    try:
        # Read uploaded image
        contents = await file.read()
        img = Image.open(io.BytesIO(contents)).convert("RGB")
        
        # Transform keyword into descriptive prompt
        ai_prompt = f"a {keyword}"
        labels = [ai_prompt, "a blurry background", "a random object"]
        
        # Run CLIP inference
        inputs = processor(text=labels, images=img, return_tensors="pt", padding=True).to(device)
        
        with torch.no_grad():
            outputs = model(**inputs)
        
        probs = outputs.logits_per_image.softmax(dim=1).cpu().numpy()[0]
        
        # Debug output
        print(f"\n--- Verification Request: {keyword} ---")
        for label, prob in zip(labels, probs):
            print(f"Label: {label: <30} Confidence: {prob*100:.2f}%")
        
        best_idx = probs.argmax()
        max_prob = probs[best_idx]
        
        verified = bool(best_idx == 0 and max_prob >= 0.80)
        print(f"VERIFIED: {verified}\n")
        
        return {"Verified": verified}
        
    except Exception as e:
        print(f"❌ Classification error: {str(e)}")
        return {"Verified": False, "error": str(e)}

@app.post("/predict")
async def predict(data: PredictRequest):
    """
    Recommend personalized tasks based on user preferences
    
    Expected from ASP.NET API:
    {
      "preferences": [{"PreferredCategory": "Recycling", "PreferredDifficulty": "Easy"}],
      "totalScore": 350,
      "tasks": [{"TaskID": 1, "Description": "...", ...}, ...]
    }
    
    Returns:
    [5, 3, 1]  // List of task IDs ranked by relevance
    """
    try:
        preferences = data.preferences
        total_score = data.totalScore
        tasks = data.tasks
        
        # TODO: Replace with your recommendation model
        # from CLIPModel_donotmerge import recommend_tasks
        # ranked_ids = recommend_tasks(preferences, total_score, tasks)
        
        # For now, mock response - return first 3 tasks
        # This allows GetDailyTasksApi to work with random-ish selection
        ranked_ids = [task.TaskID for task in tasks[:3]] if tasks else [1, 2, 3]
        
        print(f"🎯 Task recommendation requested: user_score={total_score}, recommended={ranked_ids}")
        
        return ranked_ids
        
    except Exception as e:
        print(f"❌ Prediction error: {str(e)}")
        return [1, 2, 3]  # Fallback to first 3 tasks

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5000)
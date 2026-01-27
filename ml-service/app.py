from fastapi import FastAPI
from pydantic import BaseModel

app = FastAPI()

class TreeData(BaseModel):
    height: float
    leaf_type: str

@app.get("/")
def home():
    return {"status": "ML Service is running"}

@app.post("/predict")
def predict(data: TreeData):
    # YOUR ML LOGIC GOES HERE
    # result = model.predict(...)
    return {"prediction": "Healthy Oak", "confidence": 0.95}

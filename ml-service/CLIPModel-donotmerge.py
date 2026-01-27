import mysql.connector
from flask import Flask, request, jsonify
import torch
from PIL import Image
import io
from transformers import CLIPProcessor, CLIPModel

app = Flask(__name__)

# 1. Load CLIP once on your MacBook's GPU (MPS)
model = CLIPModel.from_pretrained("openai/clip-vit-base-patch32")
processor = CLIPProcessor.from_pretrained("openai/clip-vit-base-patch32")
device = "mps" if torch.backends.mps.is_available() else "cpu"
model.to(device)

# Add this above your @app.route('/predict')
@app.route('/')
def home():
    return "<h1>Flask AI Server is Running!</h1><p>Ready for Android connections.</p>"

@app.route('/predict', methods=['POST'])
def predict():
    file = request.files.get('image')
    # Receive the keyword from Android
    keyword = request.form.get('keyword', 'object') 

    img = Image.open(io.BytesIO(file.read()))

    # Dynamic labels based on the DB keyword
    labels = [f"a {keyword}", "background", "something else"]
    
    inputs = processor(text=labels, images=img, return_tensors="pt", padding=True).to(device)
    
    with torch.no_grad():
        outputs = model(**inputs)
    
    probs = outputs.logits_per_image.softmax(dim=1)
    max_prob, best_idx = torch.max(probs, dim=1)
    
    # Verify only if the top match is the keyword and confidence is high
    if best_idx.item() == 0 and max_prob.item() >= 0.85:
        return jsonify({"status": "verified"})
    else:
        return jsonify({"status": "non-verified"})

@app.route('/get-task', methods=['GET'])
def get_task():
    try:
        # Use the full name here: mysql.connector.connect
        conn = mysql.connector.connect(
            host="localhost",
            user="admin",
            password="frescyliafrida",
            database="game"
        )
        cursor = conn.cursor(dictionary=True)
        cursor.execute("SELECT keyword FROM active_tasks ORDER BY RAND() LIMIT 1")
        result = cursor.fetchone()
        
        cursor.close()
        conn.close()

        if result:
            return jsonify(result) 
        return jsonify({"error": "No tasks found"}), 404
    except Exception as e:
        print(f"Error: {e}")
        return jsonify({"error": str(e)}), 500
    
@app.route('/update-points', methods=['POST'])
def update_points():
    try:
        # Get data from the Android request
        data = request.get_json()
        user_id = data.get('user_id')

        conn = mysql.connector.connect(
            host="localhost",
            user="admin",
            password="frescyliafrida",
            database="game"
        )
        cursor = conn.cursor()
        
        # Update the points
        query = "UPDATE users SET points = points + 10 WHERE id = %s"
        cursor.execute(query, (user_id,))
        conn.commit()
        
        cursor.close()
        conn.close()
        
        return jsonify({"status": "success", "message": "Points added!"}), 200
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500

if __name__ == '__main__':
    # host='0.0.0.0' allows your Android phone to connect via your laptop's IP
    app.run(host='0.0.0.0', port=5000)

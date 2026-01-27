# 1. Use a slightly more robust base image for AI libraries
FROM python:3.9-slim

# 2. Set working directory
WORKDIR /app

# 3. Install system dependencies required for PyTorch/Pillow/CV
# (libgl1 and libglib2.0 are often needed for image processing libraries)
RUN apt-get update && apt-get install -y \
    build-essential \
    libgl1-mesa-glx \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

# 4. Copy requirements first (for caching)
COPY requirements.txt .

# 5. Install dependencies
# We increase the timeout because PyTorch/Transformers are HUGE downloads
RUN pip install --no-cache-dir --default-timeout=100 -r requirements.txt

# 6. Copy the rest of the code
COPY . .

# 7. EXPOSE ports for both FastAPI (8000) and Flask (5000) just in case
EXPOSE 8000
EXPOSE 5000

# 8. DEFAULT START COMMAND
# Currently set to run the 'app-donotdelete.py' (FastAPI).
# If you want to run the CLIP Model instead, change this line to:
# CMD ["python", "CLIPModel-donotmerge.py"]
CMD ["uvicorn", "app-donotdelete:app", "--host", "0.0.0.0", "--port", "8000"]
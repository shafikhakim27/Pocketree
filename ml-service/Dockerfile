FROM nvidia/cuda:12.1.1-runtime-ubuntu22.04

RUN apt-get update && apt-get install -y python3.10 python3-pip libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
# GPU build: use CUDA wheels
RUN pip3 install --no-cache-dir -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cu121

COPY . .

# Pre-download weights
RUN python3 -c "import open_clip; open_clip.create_model_and_transforms('MobileCLIP2-S0', pretrained='dfndr2b')"

CMD ["python3", "CLIPModelMobile_donotmerge.py"]

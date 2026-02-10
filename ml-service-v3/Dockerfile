FROM python:3.10-slim

WORKDIR /app

RUN apt-get update && apt-get install -y \
    build-essential git curl \
    libgl1 libglib2.0-0 \
    libffi-dev libssl-dev \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
COPY pip.conf /etc/pip.conf

RUN python -m pip install --upgrade pip setuptools wheel

RUN python -m pip install --no-cache-dir -r requirements.txt

COPY . .

RUN python -c "import open_clip; open_clip.create_model_and_transforms('MobileCLIP2-S0', pretrained='dfndr2b')"

CMD ["python","-m","uvicorn","MainModelML:app","--host","0.0.0.0","--port","8080"]

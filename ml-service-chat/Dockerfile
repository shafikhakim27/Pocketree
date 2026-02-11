FROM python:3.10-slim

WORKDIR /app

ENV GPT4ALL_BACKEND=cpu \
    OMP_NUM_THREADS=1 \
    MKL_NUM_THREADS=1 \
    OPENBLAS_NUM_THREADS=1 \
    NUMEXPR_NUM_THREADS=1 \
    TOKENIZERS_PARALLELISM=false \
    PYTHONUNBUFFERED=1

RUN apt-get update && apt-get install -y \
    build-essential git curl \
    libgl1 libglib2.0-0 \
    libffi-dev libssl-dev \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .

RUN python -m pip install --upgrade pip setuptools wheel

RUN python -m pip install --no-cache-dir -r requirements.txt

COPY . .

CMD ["python","-m","uvicorn","MainModelML_chatbot:app","--host","0.0.0.0","--port","8080"]

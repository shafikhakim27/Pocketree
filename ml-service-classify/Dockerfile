FROM python:3.10-slim

WORKDIR /app

ENV OMP_NUM_THREADS=1 \
    MKL_NUM_THREADS=1 \
    OPENBLAS_NUM_THREADS=1 \
    NUMEXPR_NUM_THREADS=1 \
    TOKENIZERS_PARALLELISM=false \
    TRANSFORMERS_VERBOSITY=error \
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

CMD ["sh","-c","python -m uvicorn MainModelML_pic:app --host 0.0.0.0 --port ${PORT:-8080}"]


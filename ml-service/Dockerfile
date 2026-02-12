FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    libgomp1 \
    libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN python -m pip install --upgrade pip setuptools wheel && \
    python -m pip install -r requirements.txt

COPY . .
RUN mkdir -p /app/models

CMD ["sh", "-c", "python -m uvicorn MainModelML:app --host 0.0.0.0 --port ${PORT:-8080} --timeout-keep-alive 120 --timeout-graceful-shutdown 120"]

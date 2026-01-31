# 1. Base Image
FROM python:3.10-slim

# 2. Set Environment Variables
# Prevents Python from writing pyc files and buffering stdout/stderr
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV PORT=5000

WORKDIR /app

# 3. Install System Dependencies
# libgl1 and libglib2.0 are required for OpenCV/ML libraries
RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    curl \
    && rm -rf /var/lib/apt/lists/*

# 4. Install Python Dependencies
COPY ml-service/requirements.txt .
RUN pip install --no-cache-dir --default-timeout=100 -r requirements.txt

# 5. Copy ML source code
COPY ml-service/ .

# 6. Expose the port
EXPOSE 5000

# 7. Start the application
# Using uvicorn with the app instance inside app_donotdelete.py
CMD ["sh", "-c", "uvicorn app_donotdelete:app --host 0.0.0.0 --port ${PORT}"]
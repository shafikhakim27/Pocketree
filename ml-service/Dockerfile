# 1. You MUST start with a base image
FROM python:3.8-slim

# 2. Set the working directory inside the container
WORKDIR /app

# 3. Copy requirements (This works because you moved the file into ml-service folder)
COPY requirements.txt .

# 4. Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# 5. Copy the rest of your Python code
COPY . .

# 6. Run the application (Adjust 'main:app' if your file is named something else)
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
# ... (python setup)
WORKDIR /app
COPY requirements.txt .    # It finds it because it's right next to the Dockerfile
RUN pip install -r requirements.txt
COPY . .                   # It copies only the python code, nothing else
# ... (cmd)
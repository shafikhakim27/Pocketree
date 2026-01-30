# 🌳 Pocketree - Development Branch

![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/backend-api.yml/badge.svg)
![ML Ops Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-ops.yml/badge.svg)
![Android Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/android.yml/badge.svg)

Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits.
This repository operates as a **Monorepo**, containing the Backend API, Android Mobile App, Python ML Service, and the necessary DevOps infrastructure, orchestrating the Mobile App, Backend API, and AI Service into a unified ecosystem.

## 🏗️ Architecture & Tech Stack

The system is composed of three primary services orchestrated via Docker.

* **📱 Frontend:** Native Android (Kotlin / Jetpack Compose)
* **⚙️ Backend:** ASP.NET Core Web API 9.0 (C#)
* **🧠 Machine Learning:** Python Service (Flask/FastAPI)
* **🗄️ Database:** MySQL 8.0
* **🐳 Infrastructure:** Docker Compose

---

## 💻 Prerequisites

1. Git & Docker Desktop (Essential)

2. .NET 9.0 SDK & Visual Studio 2022

3. Android Studio (Ladybug or newer)

4. Maestro CLI (For UI Testing) - curl -fsSL https://get.maestro.mobile.dev | bash



---

## 📂 Repository Structure

```
Pocketree/
├── .github/workflows/        # 🤖 CI/CD Pipelines (GitHub Actions)
├── android/                  # 📱 Android Source Code (Kotlin)
│   └── .maestro/             #    └── UI Automation Tests
├── api/                      # ⚙️ Backend Source Code (.NET 9.0)
│   ├── Pocketree.Api/        #    └── Main Web Api
│   └── Pocketree.Api.Tests/  #    └── Integration & Unit Tests
├── ml-service/               # 🧠 Python Machine Learning Service
├── docker-compose.yml        # 🐳 Orchestration for API, DB, and ML
└── README.md                 # Local Infra Blueprint

```

---

## 🔄 Git Workflow

We utilize a simplified **Git Flow** strategy:

* **`develop`**: The main working branch. All features are merged here.
* **`main`**: The stable production release. Merging here triggers deployment.

### **How to Contribute**

1. **Pull Latest Changes:**
```bash
git checkout develop
git pull origin develop

```

2. **Commit Your Work:**
```bash
git add .
git commit -m "feat: added login screen"
git push origin develop

```


---

## 🚀 CI/CD Pipelines (GitHub Actions)

We have implemented fully automated pipelines to ensure code quality and security.

### **1. 📱 Android Pipeline (`android.yml`)**

* **Trigger:** Pushes to `android/**`
* **Build:** Compiles the APK (Debug).
* **Unit Tests:** Runs JUnit tests via Gradle.
* **UI Automation:** Uses **Maestro** to spin up an emulator and simulate user interactions (Login, Navigation).
* **Artifacts:** Uploads the `.apk` file to GitHub Actions for download.

### **2. ☁️ Backend API Pipeline (`backend-api.yml`)**

* **Trigger:** Pushes to `api/**`
* **Optimization:** Uses Smart Tagging (SHA & Branch) and Docker Layer Caching.
* **Security:** Snyk Security: Scans NuGet packages for vulnerabilities. Runs **Trivy** to scan the container for vulnerabilities (High/Critical). Trivy: Scans the final Docker image for OS-level threats.
* **Docker Build:** Builds the production container image. 
* **Deploy:** Pushes to Docker Hub & deploys to **Azure Web App** (Production).

### **3. 🧠 ML Service (`ml-ops.yml`)**
Bandit: Performs security "SAST" scanning on Python logic.

Snyk: Checks Python dependencies (pip) for known exploits.

Deploy: Auto-deploys to Railway/Azure Container Apps.
---

## 🛠️ Docker - Local Development Setup

### **Step 1: Start Infrastructure (Docker)**

Instead of installing MySQL manually, we use Docker to spin up the Database, API, and ML Service together.

```bash
# In the root folder:
docker-compose up --build

```

* **API:** `http://localhost:8080` (Health Check: `/api/health/live`)
* **ML Service:** `http://localhost:5000`
* **Database:** `localhost:3306`

### **Step 2: Backend Development (.NET)**

If you want to run the API *outside* of Docker for debugging:

1. Open `api/Pocketree.Api/appsettings.json` and ensure the connection string points to `localhost`.
2. Run the app:
```bash
cd api/Pocketree.Api
dotnet run

```

3. **Scalar UI:** http://localhost:5042/scalar/v1
   **Swagger Docs:** http://localhost:5042/swagger


### **Step 3: Frontend Development (Android)**

1. Open **Android Studio**.
2. **Crucial:** Select **Open** and choose the `Pocketree/android` subfolder.
3. Wait for Gradle Sync.
4. Run on Emulator (configured to talk to `http://10.0.2.2:8080` for Docker API).

### **Step 4: Running UI Tests Locally (Maestro)**

To run the robot tests on your machine:

```bash
# Ensure emulator is running, then:
maestro test android/.maestro/flow.yaml

```
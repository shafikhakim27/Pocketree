# 🌳 Pocketree - Development Branch

![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/api.yml/badge.svg)
![ML Ops Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-ops.yml/badge.svg)
![Android Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/android.yml/badge.svg)

Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits.
This repository operates as a **Monorepo**, containing the Backend API, Android Mobile App, Python ML Service, and the necessary DevOps infrastructure, orchestrating the Mobile App, Backend API, and Machine Learning components.

## 🏗️ Architecture & Tech Stack

The system is composed of three primary services orchestrated via Docker.

* **📱 Frontend:** Native Android (Kotlin / Jetpack Compose)
* **⚙️ Backend:** ASP.NET Core Web API 9.0 (C#)
* **🧠 Machine Learning:** Python Service (Flask/FastAPI)
* **🗄️ Database:** MySQL 8.0
* **🐳 Infrastructure:** Docker Compose
* **🤖 CI/CD:** GitHub Actions with automated testing and deployment

---

## 💻 Prerequisites

1. Git & Docker Desktop (Essential)

2. .NET 9.0 SDK & Visual Studio 2022

3. Android Studio (Ladybug or newer)

4. Maestro CLI (For UI Testing) - `curl -fsSL https://get.maestro.mobile.dev | bash`

---

## 📂 Repository Structure

```
Pocketree/
├── .github/workflows/        # 🤖 CI/CD Pipelines (GitHub Actions)
│   ├── api.yml              #    └── Backend API CI/CD
│   ├── android.yml          #    └── Android CI/CD
│   └── ml-ops.yml           #    └── ML Service CI/CD
├── android/                  # 📱 Android Source Code (Kotlin)
│   └── .maestro/             #    └── UI Automation Tests
├── api/                      # ⚙️ Backend Source Code (.NET 9.0)
│   ├── Pocketree.Api/        #    └── Main Web Api
│   └── Pocketree.Api.Tests/  #    └── Integration & Unit Tests
├── ml-service/               # 🧠 Python Machine Learning Service
├── docker-compose.yml        # 🐳 Orchestration for API, DB, and ML
└── README.md                 # Project Documentation
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

We have implemented fully automated pipelines to ensure code quality, security, and deployment. All pipelines run automatically when you push changes to the relevant directories.

### **1. 📱 Android Pipeline (`android.yml`)**

**Trigger Conditions:**
* Pushes to `main` or `develop` branches
* Changes in `android/**` directory
* Changes to `.github/workflows/android.yml`

**Pipeline Jobs:**

#### **Job 1: Build & Unit Test**
* **Checkout:** Clones the repository
* **JDK Setup:** Installs JDK 21 (Temurin distribution)
* **Gradle Cache:** Caches Gradle dependencies for faster builds
* **Unit Tests:** Runs `./gradlew testDebugUnitTest`
* **Build APK:** Compiles the debug APK using `./gradlew assembleDebug`
* **Upload Artifact:** Stores the APK for the next job and manual download

#### **Job 2: UI Automation Testing**
* **Download APK:** Retrieves the built APK from Job 1
* **Emulator Setup:** Spins up Android Emulator (API 29, Nexus 6)
* **Maestro Installation:** Installs Maestro CLI for UI testing
* **Run Tests:** Executes Maestro flows from `android/.maestro/flow.yaml`
  * Generates JUnit XML report
  * Records MP4 video of test execution
* **Test Summary:** Publishes test results to GitHub Actions summary

**How to Run Manually:**
```bash
# Trigger workflow manually
# Go to: Actions → 📱 Android CI/CD → Run workflow

# Or run locally:
cd android
./gradlew testDebugUnitTest  # Unit tests
./gradlew assembleDebug       # Build APK
maestro test .maestro/flow.yaml  # UI tests (requires emulator)
```

**Artifacts Generated:**
* `pocketree-debug.apk` - Available for download in Actions tab
* Test reports in JUnit XML format
* UI test recordings (MP4)

---

### **2. ☁️ Backend API Pipeline (`api.yml`)**

**Trigger Conditions:**
* Pushes to `main` or `develop` branches
* Changes in `api/**` directory
* Changes to `.github/workflows/api.yml`
* Manual workflow dispatch

**Pipeline Steps:**

1. **Environment Setup**
   * Checkout code
   * Setup .NET 9.0 SDK
   * Restore NuGet dependencies

2. **🛡️ Security Scanning**
   * **Snyk Scan:** Analyzes NuGet packages for vulnerabilities
     * Severity threshold: High
     * Scans `api/api.sln`
   * **Trivy Scan:** Scans Docker image for OS-level vulnerabilities
     * Severity: Critical only
     * Ignores unfixed vulnerabilities

3. **🧪 Testing**
   * Runs all tests in `Pocketree.Api.Tests` project
   * Uses `dotnet test` with verbose output
   * Fails pipeline if tests fail

4. **🐳 Docker Build & Push**
   * Builds multi-platform Docker image
   * Tags with branch name and commit SHA
   * Uses GitHub Actions cache for faster builds
   * Pushes to Docker Hub: `{DOCKER_USER}/pocketree-api`

5. **🚀 Deployment**
   * Deploys to Azure Web App: `pocketree-api`
   * Uses SHA-tagged image for deterministic deployment
   * Automatically updates production environment

**How to Run Manually:**
```bash
# Trigger workflow manually
# Go to: Actions → ☁️ Backend API CI/CD → Run workflow

# Or run locally:
cd api
dotnet restore api.sln
dotnet test Pocketree.Api.Tests/Pocketree.Api.Tests.csproj
dotnet build api.sln
dotnet run --project Pocketree.Api/Pocketree.Api.csproj

# Docker build locally:
docker build -f api/Dockerfile -t pocketree-api .
docker run -p 8080:8080 pocketree-api
```

**Required Secrets:**
* `DOCKER_USER` - Docker Hub username
* `DOCKER_PASS` - Docker Hub password/token
* `SNYK_TOKEN` - Snyk API token
* `AZURE_CREDENTIALS` - Azure service principal credentials

---

### **3. 🧠 ML Service Pipeline (`ml-ops.yml`)**

**Trigger Conditions:**
* Pushes to `main` or `develop` branches
* Changes in `ml-service/**` directory
* Changes to `.github/workflows/ml-ops.yml`
* Manual workflow dispatch

**Pipeline Steps:**

1. **Environment Setup**
   * Checkout code
   * Prepare Python environment

2. **🛡️ Security Scanning**
   * **Snyk Python Scan:** Analyzes `requirements.txt`
     * Severity threshold: High
     * Skips unresolved dependencies
   * **Bandit SAST:** Static security analysis on Python code
     * Confidence level: High (lll)
     * Severity level: Medium and above (iii)

3. **🐳 Docker Build & Push**
   * Builds ML service Docker image
   * Tags with commit SHA
   * Uses GitHub Actions cache
   * Pushes to Docker Hub: `{DOCKER_USER}/pocketree-ml`

4. **🚀 Deployment**
   * Deploys to Azure Web App for Containers: `pocketree-ml-service`
   * Uses SHA-tagged image

**How to Run Manually:**
```bash
# Trigger workflow manually
# Go to: Actions → 🧠 ML Service Ops → Run workflow

# Or run locally:
cd ml-service
pip install -r requirements.txt
bandit -r . -lll -iii  # Security scan

# Docker build locally:
docker build -f ml-service/Dockerfile -t pocketree-ml .
docker run -p 5000:5000 pocketree-ml
```

**Required Secrets:**
* `DOCKER_USER` - Docker Hub username
* `DOCKER_PASS` - Docker Hub password/token
* `SNYK_TOKEN` - Snyk API token
* `AZURE_CREDENTIALS` - Azure service principal credentials

---

## 🧪 Testing Guide

### **Running Tests Locally**

#### **Android Tests**
```bash
cd android

# Unit tests only
./gradlew testDebugUnitTest

# Build without tests
./gradlew assembleDebug

# UI tests with Maestro (requires running emulator)
maestro test .maestro/flow.yaml

# Record UI tests
maestro record .maestro/flow.yaml output.mp4
```

#### **Backend API Tests**
```bash
cd api

# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test Pocketree.Api.Tests/Pocketree.Api.Tests.csproj --verbosity detailed
```

#### **ML Service Tests**
```bash
cd ml-service

# Install dependencies
pip install -r requirements.txt

# Run tests (if pytest is configured)
pytest

# Security scans
bandit -r . -lll -iii
```

### **Viewing Test Results in GitHub Actions**

1. Navigate to the **Actions** tab in your repository
2. Select the workflow run you want to inspect
3. View test summaries in the workflow summary page
4. Download artifacts (APKs, test reports) from the artifacts section
5. Android UI tests include video recordings of the test execution

---

## 🛠️ Docker - Local Development Setup

### **Step 1: Start Infrastructure (Docker)**

Instead of installing MySQL manually, we use Docker to spin up the Database, API, and ML Service together.

```bash
# In the root folder:
docker-compose up --build
```

**Services Available:**
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

3. **Access API Documentation:**
   * **Scalar UI:** http://localhost:5042/scalar/v1
   * **Swagger Docs:** http://localhost:5042/swagger

### **Step 3: Frontend Development (Android)**

1. Open **Android Studio**.
2. **Crucial:** Select **Open** and choose the `Pocketree/android` subfolder.
3. Wait for Gradle Sync.
4. Update API endpoint in your configuration to point to:
   * Local Docker: `http://10.0.2.2:8080` (Android Emulator)
   * Physical Device: `http://<your-local-ip>:8080`
5. Run on Emulator or Physical Device

### **Step 4: Running UI Tests Locally (Maestro)**

To run the automated UI tests on your machine:

```bash
# Ensure emulator is running, then:
cd android
maestro test .maestro/flow.yaml

# With recording:
maestro record .maestro/flow.yaml output.mp4
```

---

## 📊 Monitoring & Deployment

### **Deployment Targets**

* **Backend API:** Azure Web App - `pocketree-api`
* **ML Service:** Azure Web App for Containers - `pocketree-ml-service`
* **Docker Images:** Docker Hub - `{DOCKER_USER}/pocketree-api` and `{DOCKER_USER}/pocketree-ml`

### **Status Badges**

The badges at the top of this README show real-time status:
* ![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/api.yml/badge.svg)
* ![ML Ops Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-ops.yml/badge.svg)
* ![Android Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/android.yml/badge.svg)

### **Viewing Deployments**

1. Go to **Actions** tab
2. Select completed workflow run
3. Check deployment step for Azure deployment URL
4. View deployed application in Azure Portal

---

## 🔐 Security

All services undergo automated security scanning:
* **Snyk:** Dependency vulnerability scanning
* **Trivy:** Container image scanning
* **Bandit:** Python SAST analysis

Security scans run on every push and will fail the pipeline if critical vulnerabilities are found.

---
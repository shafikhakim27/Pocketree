# 🌳 Pocketree

Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits. 
This repository operates as a **Monorepo**, containing the Backend API, Android Mobile App, and the complete DevSecOps infrastructure (Docker & Jenkins).

## 🏗️ Architecture & Tech Stack
* **Backend:** ASP.NET Core Web API 8.0 (C#)
* **Frontend:** Native Android (Kotlin/Jetpack Compose)
* **Database:** MySQL 8.0 (Containerized)
* **Infrastructure:** Docker & Docker Compose
* **CI/CD:** Jenkins (Pipeline as Code)

---

## 💻 Prerequisites (Required Software)
Before you start, ensure you have the following installed on your machine:
1.  **Git** - [Download](https://git-scm.com/downloads)
2.  **Docker Desktop** (Must be running) - [Download](https://www.docker.com/products/docker-desktop/)
3.  **Visual Studio Code** (For Backend & DevOps) - [Download](https://code.visualstudio.com/)
    * *Extension Recommended:* C# Dev Kit
4.  **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
5.  **Android Studio** (For Frontend) - [Download](https://developer.android.com/studio)

---

## 📂 Repository Structure

Pocketree/
├── .github/                  # GitHub specific settings
├── .env                      # 🔐 Secrets & Ports (Created Manually - to Update at a later time)
├── android-app/              # 📱 Android Source Code (Kotlin)
├── ml-service/               # 🧠 Python Machine Learning API
├── src/                      # ⚙️ Backend Source Code (.NET)
│   ├── Pocketree.Api/        # The Main API Project
│   └── Pocketree.Api.Tests/  # Unit Tests
├── docker-compose.yml        # Orchestrates the Container Infrastructure
├── Dockerfile.jenkins        # Custom Jenkins Image Configuration
├── Jenkinsfile               # The CI/CD Pipeline Script
├── Pocketree.sln             # Visual Studio Solution File
└── README.md                 # This Documentation


---
## 🔄 Git Workflow (How We Collaborate)

### **1. Start a New Task**

```bash
# Update your local code first
git checkout develop
git pull origin develop
```

### **2. Save/Share Your Work**

```bash
git add .
git commit -m "Added login button and styled the header"
git push origin develop
```

### I will manage the commits from develop, to later merge into main when ready for production/deployment.

## 🚀 Step 1: Clone & Setup

1.  **Clone the Repository**
    Open your terminal/command prompt:

    ```bash
    git clone https://github.com/shafikhakim27/Pocketree.git
    cd Pocketree
    ```

2.  **Wake up the Infrastructure**
    We use Docker to spin up the Database and Jenkins automatically.

    ```bash
    docker-compose up -d
    ```

    * *This starts MySQL (Port 3307) and Jenkins (Port 8080).*

3.  **Verify Status**

    Run:
    ```bash
    docker ps
    ```

    * Ensure `pocketree-db` and `jenkins` are listed as "Up". (Docker Desktop)

---
## ⚙️ Step 2: Backend Development (.NET)

1.  Open the **root** `Pocketree` folder in VS Code.

2.  Open the integrated terminal (`Ctrl + ~`).

3.  Navigate to the API folder and run the app:

    ```bash
    cd src/Pocketree.Api
    dotnet run
    ```

4.  **Access the API:**

    * **Swagger Docs:** [http://localhost:5000/swagger](http://localhost:5000/swagger)
    * **API Root:** [http://localhost:5000](http://localhost:5000)

> **Important Database Note:**

> * **Local Development:** Your machine connects via **Port 3307**.

> * **Docker/Production:** The internal code uses **Port 3306**.

---
## 📱 Step 3: Frontend Development (Android)

1.  Open **Android Studio**.

2.  Select **File > Open**.

3.  **Crucial:** Navigate to the `Pocketree/android-app` subfolder and select that. (Do not open the root repo folder in Android Studio).

4.  Wait for the Gradle Sync to finish (bottom bar).

5.  Select your emulator or physical device and click the **Green Run Arrow (▶)**.

---

## 🛠️ Step 4: CI/CD Pipeline (Jenkins)

### We use Jenkins to automatically Build, Test, and Scan our code. 

### Since Jenkins runs inside Docker, every team member must set it up once on their own machine.

### **1. Access & Unlock Jenkins**

1.  Open [http://localhost:8080](http://localhost:8080) in your browser.

2.  It will ask for an **Administrator Password**.

3.  Open your terminal and run this command to reveal it:

    ```bash
    docker exec jenkins cat /var/jenkins_home/secrets/initialAdminPassword

    ```
    *(If that fails, use `docker ps` to check your container name).*

4.  Copy the password and paste it into Jenkins.

### **2. Install Plugins**

1.  Select **"Install suggested plugins"** and wait for it to finish.

2.  Create your Admin User account (e.g., `admin` / `password`).

3.  Once logged in, go to **Dashboard > Manage Jenkins > Plugins > Available Plugins**.

4.  Search for and install these specific plugins:

    * `Docker Pipeline` (the most important)

    * `MSTest` (for .NET test results)

5.  **Restart Jenkins** (tick the box "Restart Jenkins when installation is complete").

### **3. Create the Pipeline Job**

1.  On the Dashboard, click **+ New Item**.

2.  **Item Name:** `Pocketree-Pipeline`

3.  Select **Pipeline** and click **OK**.

4.  Scroll down to the **Pipeline** section:

    * **Definition:** `Pipeline script from SCM`

    * **SCM:** `Git`

    * **Repository URL:** `https://github.com/shafikhakim27/Pocketree.git`

    * **Branch Specifier:** `*/develop` 

    * **Script Path:** `Jenkinsfile` (Ensure it matches the file name in your repo)

5.  Click **Save**.

### **4. Run a Build**

1.  Click **Build Now** on the left menu.

2.  Wait for the build to appear in the **Build History** (bottom left).

3.  Click the **Blue Ball** (Success) or **Red Ball** (Fail) to see details.

4.  **View Logs:** Click the Build Number (#1) > **Console Output**.

# If Build fails, highlight, send me the error logs.
---

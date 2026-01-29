# 🌳 Pocketree - Development Branch

Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits.
This repository operates as a **Monorepo**, containing the Backend API, Android Mobile App, and the necessary development infrastructure.

## 🏗️ Architecture & Tech Stack

* **Backend:** ASP.NET Core Web API 9.0 (C#)
* **Frontend:** Native Android (Kotlin/Jetpack Compose)
* **Database:** MySQL 8.0

---

## 💻 Prerequisites (Required Software)

Before you start, ensure you have the following installed on your machine:

1. **Git** - [Download](https://git-scm.com/downloads)
2. **Visual Studio/VS Code** (For Backend) - [Download](https://code.visualstudio.com/)
* *Extension Recommended:* C# Dev Kit
3. **.NET 9.0 SDK** - [Download](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
4. **Android Studio** (For Frontend) - [Download](https://developer.android.com/studio)

---

## 📂 Repository Structure

```text
Pocketree/
├── .github/                  # GitHub specific settings
├── android-app/              # 📱 Android Source Code (Kotlin)
├── ml-service/               # 🧠 Python Machine Learning API
├── src/                      # ⚙️ Backend Source Code (.NET)
│   ├── Pocketree.Api/        # The Main API Project
│   └── Pocketree.Api.Tests/  # Unit Tests
|____ .gitignore
├── Pocketree.sln             # Visual Studio Solution File
└── README.md                 # This Documentation

```

---

## 🔄 Git Workflow (How We Collaborate)

### **1. Start a New Task**

Always pull the latest changes before starting work.

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

> **Note:** Commits to `develop` will be reviewed.

---

## 🚀 Step 1: Clone & Database Setup

1. **Clone the Repository**
Open your terminal/command prompt:
```bash
git clone https://github.com/shafikhakim27/Pocketree.git
cd Pocketree

```
---

## ⚙️ Step 2: Backend Development (.NET)

1. Open the **root** `Pocketree` folder in **VS Code**.
2. Open the integrated terminal (`Ctrl + ~`).
3. Navigate to the API folder and run the app:
```bash
cd src/Pocketree.Api
dotnet run

4. **Access the API:**
* **Swagger Docs:** [http://localhost:5042/swagger](https://www.google.com/search?q=http://localhost:5042/swagger)
* **API Root:** [http://localhost:5042](https://www.google.com/search?q=http://localhost:5042)

---

## 📱 Step 3: Frontend Development (Android)

1. Open **Android Studio**.
2. Select **File > Open**.
3. **Crucial:** Navigate to the `Pocketree/android-app` subfolder and select that. (Do not open the root repo folder in Android Studio).
4. Wait for the Gradle Sync to finish (bottom bar).
5. Select your emulator or physical device and click the **Green Run Arrow (▶)**.

> **Note for Emulator:** The Android app is configured to look for the API at `http://10.0.2.2:5042` (which is the emulator's way of talking to your localhost).
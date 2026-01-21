# 🌳 Pocketree - DevSecOps Monorepo

This repository contains the full stack for the Pocketree application:

* **Backend:** ASP.NET Core Web API (connected to MySQL).
* **Frontend:** Android (Kotlin).
* **Infrastructure:** Dockerized Database & Jenkins Pipeline.

## 🚀 Quick Start (Day-to-Day)

**1. Wake up the Infrastructure**
Before opening any code, start your local containers (Database & Jenkins).

```powershell
docker start pocketree-db pocketree-jenkins

```

**2. Verify they are running**

```powershell
docker ps

```

* *Check:* Ensure `pocketree-db` is up.
* *Check:* Ensure `pocketree-jenkins` is up at `http://localhost:8080`.

---

## 💻 How to Edit the Backend (.NET)

**1. Open the Project**

* Open **VS Code**.
* **File > Open Folder** > Select `Pocketree-Repo`.

**2. Run the API**
Open the integrated terminal in VS Code (`Ctrl + ~`) and run:

```powershell
cd src/Pocketree.Api
dotnet run

```

* **API URL:** `http://localhost:5000` (or the port shown in terminal).
* **Swagger Docs:** `http://localhost:5000/swagger`

**⚠️ Database Note:**

* Your machine connects via **Port 3307** (managed by your local `User Secrets`).
* The code in GitHub defaults to **Port 3306** (for teammates/Jenkins).
* *Do not change `appsettings.json` port numbers!*

---

## 📱 How to Edit the Frontend (Android)

**1. Open the Project**

* Open **Android Studio**.
* **File > Open** > Select `Pocketree-Repo/android-app`.
* *(Make sure to select the `android-app` subfolder, not the root repo!)*



**2. Run the App**

* Connect your phone or start an Emulator.
* Click the green **Run (▶)** button in the top toolbar.

---

## 🔄 The Git Workflow (How to Save)

**1. Start a New Task**
Always create a new branch for every feature (e.g., adding login, fixing bugs).

```powershell
# Make sure you are on develop first
git checkout develop
git pull origin develop

# Create your feature branch
git checkout -b feature/my-new-feature

```

**2. Save Your Work**
When you are done editing:

```powershell
git add .
git commit -m "Added login screen logic"

```

**3. Push to GitHub**

```powershell
git push -u origin feature/my-new-feature

```

---

## 🏗️ The Build Pipeline (Jenkins)

Once you push code to GitHub, you can trigger the automated build.

1. **Open Jenkins:** [http://localhost:8080](https://www.google.com/search?q=http://localhost:8080)
2. **Go to Project:** `Pocketree-Pipeline`
3. **Click:** `Build Now` (Left menu).
4. **Check Status:** Click the blinking ball or "Console Output" to see the build/test results.

---

## 🛠️ Troubleshooting

**"Port already in use" Error:**

* Check if you have another MySQL instance running.
* Run `docker ps` to see if `pocketree-db` is already using port 3307.

**"Jenkins build failed"**

* Check the console logs.
* Ensure your `Jenkinsfile` points to the correct branch (`*/main` or `*/develop`).

**"Android Studio can't find files"**

* Ensure you opened the `android-app` folder specifically, let Gradle sync finish (watch the bottom bar).

---

### **Repo Structure**

```text
Pocketree-Repo/
├── src/                    # .NET Backend
│   ├── Pocketree.Api/
│   └── Pocketree.Shared/
├── android-app/            # Android Frontend
├── Jenkinsfile             # Automation Script
└── README.md               # This file

```

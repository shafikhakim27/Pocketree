# 🌳 Pocketree
Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits.
This repository operates as a **Monorepo**, containing the Backend API with Machine Learning components, packaged into an Android Mobile App.

## 🏗️ Architecture & Tech Stack
The system is composed of three primary services orchestrated via Docker.
* **📱 Frontend:** Native Android (Kotlin / Jetpack Compose)
* **⚙️ Backend:** ASP.NET Core Web API 9.0 (C#)
* **🧠 Machine Learning:** Python Service (Flask/FastAPI)
* **🗄️ Database:** MySQL 8.0
* **🐳 Infrastructure:** Docker Compose
* **🤖 CI/CD:** GitHub Actions with automated testing and deployment

---

![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/api-ci.yml/badge.svg)
![ML Service Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-service-ci.yml/badge.svg)
![Android Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/android-ci.yml/badge.svg)
![Azure API Deployment](https://github.com/shafikhakim27/Pocketree/actions/workflows/api-cd.yml/badge.svg)

**Azure Deployment Status:**
- 🌐 **API**: [![Deploy to Azure](https://img.shields.io/badge/Azure-Deployed-blue?logo=microsoft-azure)](https://pocketree-api.azurewebsites.net)
- 🧠 **ML Service**: [![Deploy to Azure](https://img.shields.io/badge/Azure-Deployed-blue?logo=microsoft-azure)](https://pocketree-ml-service.azurewebsites.net)

---
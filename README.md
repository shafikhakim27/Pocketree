# 🌳 Pocketree

Welcome to **Pocketree**, a full-stack sustainability app designed for tracking and encouraging eco-friendly habits.
This repository contains source codes for Backend API with Machine Learning features, connecting to an Android Mobile App.

## 🏗️ Architecture & Tech Stack

The system is composed of three primary services orchestrated via Docker.

* **📱 Frontend:** Android (Kotlin)

* **⚙️ Backend:** ASP.NET Core Web API 9.0 (C#)

* **🧠 Machine Learning:** Python Service (Flask/FastAPI)

* **🗄️ Database:** MySQL 8.0

* **🤖 CI/CD:** GitHub Actions with automated build, testing and deployment.

---

**Build Status:**

![Android Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/android-ci.yml/badge.svg)

![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/api-ci.yml/badge.svg)

![ML Service Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-service-ci.yml/badge.svg)

![ML Service v2 Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-service-v2-ci.yml/badge.svg)

![ML Service v3 Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/ml-service-v3-ci.yml/badge.svg)



**Release Status:**

![Azure API Deployment](https://github.com/shafikhakim27/Pocketree/actions/workflows/api-cd.yml/badge.svg)

**Deployment Status:**

- **API**: [![Deploy to Azure](https://img.shields.io/badge/Azure-Deployed-blue?logo=microsoft-azure)](https://pocketree-api.azurewebsites.net)

- 🧠 **ML Service (Vertex AI / GPU)**: Endpoint `pocketree-ml-endpoint` ![Vertex AI](https://img.shields.io/badge/Vertex%20AI-GPU-blue?logo=google-cloud)

- **ML Service (Cloud Run / CPU)**: [![Deploy to GCP](https://img.shields.io/badge/GCP-Deployed-blue?logo=google-cloud)](https://pocketree-ml-500550710563.asia-southeast1.run.app)

- **ML Service (Azure App Service / CPU)**: [![Deploy to Azure](https://img.shields.io/badge/Azure-Deployed-blue?logo=microsoft-azure)](https://pocketree-ml-service.azurewebsites.net)

---

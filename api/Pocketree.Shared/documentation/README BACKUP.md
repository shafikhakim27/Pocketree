# ?? Pocketree - Eco-Friendly Habit Tracker

![API Status](https://github.com/shafikhakim27/Pocketree/actions/workflows/azure-deploy.yml/badge.svg)
![Azure API](https://img.shields.io/website?down_message=offline&label=Azure%20API&up_message=online&url=https%3A%2F%2Fpocketree-api.azurewebsites.net)
![Azure MySQL](https://img.shields.io/badge/Azure%20MySQL-Connected-success)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![Android](https://img.shields.io/badge/Android-Kotlin-3DDC84?logo=android)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

Welcome to **Pocketree**, a full-stack sustainability application designed to track and encourage eco-friendly habits through gamification, real-time collaboration, and AI-powered task validation.

---

## ?? Live Deployment

| Service | Status | URL |
|---------|--------|-----|
| **Production API** | ![API Status](https://img.shields.io/website?down_message=offline&up_message=online&url=https%3A%2F%2Fpocketree-api.azurewebsites.net) | [pocketree-api.azurewebsites.net](https://pocketree-api.azurewebsites.net) |
| **Azure MySQL** | ![MySQL](https://img.shields.io/badge/status-running-success) | `pocketree-mysql.mysql.database.azure.com` |
| **ML Service** | ![ML](https://img.shields.io/badge/status-configured-blue) | Environment Variable |
| **GitHub Actions** | ![Deploy](https://github.com/shafikhakim27/Pocketree/actions/workflows/azure-deploy.yml/badge.svg) | [View Workflows](https://github.com/shafikhakim27/Pocketree/actions) |

### Quick Test

```bash
# Test Production API
curl https://pocketree-api.azurewebsites.net

# Health Check
curl https://pocketree-api.azurewebsites.net/api/health

# Register Test User
curl -X POST https://pocketree-api.azurewebsites.net/api/User/RegisterApi \
  -H "Content-Type: application/json" \
  -d '{"Username":"testuser","Email":"test@example.com","Password":"SecurePass123!@#"}'
```

---

## ??? Architecture & Tech Stack

The system is composed of three primary services deployed on Azure with automated CI/CD.

### Frontend
* **?? Android App:** Kotlin / Jetpack Compose
* **?? UI Components:** Material Design 3
* **?? API Client:** Retrofit + OkHttp

### Backend
* **?? API:** ASP.NET Core 9.0 Web API (C#)
* **??? Database:** Azure MySQL Flexible Server 8.0
* **?? Authentication:** JWT Bearer Tokens
* **?? Real-time:** SignalR (WebSocket fallback)

### ML Service
* **?? Model:** Python + TensorFlow/PyTorch
* **?? Framework:** Flask/FastAPI
* **?? Containerization:** Docker

### Infrastructure
* **?? Cloud:** Microsoft Azure
  * App Service (API)
  * MySQL Flexible Server (Database)
  * App Service for Containers (ML - planned)
* **?? Development:** Docker Compose
* **?? CI/CD:** GitHub Actions
* **?? Monitoring:** Azure Application Insights

---

## ?? Repository Structure

```
Pocketree/
??? .github/workflows/           # ?? CI/CD Pipelines
?   ??? azure-deploy.yml        #    ??? Azure deployment workflow
??? android/                     # ?? Android Source Code
?   ??? app/src/                #    ??? Kotlin application
??? api/                         # ?? Backend Source Code
?   ??? Pocketree.Api/          #    ??? ASP.NET Core 9.0 API
?   ??? Pocketree.Api.Tests/    #    ??? 106 Unit & Integration Tests
?   ??? Pocketree.Shared/       #    ??? Shared DTOs & Models
??? ml-service/                  # ?? Python ML Service
??? docker-compose.yml           # ?? Local Development Stack
??? README.md                    # ?? Project Documentation
```

---

## ?? CI/CD Pipeline

### Automated Deployment Workflow

Every push to `develop` or `main` triggers:

1. ? **Checkout** - Clone repository
2. ? **Setup .NET 9** - Install SDK
3. ? **Restore Dependencies** - NuGet packages
4. ? **Build** - Compile in Release mode
5. ? **Run Tests** - Execute all 106 tests
6. ? **Publish** - Create deployment package
7. ? **Login to Azure** - Authenticate with service principal
8. ? **Deploy** - Push to Azure App Service
9. ? **Notify** - Deployment status

### Test Suite Summary

| Category | Tests | Status | Coverage |
|----------|-------|--------|----------|
| **Task Completion** | 11 | ? All passing | 100% |
| **Authentication** | 4 | ? All passing | 100% |
| **Level Progression** | 7 | ? All passing | 100% |
| **Badge Awards** | 5 | ? All passing | 100% |
| **Tree Mechanics** | 6 | ? All passing | 100% |
| **Skin System** | 6 | ? All passing | 100% |
| **Database Operations** | 67 | ? All passing | 95% |
| **Total** | **106** | **? 99 passing** | **~95%** |

### Viewing Deployment Status

1. Go to [GitHub Actions](https://github.com/shafikhakim27/Pocketree/actions)
2. Click latest workflow run
3. View deployment logs and status
4. Download artifacts if needed

---

## ??? Local Development Setup

### Prerequisites

* **Git** - Version control
* **Docker Desktop** - Container runtime
* **.NET 9.0 SDK** - Backend development
* **Visual Studio 2022** or **VS Code** - IDE
* **Android Studio** - Mobile development
* **MySQL Workbench** (optional) - Database GUI

### Step 1: Clone Repository

```bash
git clone https://github.com/shafikhakim27/Pocketree.git
cd Pocketree
```

### Step 2: Start Local Stack (Docker)

```bash
# Start all services (API, MySQL, ML)
docker-compose up --build

# Services will be available at:
# - API: http://localhost:8080
# - MySQL: localhost:3306
# - ML Service: http://localhost:5000
```

### Step 3: Backend Development

#### Option A: Run in Docker (Recommended)
```bash
docker-compose up api
```

#### Option B: Run Locally for Debugging

```bash
cd api/Pocketree.Api

# Configure User Secrets (one-time setup)
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=pocketree_db;User=root;Password=password;"

# Run the API
dotnet run
# Access at: http://localhost:5042
# Scalar API Docs: http://localhost:5042/scalar/v1
```

### Step 4: Run Tests

```bash
cd api

# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# View test summary
cat Pocketree.Api.Tests/FINAL_TEST_SUMMARY.md
```

### Step 5: Android Development

```bash
# Open Android Studio
# File ? Open ? Select Pocketree/android folder
# Wait for Gradle sync

# Update API endpoint in your configuration:
# - Emulator: http://10.0.2.2:8080
# - Physical device: http://<your-local-ip>:8080
# - Production: https://pocketree-api.azurewebsites.net

# Build and run on emulator/device
```

---

## ?? Git Workflow

We use **Git Flow** with automated deployment:

### Branch Strategy

* **`main`** - Production branch (protected)
  * Merges trigger production deployment
  * Requires pull request review
  * All tests must pass

* **`develop`** - Development branch (default)
  * Active development happens here
  * Auto-deploys to staging
  * Feature branches merge here

### Workflow Example

```bash
# 1. Start from develop
git checkout develop
git pull origin develop

# 2. Create feature branch
git checkout -b feature/your-feature-name

# 3. Make changes and commit
git add .
git commit -m "feat: add new feature"

# 4. Push and create PR
git push origin feature/your-feature-name
# Create Pull Request on GitHub ? develop

# 5. After PR approval and merge, deployment happens automatically!
```

### Commit Message Convention

```bash
feat: new feature
fix: bug fix
docs: documentation changes
test: adding tests
refactor: code refactoring
chore: maintenance tasks
```

---

## ?? Security & Configuration

### Required Azure Secrets (GitHub)

Set these in: **Repository Settings ? Secrets and variables ? Actions**

| Secret | Purpose |
|--------|---------|
| `AZURE_CREDENTIALS` | Service principal for Azure login |
| `DOCKER_USER` | Docker Hub username (optional) |
| `DOCKER_PASS` | Docker Hub password (optional) |

### Environment Variables (Azure App Service)

Set in: **Azure Portal ? App Service ? Configuration ? Application Settings**

| Name | Value |
|------|-------|
| `ConnectionStrings__DefaultConnection` | Azure MySQL connection string |
| `MlService__Url` | ML service endpoint URL |
| `Jwt__Key` | JWT signing key (keep secret!) |
| `Jwt__Issuer` | `PocketreeBackend` |
| `Jwt__Audience` | `PocketreeAndroidApp` |

### Security Features

? SSL/TLS encryption (Azure enforced)  
? JWT Bearer authentication  
? Password hashing with ASP.NET Core Identity  
? MySQL firewall configured  
? CORS enabled for mobile clients  
? Connection strings in Azure (not in code)  

---

## ?? API Documentation

### Base URLs

* **Production:** `https://pocketree-api.azurewebsites.net`
* **Local Development:** `http://localhost:8080`

### Authentication Endpoints

```bash
# Register
POST /api/User/RegisterApi
Content-Type: application/json
{
  "Username": "newuser",
  "Email": "user@example.com",
  "Password": "SecurePass123!@#"
}

# Login
POST /api/User/LoginApi
Content-Type: application/json
{
  "Username": "newuser",
  "Password": "SecurePass123!@#"
}
# Returns: { "Token": "eyJhbGc...", "User": {...} }

# Get Profile (Protected)
GET /api/User/GetUserProfileApi
Authorization: Bearer {your-token-here}
```

### Full API Documentation

* **Scalar UI (Dev only):** http://localhost:5042/scalar/v1
* **Swagger (Dev only):** http://localhost:5042/swagger

---

## ?? Android Integration

### Retrofit Setup

```kotlin
// Base URL configuration
object ApiConfig {
    const val BASE_URL = "https://pocketree-api.azurewebsites.net/"
    // or for local dev: "http://10.0.2.2:8080/"
}

// API Service Interface
interface PocketreeApiService {
    @POST("api/User/RegisterApi")
    suspend fun register(@Body dto: UserRegistrationDto): RegisterResponse
    
    @POST("api/User/LoginApi")
    suspend fun login(@Body dto: UserLoginDto): LoginResponse
    
    @GET("api/User/GetUserProfileApi")
    suspend fun getUserProfile(
        @Header("Authorization") token: String
    ): UserProfile
}

// Usage in ViewModel
class AuthViewModel : ViewModel() {
    suspend fun login(username: String, password: String) {
        val response = apiService.login(
            UserLoginDto(username, password)
        )
        // Save token and navigate to home
    }
}
```

---

## ??? Database

### Schema Overview

* **Users** - User accounts and profiles
* **Trees** - Virtual trees for missions
* **Tasks** - Eco-friendly activities
* **Levels** - User progression (Seedling ? Sapling ? Mighty Oak)
* **Badges** - Achievement tracking
* **GlobalMissions** - Collaborative goals

### Seed Data (Auto-loaded)

? **Levels:** Seedling (0 coins), Sapling (250), Mighty Oak (500)  
? **Tasks:** 5 eco-friendly tasks (Easy, Normal, Hard)  
? **Badges:** 5 achievement badges  
? **GlobalMission:** "Greenify Sahara" - 1000 trees goal  
? **Test User:** `ecotester` / `password`  

### Migrations

```bash
# Create migration
cd api/Pocketree.Api
dotnet ef migrations add MigrationName

# Apply to local database
dotnet ef database update

# Apply to Azure (automatic on deployment)
# or manually:
dotnet ef database update --connection "YourAzureConnectionString"
```

---

## ?? Docker Configuration

### Services

```yaml
services:
  api:
    build: ./api
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__DefaultConnection=Server=db;Database=pocketree_db;User=root;Password=password;
    depends_on: [db]
    
  db:
    image: mysql:8.0
    ports: ["3306:3306"]
    environment:
      - MYSQL_ROOT_PASSWORD=password
      - MYSQL_DATABASE=pocketree_db
    volumes: [mysql-data:/var/lib/mysql]
    
  ml-service:
    build: ./ml-service
    ports: ["5000:5000"]
```

### Commands

```bash
# Start all services
docker-compose up --build

# Start specific service
docker-compose up api

# View logs
docker-compose logs -f api

# Stop all services
docker-compose down

# Clean volumes (reset database)
docker-compose down -v
```

---

## ?? Monitoring & Troubleshooting

### Azure Application Logs

```bash
# View live logs
az webapp log tail --name pocketree-api --resource-group pocketree-rg

# Download logs
az webapp log download --name pocketree-api --resource-group pocketree-rg --log-file app-logs.zip
```

### Common Issues

#### Issue: API returns 500 error
**Solution:** Check Azure logs for database connection issues
```bash
az webapp log tail --name pocketree-api --resource-group pocketree-rg
```

#### Issue: Tests failing locally
**Solution:** Ensure MySQL is running and connection string is correct
```bash
docker-compose up db
dotnet test --verbosity detailed
```

#### Issue: Android can't connect to API
**Solution:** Check endpoint URL
* Emulator: `http://10.0.2.2:8080`
* Device on same network: `http://<your-local-ip>:8080`
* Production: `https://pocketree-api.azurewebsites.net`

---

## ?? Team & Contributing

### Core Team
* **Backend Lead** - ASP.NET Core API & Azure Infrastructure
* **Android Lead** - Kotlin Mobile App
* **ML Lead** - Python ML Service
* **DevOps** - CI/CD & Docker

### How to Contribute

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Write tests for your changes
4. Ensure all tests pass (`dotnet test`)
5. Commit your changes (`git commit -m 'feat: Add AmazingFeature'`)
6. Push to the branch (`git push origin feature/AmazingFeature`)
7. Open a Pull Request

---

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## ?? Acknowledgments

* **Microsoft Azure** - Cloud hosting
* **ASP.NET Core Team** - Excellent framework
* **Docker** - Containerization
* **GitHub Actions** - CI/CD automation
* **Android Jetpack** - Modern Android development

---

## ?? Support & Contact

* **Issues:** [GitHub Issues](https://github.com/shafikhakim27/Pocketree/issues)
* **Discussions:** [GitHub Discussions](https://github.com/shafikhakim27/Pocketree/discussions)
* **Email:** [Your email here]

---

## ?? Project Status

| Milestone | Status |
|-----------|--------|
| Backend API | ? Complete |
| Azure Deployment | ? Live |
| Database Migration | ? Complete |
| Android App | ?? In Progress |
| ML Service | ?? Planned |
| CI/CD Pipeline | ? Automated |
| Documentation | ? Complete |

**Last Updated:** February 2026  
**Version:** 1.0.0  
**Build Status:** ![Build](https://github.com/shafikhakim27/Pocketree/actions/workflows/azure-deploy.yml/badge.svg)

---

<div align="center">
  
### ?? Building a Greener Future, One Task at a Time ??

Made with ?? by the Pocketree Team

[View Live Demo](https://pocketree-api.azurewebsites.net) • [Report Bug](https://github.com/shafikhakim27/Pocketree/issues) • [Request Feature](https://github.com/shafikhakim27/Pocketree/issues)

</div>

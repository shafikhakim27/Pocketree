# Pocketree Deployment Checklist (Azure)

## Pre-Deployment Verification

### 1. **Environment Setup**
- [ ] `.env` file created with secure passwords
- [ ] `DB_PASSWORD` is strong (min 12 chars, mixed case, numbers, symbols)
- [ ] All ports are free (3306, 5080, 5000 for local testing)

### 2. **Database Migrations**
- [ ] Run migrations locally first:
  ```bash
  cd api
  dotnet ef database update
  ```
- [ ] Verify migrations exist: `api/Pocketree.Api/Migrations/`
- [ ] Migration file: `20260202083344_InitialCreate.cs` exists
- [ ] No pending migrations

### 3. **API Configuration**
- [ ] `api/Pocketree.Api/appsettings.json` - verify settings are correct:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=pocketree-db;Port=3306;Database=pocketree;User=root;Password=SecurePassword123!;"
  },
  "MlService": {
    "Url": "http://ml-service:5000"
  }
  ```
- [ ] JWT keys configured in appsettings.json
- [ ] CORS settings allow frontend domain (if applicable)

### 4. **ML Service**
- [ ] `ml-service/app.py` - CLIP model code merged ✅
- [ ] `ml-service/requirements.txt` - has all dependencies ✅
- [ ] `ml-service/Dockerfile` - uses Python 3.11-slim ✅
- [ ] Test CLIP model download (first run will be slow):
  ```bash
  python -c "from transformers import CLIPModel; CLIPModel.from_pretrained('openai/clip-vit-base-patch32')"
  ```
- [ ] Verify model is ~2GB (located in ~/.cache/huggingface/)

### 5. **Docker Setup**
- [ ] Docker Desktop running
- [ ] `docker --version` and `docker-compose --version` working
- [ ] All Dockerfiles exist:
  - [ ] `api/Pocketree.Api/Dockerfile`
  - [ ] `ml-service/Dockerfile`
- [ ] No syntax errors in Dockerfiles
- [ ] `docker-compose.yml` has all 3 services (db, api, ml-service)
- [ ] All health checks configured ✅

### 6. **Local Test (Before Azure)**
```bash
# Build images
docker-compose build

# Start services
docker-compose up -d

# Check status
docker-compose ps

# Test health
curl http://localhost:5080/api/health/live
curl http://localhost:5000/

# View logs
docker-compose logs -f ml-service
docker-compose logs -f pocketree-api

# Stop
docker-compose down
```

---

## Azure Deployment Preparation

### 7. **Azure Prerequisites**
- [ ] Azure subscription active
- [ ] Azure CLI installed: `az --version`
- [ ] Logged in: `az login`
- [ ] Azure resource group created:
  ```bash
  az group create --name pocketree-rg --location eastus
  ```

### 8. **Azure Container Registry (ACR)**
- [ ] ACR created:
  ```bash
  az acr create --resource-group pocketree-rg --name pocketreeregistry --sku Basic
  ```
- [ ] Verify ACR login:
  ```bash
  az acr login --name pocketreeregistry
  ```
- [ ] Get ACR login server:
  ```bash
  az acr show --resource-group pocketree-rg --name pocketreeregistry --query loginServer --output tsv
  ```

### 9. **Docker Images for Azure**
- [ ] Tag images for ACR:
  ```bash
  docker tag pocketree-api:latest pocketreeregistry.azurecr.io/pocketree-api:latest
  docker tag ml-service:latest pocketreeregistry.azurecr.io/ml-service:latest
  ```
- [ ] Push to ACR:
  ```bash
  docker push pocketreeregistry.azurecr.io/pocketree-api:latest
  docker push pocketreeregistry.azurecr.io/ml-service:latest
  ```
- [ ] Verify images in ACR:
  ```bash
  az acr repository list --name pocketreeregistry
  ```

### 10. **Azure Database for MySQL**
- [ ] Create Azure Database for MySQL:
  ```bash
  az mysql server create \
    --resource-group pocketree-rg \
    --name pocketree-db \
    --admin-user root \
    --admin-password SecurePassword123! \
    --sku-name B_Gen5_1 \
    --storage-size 51200 \
    --location eastus
  ```
- [ ] Configure firewall to allow Azure services:
  ```bash
  az mysql server firewall-rule create \
    --resource-group pocketree-rg \
    --server-name pocketree-db \
    --name AllowAzure \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0
  ```
- [ ] Get connection string:
  ```bash
  az mysql server show \
    --resource-group pocketree-rg \
    --name pocketree-db \
    --query fullyQualifiedDomainName
  ```

### 11. **Azure Container Instances or App Service**
Choose one:

**Option A: Azure Container Instances (Simpler)**
```bash
az container create \
  --resource-group pocketree-rg \
  --name pocketree-api \
  --image pocketreeregistry.azurecr.io/pocketree-api:latest \
  --registry-login-server pocketreeregistry.azurecr.io \
  --registry-username <username> \
  --registry-password <password> \
  --environment-variables \
    ConnectionStrings__DefaultConnection="Server=pocketree-db.mysql.database.azure.com;Port=3306;Database=pocketree;User=root@pocketree-db;Password=SecurePassword123!;" \
    ML_SERVICE_URL="http://ml-service:5000" \
  --ports 80 8080 \
  --dns-name-label pocketree-api
```

**Option B: Azure App Service (Recommended)**
```bash
az appservice plan create \
  --name pocketree-plan \
  --resource-group pocketree-rg \
  --sku B2 \
  --is-linux

az webapp create \
  --resource-group pocketree-rg \
  --plan pocketree-plan \
  --name pocketree-api \
  --deployment-container-image-name pocketreeregistry.azurecr.io/pocketree-api:latest
```

### 12. **Azure Key Vault (For Secrets)**
- [ ] Create Key Vault:
  ```bash
  az keyvault create \
    --resource-group pocketree-rg \
    --name pocketree-kv
  ```
- [ ] Store secrets:
  ```bash
  az keyvault secret set \
    --vault-name pocketree-kv \
    --name DbPassword \
    --value SecurePassword123!
  
  az keyvault secret set \
    --vault-name pocketree-kv \
    --name JwtKey \
    --value "YourLongSecretKeyHere"
  ```

### 13. **Networking (Optional but Recommended)**
- [ ] Create Virtual Network:
  ```bash
  az network vnet create \
    --resource-group pocketree-rg \
    --name pocketree-vnet \
    --address-prefix 10.0.0.0/16
  ```
- [ ] Create subnets for each service

### 14. **Monitoring & Logging**
- [ ] Create Application Insights:
  ```bash
  az monitor app-insights component create \
    --app pocketree-insights \
    --location eastus \
    --resource-group pocketree-rg
  ```
- [ ] Connect to API (add to appsettings.json)
- [ ] Set up alerts for failures

---

## Deployment Script (Automated)

```bash
#!/bin/bash
set -e

# Variables
RESOURCE_GROUP="pocketree-rg"
LOCATION="eastus"
REGISTRY_NAME="pocketreeregistry"
DB_PASSWORD="SecurePassword123!"

# 1. Create resource group
echo "Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION

# 2. Create ACR
echo "Creating Azure Container Registry..."
az acr create --resource-group $RESOURCE_GROUP --name $REGISTRY_NAME --sku Basic

# 3. Build and push images
echo "Building and pushing Docker images..."
docker-compose build
docker tag pocketree-api:latest ${REGISTRY_NAME}.azurecr.io/pocketree-api:latest
docker tag ml-service:latest ${REGISTRY_NAME}.azurecr.io/ml-service:latest
az acr login --name $REGISTRY_NAME
docker push ${REGISTRY_NAME}.azurecr.io/pocketree-api:latest
docker push ${REGISTRY_NAME}.azurecr.io/ml-service:latest

# 4. Create Azure MySQL
echo "Creating Azure Database for MySQL..."
az mysql server create \
  --resource-group $RESOURCE_GROUP \
  --name pocketree-db \
  --admin-user root \
  --admin-password $DB_PASSWORD \
  --sku-name B_Gen5_1 \
  --storage-size 51200 \
  --location $LOCATION

# 5. Create App Service Plan
echo "Creating App Service Plan..."
az appservice plan create \
  --name pocketree-plan \
  --resource-group $RESOURCE_GROUP \
  --sku B2 \
  --is-linux

# 6. Create Web App
echo "Creating Web App..."
az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan pocketree-plan \
  --name pocketree-api \
  --deployment-container-image-name ${REGISTRY_NAME}.azurecr.io/pocketree-api:latest

echo "✅ Deployment complete!"
echo "API URL: https://pocketree-api.azurewebsites.net"
```

---

## Post-Deployment Verification

- [ ] API health check: `https://pocketree-api.azurewebsites.net/api/health/live`
- [ ] ML service responding: `https://pocketree-api.azurewebsites.net/ml-status`
- [ ] Database connected: Check logs in Azure Portal
- [ ] CORS working: Test API from Android app
- [ ] Logs accessible: `az webapp log tail --resource-group pocketree-rg --name pocketree-api`

---

## Rollback Plan

If deployment fails:
```bash
# Delete all resources
az group delete --name pocketree-rg --yes

# Recreate from scratch using deployment script above
```

---

## Cost Optimization (Azure)

- [ ] Use **B1** tier for App Service (not B2)
- [ ] Use **Basic** MySQL tier (not Standard)
- [ ] Enable **auto-shutdown** for dev environments
- [ ] Use **Spot instances** if available
- [ ] Monitor costs weekly: `az billing account list`

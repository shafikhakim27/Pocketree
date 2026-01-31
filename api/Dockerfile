# 1. BUILD STAGE
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /api

# --- STEP A: COPY PROJECT FILES FOR CACHING ---
# We copy all .csproj and the .sln first. 
# This way, 'dotnet restore' only reruns if you change a NuGet package.
COPY ["api.sln", "api/"]
COPY ["Pocketree.Api/Pocketree.Api.csproj", "api/Pocketree.Api/"]
COPY ["Pocketree.Shared/Pocketree.Shared.csproj", "api/Pocketree.Shared/"]
COPY ["Pocketree.Api.Tests/Pocketree.Api.Tests.csproj", "api/Pocketree.Api.Tests/"]

# --- STEP B: RESTORE ---
RUN dotnet restore "api/api.sln"

# --- STEP C: COPY SOURCE CODE ---
# Now we copy the actual code for all folders
COPY api.sln ./api/
COPY api/Pocketree.Api/ ./api/Pocketree.Api/
COPY api/Pocketree.Shared/ ./api/Pocketree.Shared/
COPY api/Pocketree.Api.Tests/ ./api/Pocketree.Api.Tests/

# --- STEP D: BUILD & PUBLISH ---
WORKDIR "api/Pocketree.Api"
RUN dotnet publish "Pocketree.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. FINAL RUNTIME STAGE
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# Essential for Azure Health Checks and troubleshooting
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Pocketree.Api.dll"]
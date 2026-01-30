# 1. BUILD STAGE
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /api

COPY ["Pocketree.Api.csproj", "./"]

RUN dotnet restore "Pocketree.Api.csproj"

COPY . .

RUN dotnet build "Pocketree.Api.csproj" -c Release -o /app/build
RUN dotnet publish "Pocketree.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Pocketree.Api.dll"]
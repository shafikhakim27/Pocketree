# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first for better caching
COPY src/Pocketree.Api/*.csproj src/Pocketree.Api/
RUN dotnet restore src/Pocketree.Api/Pocketree.Api.csproj

# Copy the rest of the source
COPY . .
RUN dotnet publish src/Pocketree.Api/Pocketree.Api.csproj -c Release -o /app/out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 5000
ENTRYPOINT ["dotnet", "Pocketree.Api.dll"]
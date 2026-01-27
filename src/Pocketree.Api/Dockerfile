FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Pocketree.Api/Pocketree.Api.csproj", "src/Pocketree.Api/"]
RUN dotnet restore "src/Pocketree.Api/Pocketree.Api.csproj"
COPY . .
WORKDIR "/src/src/Pocketree.Api"
RUN dotnet build "Pocketree.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pocketree.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose both HTTP and HTTPS ports
EXPOSE 8080 8443

ENTRYPOINT ["dotnet", "Pocketree.Api.dll"]
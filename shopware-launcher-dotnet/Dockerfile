# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ShopwareLauncher/ShopwareLauncher.csproj ShopwareLauncher/
RUN dotnet restore ShopwareLauncher/ShopwareLauncher.csproj
COPY src/ShopwareLauncher/ ShopwareLauncher/
RUN dotnet publish ShopwareLauncher/ShopwareLauncher.csproj -c Release -o /app --no-restore

# Runtime (laeuft als root, damit /var/run/docker.sock nutzbar ist)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShopwareLauncher.dll"]

# ?? Build ??
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY AuthService.csproj ./
RUN dotnet restore AuthService.csproj
COPY . ./
RUN dotnet publish AuthService.csproj -c Release -o /out

# ?? Runtime ??
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# ?? Installer curl pour le healthcheck Docker
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AuthService.dll"]
# Build du microservice documentation : contexte = racine du dépôt (voir docker-compose.yml).
# Le fichier Api/AppRoleHeaderParser.cs du dépôt peut être vide ; on superpose l'implémentation depuis init/patches.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY documentation_service_backend/DocumentationBackend/DocumentationBackend.csproj DocumentationBackend/
COPY documentation_service_backend/Documentation.Contracts/Documentation.Contracts.csproj Documentation.Contracts/
RUN dotnet restore "./DocumentationBackend/DocumentationBackend.csproj"

COPY documentation_service_backend/ .
COPY init/patches/documentation-service/AppRoleHeaderParser.cs DocumentationBackend/Api/AppRoleHeaderParser.cs

RUN dotnet publish "./DocumentationBackend/DocumentationBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentationBackend.dll"]

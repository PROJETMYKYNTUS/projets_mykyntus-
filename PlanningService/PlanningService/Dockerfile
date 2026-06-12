# Contexte Docker = racine du dépôt (voir docker-compose.yml service planning-backend).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Messaging.Contracts/Messaging.Contracts.csproj Messaging.Contracts/
COPY PlanningService/PlanningService/PlanningService.csproj PlanningService/PlanningService/
RUN dotnet restore PlanningService/PlanningService/PlanningService.csproj

COPY Messaging.Contracts/ Messaging.Contracts/
COPY PlanningService/PlanningService/ PlanningService/PlanningService/
RUN dotnet publish PlanningService/PlanningService/PlanningService.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PlanningService.dll"]

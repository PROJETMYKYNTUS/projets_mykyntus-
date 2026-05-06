FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# ✅ Nom exact du .csproj
COPY PlanningService.csproj ./
RUN dotnet restore PlanningService.csproj

COPY . ./
RUN dotnet publish PlanningService.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /out .

EXPOSE 8080

# ✅ Nom exact de la DLL générée
ENTRYPOINT ["dotnet", "PlanningService.dll"]
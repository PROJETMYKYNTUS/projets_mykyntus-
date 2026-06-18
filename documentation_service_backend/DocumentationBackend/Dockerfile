# syntax=docker/dockerfile:1
# Build context: repository root "backend/" (see docker-compose.build.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DocumentationBackend/DocumentationBackend.csproj DocumentationBackend/
COPY Documentation.Contracts/Documentation.Contracts.csproj Documentation.Contracts/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore DocumentationBackend/DocumentationBackend.csproj

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish DocumentationBackend/DocumentationBackend.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "DocumentationBackend.dll"]

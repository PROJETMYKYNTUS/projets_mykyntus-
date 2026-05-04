FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["projetformationconges/Formation.API.csproj", "projetformationconges/"]
COPY ["src/Services/Formation/Formation.Domain/Formation.Domain.csproj", "src/Services/Formation/Formation.Domain/"]
COPY ["src/Services/Formation/Formation.Application/Formation.Application.csproj", "src/Services/Formation/Formation.Application/"]
COPY ["src/Services/Formation/Formation.Infrastructure/Formation.Infrastructure.csproj", "src/Services/Formation/Formation.Infrastructure/"]
COPY ["src/Services/Formation/Shared.Kernel/Shared.Kernel.csproj", "src/Services/Formation/Shared.Kernel/"]

RUN dotnet restore "projetformationconges/Formation.API.csproj"

COPY . .

WORKDIR "/src/projetformationconges"
RUN dotnet build "Formation.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Formation.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Formation.API.dll"]
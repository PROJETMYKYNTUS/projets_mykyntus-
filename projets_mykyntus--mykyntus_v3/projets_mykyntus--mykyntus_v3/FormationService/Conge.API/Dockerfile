FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Conge.API/Conge.API.csproj", "Conge.API/"]
COPY ["Conge.Application/Conge.Application.csproj", "Conge.Application/"]
COPY ["Conge.Domain/Conge.Domain.csproj", "Conge.Domain/"]
COPY ["Conge.Infrastructure/Conge.Infrastructure.csproj", "Conge.Infrastructure/"]

RUN dotnet restore "Conge.API/Conge.API.csproj"

COPY . .

WORKDIR "/src/Conge.API"
RUN dotnet build "Conge.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Conge.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Conge.API.dll"]
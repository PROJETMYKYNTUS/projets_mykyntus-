FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY PrimeBackend.csproj ./
RUN dotnet restore PrimeBackend.csproj

COPY . ./
RUN dotnet publish PrimeBackend.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PrimeBackend.dll"]

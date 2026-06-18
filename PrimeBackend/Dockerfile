FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Messaging.Contracts/Messaging.Contracts.csproj Messaging.Contracts/
COPY Kyntus.Identity.Jwt/Kyntus.Identity.Jwt.csproj Kyntus.Identity.Jwt/
COPY Kyntus.Iam/Kyntus.Iam.csproj Kyntus.Iam/
COPY Kyntus.Messaging.Outbox/Kyntus.Messaging.Outbox.csproj Kyntus.Messaging.Outbox/
COPY PrimeBackend/PrimeBackend.csproj PrimeBackend/
RUN dotnet restore PrimeBackend/PrimeBackend.csproj

COPY Messaging.Contracts/ Messaging.Contracts/
COPY Kyntus.Identity.Jwt/ Kyntus.Identity.Jwt/
COPY Kyntus.Iam/ Kyntus.Iam/
COPY Kyntus.Messaging.Outbox/ Kyntus.Messaging.Outbox/
COPY PrimeBackend/ PrimeBackend/
RUN dotnet publish PrimeBackend/PrimeBackend.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "PrimeBackend.dll"]

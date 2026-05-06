FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY ApiGateway.API.csproj ./
RUN dotnet restore ApiGateway.API.csproj

COPY . ./
RUN dotnet publish ApiGateway.API.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /out .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ApiGateway.API.dll"]
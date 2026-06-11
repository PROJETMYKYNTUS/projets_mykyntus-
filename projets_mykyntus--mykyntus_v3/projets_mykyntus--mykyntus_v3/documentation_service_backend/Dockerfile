FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DocumentationBackend/DocumentationBackend.csproj DocumentationBackend/
COPY Documentation.Contracts/Documentation.Contracts.csproj Documentation.Contracts/
RUN dotnet restore "./DocumentationBackend/DocumentationBackend.csproj"

COPY . .
RUN dotnet publish "./DocumentationBackend/DocumentationBackend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentationBackend.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinTestHub.Server/ITMartinTestHub.Server.csproj

RUN dotnet publish ITMartinTestHub.Server/ITMartinTestHub.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/db

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinTestHub.Server.dll"]

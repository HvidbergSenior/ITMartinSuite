FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinMusic.Server/ITMartinMusic.Server.csproj

RUN dotnet publish ITMartinMusic.Server/ITMartinMusic.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data /musik

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinMusic.Server.dll"]

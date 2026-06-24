FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinMagazine.Server/ITMartinMagazine.Server.csproj

RUN dotnet publish ITMartinMagazine.Server/ITMartinMagazine.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data/images

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinMagazine.Server.dll"]

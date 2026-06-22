FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinAuction.Server/ITMartinAuction.Server.csproj

RUN dotnet publish ITMartinAuction.Server/ITMartinAuction.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/db /app/data/photos

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinAuction.Server.dll"]

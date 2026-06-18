FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ITMartinMarket.Server/ITMartinMarket.Server.csproj
RUN dotnet publish ITMartinMarket.Server/ITMartinMarket.Server.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN mkdir -p /app/data/items
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ITMartinMarket.Server.dll"]

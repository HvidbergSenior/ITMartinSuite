FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinBarTab.Server/ITMartinBarTab.Server.csproj

RUN dotnet publish ITMartinBarTab.Server/ITMartinBarTab.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data/photos

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinBarTab.Server.dll"]

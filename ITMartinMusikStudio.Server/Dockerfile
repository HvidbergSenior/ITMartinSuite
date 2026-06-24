FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinMusikStudio.Server/ITMartinMusikStudio.Server.csproj

RUN dotnet publish ITMartinMusikStudio.Server/ITMartinMusikStudio.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinMusikStudio.Server.dll"]

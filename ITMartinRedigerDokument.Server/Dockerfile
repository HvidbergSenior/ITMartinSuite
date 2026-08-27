FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinRedigerDokument.Server/ITMartinRedigerDokument.Server.csproj

RUN dotnet publish ITMartinRedigerDokument.Server/ITMartinRedigerDokument.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data/documents

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinRedigerDokument.Server.dll"]

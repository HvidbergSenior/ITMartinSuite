FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinAeroMedRecord.Server/ITMartinAeroMedRecord.Server.csproj

RUN dotnet publish ITMartinAeroMedRecord.Server/ITMartinAeroMedRecord.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data/documents

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinAeroMedRecord.Server.dll"]

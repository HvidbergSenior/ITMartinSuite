FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinR6Intel.Server/ITMartinR6Intel.Server.csproj

RUN dotnet publish ITMartinR6Intel.Server/ITMartinR6Intel.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinR6Intel.Server.dll"]

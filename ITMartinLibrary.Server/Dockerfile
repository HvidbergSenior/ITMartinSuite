FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinLibrary.Server/ITMartinLibrary.Server.csproj

RUN dotnet publish ITMartinLibrary.Server/ITMartinLibrary.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN mkdir -p /app/data && chown -R app:app /app/data

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinLibrary.Server.dll"]

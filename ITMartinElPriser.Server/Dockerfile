FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinElPriser.Server/ITMartinElPriser.Server.csproj

RUN dotnet publish ITMartinElPriser.Server/ITMartinElPriser.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinElPriser.Server.dll"]

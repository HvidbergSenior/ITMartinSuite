FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinKanal.Server/ITMartinKanal.Server.csproj

RUN dotnet publish ITMartinKanal.Server/ITMartinKanal.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinKanal.Server.dll"]

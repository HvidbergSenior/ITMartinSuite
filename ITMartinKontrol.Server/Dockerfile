FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ITMartinKontrol.Server/ ITMartinKontrol.Server/
COPY ITMartin.Shared.UI/ ITMartin.Shared.UI/

RUN dotnet restore ITMartinKontrol.Server/ITMartinKontrol.Server.csproj

RUN dotnet publish ITMartinKontrol.Server/ITMartinKontrol.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinKontrol.Server.dll"]

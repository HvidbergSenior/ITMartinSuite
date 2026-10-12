FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinVareinfo.Server/ITMartinVareinfo.Server.csproj

RUN dotnet publish ITMartinVareinfo.Server/ITMartinVareinfo.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinVareinfo.Server.dll"]

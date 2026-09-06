FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinR6Club.Server/ITMartinR6Club.Server.csproj

RUN dotnet publish ITMartinR6Club.Server/ITMartinR6Club.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

RUN mkdir -p /app/data/documents

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinR6Club.Server.dll"]

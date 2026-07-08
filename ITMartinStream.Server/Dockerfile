FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ITMartinStream.Server/ITMartinStream.Server.csproj
RUN dotnet publish ITMartinStream.Server/ITMartinStream.Server.csproj \
    -c Release \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ITMartinStream.Server.dll"]

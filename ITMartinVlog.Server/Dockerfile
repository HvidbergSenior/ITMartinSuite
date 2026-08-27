FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinVlog.Server/ITMartinVlog.Server.csproj

RUN dotnet publish ITMartinVlog.Server/ITMartinVlog.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinVlog.Server.dll"]

# ------------------------
# BUILD STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ------------------------
# RUNTIME STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Install ffmpeg (used by your converters)
RUN apt-get update && \
    apt-get install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# 🔥 Ensure folders exist (avoids runtime issues)
RUN mkdir -p /data /library

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinFileSorter.Server.dll"]
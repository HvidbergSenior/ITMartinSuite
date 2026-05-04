# ------------------------
# BUILD STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (for restore)
COPY ITMartinFileSorter.Server/*.csproj ITMartinFileSorter.Server/
COPY ITMartinFileSorter.Application/*.csproj ITMartinFileSorter.Application/
COPY ITMartinFileSorter.Domain/*.csproj ITMartinFileSorter.Domain/
COPY ITMartinFileSorter.Infrastructure/*.csproj ITMartinFileSorter.Infrastructure/
COPY ITMartin.Shared.UI/*.csproj ITMartin.Shared.UI/

# Restore dependencies
RUN dotnet restore ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj

# Copy everything else
COPY . .

# Publish
RUN dotnet publish ITMartinFileSorter.Server/ITMartinFileSorter.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ------------------------
# RUNTIME STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /data /library

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinFileSorter.Server.dll"]
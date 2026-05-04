# ------------------------
# BUILD STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first
COPY ITMartinImageProcessor.Worker/*.csproj ITMartinImageProcessor.Worker/
COPY ITMartinImageProcessor.Application/*.csproj ITMartinImageProcessor.Application/
COPY ITMartinImageProcessor.Domain/*.csproj ITMartinImageProcessor.Domain/
COPY ITMartinImageProcessor.Infrastructure/*.csproj ITMartinImageProcessor.Infrastructure/

# ALSO needed because worker uses Budget logic
COPY ITMartinBudget.Application/*.csproj ITMartinBudget.Application/
COPY ITMartinBudget.Domain/*.csproj ITMartinBudget.Domain/
COPY ITMartinBudget.Infrastructure/*.csproj ITMartinBudget.Infrastructure/

# Restore
RUN dotnet restore ITMartinImageProcessor.Worker/ITMartinImageProcessor.Worker.csproj

# Copy everything
COPY . .

# Publish
RUN dotnet publish ITMartinImageProcessor.Worker/ITMartinImageProcessor.Worker.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ------------------------
# RUNTIME STAGE
# ------------------------
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /data /library /archive

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "ITMartinImageProcessor.Worker.dll"]
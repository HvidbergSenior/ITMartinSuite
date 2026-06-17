FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinFileSorter.Worker/ITMartinFileSorter.Worker.csproj

RUN dotnet publish ITMartinFileSorter.Worker/ITMartinFileSorter.Worker.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN apt-get update && \
    apt-get install -y \
        ffmpeg \
        libgl1 \
        libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /data /library

ENTRYPOINT ["dotnet", "ITMartinFileSorter.Worker.dll"]

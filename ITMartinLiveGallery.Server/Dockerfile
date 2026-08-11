FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinLiveGallery.Server/ITMartinLiveGallery.Server.csproj

RUN dotnet publish ITMartinLiveGallery.Server/ITMartinLiveGallery.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# ffmpeg: video-frame thumbnails. libheif-examples: heif-convert, for iPhone
# HEIC uploads (browsers can't display HEIC directly) - same tool FileSorter
# already uses for the same reason, see ImageConverterService.
RUN apt-get update && \
    apt-get install -y ffmpeg libheif-examples && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /media /data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinLiveGallery.Server.dll"]

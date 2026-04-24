FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

RUN apt-get update && apt-get install -y imagemagick

COPY out/ .

ENTRYPOINT ["dotnet", "ITMartinImageProcessor.Worker.dll"]
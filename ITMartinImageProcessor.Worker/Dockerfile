FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY . .

RUN mkdir -p /data /library /archive

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "ITMartinImageProcessor.Worker.dll"]
FROM mcr.microsoft.com/dotnet/runtime:8.0

RUN apt-get update && \
    apt-get install -y \
        libheif-examples \
        libheif1 \
        libde265-0 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY ./app/ ./
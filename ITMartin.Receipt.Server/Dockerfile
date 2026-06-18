FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartin.Receipt.Server/ITMartin.Receipt.Server.csproj

RUN dotnet publish ITMartin.Receipt.Server/ITMartin.Receipt.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN apt-get update && \
    apt-get install -y \
        libgl1 \
        libglib2.0-0 \
        tesseract-ocr \
        tesseract-ocr-eng \
    && find /usr/lib -name "libleptonica.so.*" | head -1 | xargs -I{} ln -sf {} /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data/receipts

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartin.Receipt.Server.dll"]

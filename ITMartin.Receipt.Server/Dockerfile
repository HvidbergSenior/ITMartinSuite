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
        libleptonica-dev \
    && find /usr/lib -name "libleptonica.so*" -o -name "libleptonica-*.so" 2>/dev/null | head -5 \
    && LEPT=$(find /usr/lib /usr/local/lib -name "libleptonica.so*" 2>/dev/null | head -1) \
    && if [ -n "$LEPT" ]; then ln -sf "$LEPT" /usr/lib/x86_64-linux-gnu/libleptonica-1.82.0.so; fi \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /data/receipts

COPY --from=build /app/publish .

# 2026-11-19: the Tesseract package loads its native libraries from /app/x64 (only Windows .dll files ship there), so OCR
# failed with "Failed to find library libleptonica-1.82.0.so" and every free read returned nothing. Link the Debian ones in.
RUN mkdir -p /app/x64 \
    && ln -sf "$(ls /usr/lib/x86_64-linux-gnu/liblept.so.5* | head -1)" /app/x64/libleptonica-1.82.0.so \
    && ln -sf "$(ls /usr/lib/x86_64-linux-gnu/libtesseract.so.5* | head -1)" /app/x64/libtesseract50.so \
    && ls -l /app/x64

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartin.Receipt.Server.dll"]

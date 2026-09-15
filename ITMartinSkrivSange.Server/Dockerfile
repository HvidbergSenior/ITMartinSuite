FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinSkrivSange.Server/ITMartinSkrivSange.Server.csproj

RUN dotnet publish ITMartinSkrivSange.Server/ITMartinSkrivSange.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# ffmpeg for sketch/take conversion. No torch/demucs here on purpose - the
# song-writing app stays light; stems and chord detection simply report
# "not available" (Song Vault hides those buttons).
RUN apt-get update && \
    apt-get install -y ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinSkrivSange.Server.dll"]

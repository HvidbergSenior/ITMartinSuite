FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinFileSorter.Gallery.Server/ITMartinFileSorter.Gallery.Server.csproj

RUN dotnet publish ITMartinFileSorter.Gallery.Server/ITMartinFileSorter.Gallery.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN mkdir -p /library

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinFileSorter.Gallery.Server.dll"]

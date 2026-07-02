FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ITMartinUpload.Server/ITMartinUpload.Server.csproj", "ITMartinUpload.Server/"]
RUN dotnet restore "ITMartinUpload.Server/ITMartinUpload.Server.csproj"
COPY . .
WORKDIR "/src/ITMartinUpload.Server"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ITMartinUpload.Server.dll"]

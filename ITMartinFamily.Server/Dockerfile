FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ITMartinFamily.Server/ITMartinFamily.Server.csproj
RUN dotnet publish ITMartinFamily.Server/ITMartinFamily.Server.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN mkdir -p /app/data/tasks
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ITMartinFamily.Server.dll"]

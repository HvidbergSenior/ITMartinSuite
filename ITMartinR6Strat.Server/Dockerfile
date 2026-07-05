FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ITMartinR6Strat.Server/ITMartinR6Strat.Server.csproj", "ITMartinR6Strat.Server/"]
RUN dotnet restore "ITMartinR6Strat.Server/ITMartinR6Strat.Server.csproj"
COPY . .
WORKDIR "/src/ITMartinR6Strat.Server"
RUN dotnet build "ITMartinR6Strat.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ITMartinR6Strat.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ITMartinR6Strat.Server.dll"]

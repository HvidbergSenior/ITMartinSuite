# ------------------------
# BUILD
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 👉 Copy ONLY csproj files first (for restore)
COPY ITMartinBudget.Server/*.csproj ITMartinBudget.Server/
COPY ITMartinBudget.Application/*.csproj ITMartinBudget.Application/
COPY ITMartinBudget.Domain/*.csproj ITMartinBudget.Domain/
COPY ITMartinBudget.Infrastructure/*.csproj ITMartinBudget.Infrastructure/
COPY ITMartin.Shared.UI/*.csproj ITMartin.Shared.UI/

# 👉 Restore dependencies
RUN dotnet restore ITMartinBudget.Server/ITMartinBudget.Server.csproj

# 👉 Now copy EVERYTHING
COPY . .

# 👉 Publish
RUN dotnet publish ITMartinBudget.Server/ITMartinBudget.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ------------------------
# RUNTIME
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinBudget.Server.dll"]
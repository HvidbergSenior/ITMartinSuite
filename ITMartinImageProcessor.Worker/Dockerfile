FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

COPY ./app/ ./

ENTRYPOINT ["dotnet", "ITMartinImageProcessor.Worker.dll"]
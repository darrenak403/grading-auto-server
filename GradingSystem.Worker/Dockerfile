
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GradingSystem.Domain/GradingSystem.Domain.csproj                 GradingSystem.Domain/
COPY GradingSystem.Application/GradingSystem.Application.csproj       GradingSystem.Application/
COPY GradingSystem.Infrastructure/GradingSystem.Infrastructure.csproj GradingSystem.Infrastructure/
COPY GradingSystem.Worker/GradingSystem.Worker.csproj                 GradingSystem.Worker/
RUN dotnet restore GradingSystem.Worker/GradingSystem.Worker.csproj

COPY GradingSystem.Domain/           GradingSystem.Domain/
COPY GradingSystem.Application/      GradingSystem.Application/
COPY GradingSystem.Infrastructure/   GradingSystem.Infrastructure/
COPY GradingSystem.Worker/           GradingSystem.Worker/
RUN dotnet publish GradingSystem.Worker/GradingSystem.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-noble AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g newman@6 \
    && apt-get purge -y curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GradingSystem.Worker.dll"]

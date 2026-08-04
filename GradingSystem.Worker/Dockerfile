
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

# Chromium + deps đã bake sẵn (cùng bản 1.52 noble — khớp Microsoft.Playwright 1.52.*)
FROM mcr.microsoft.com/playwright:v1.52.0-noble AS playwright

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g newman@6 \
    && apt-get purge -y curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=playwright /ms-playwright /ms-playwright

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

ENTRYPOINT ["dotnet", "GradingSystem.Worker.dll"]

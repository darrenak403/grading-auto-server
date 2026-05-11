FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY GradingSystem.Domain/GradingSystem.Domain.csproj           GradingSystem.Domain/
COPY GradingSystem.Application/GradingSystem.Application.csproj GradingSystem.Application/
COPY GradingSystem.Infrastructure/GradingSystem.Infrastructure.csproj GradingSystem.Infrastructure/
COPY GradingSystem.Api/GradingSystem.Api.csproj                 GradingSystem.Api/
RUN dotnet restore GradingSystem.Api/GradingSystem.Api.csproj

COPY GradingSystem.Domain/           GradingSystem.Domain/
COPY GradingSystem.Application/      GradingSystem.Application/
COPY GradingSystem.Infrastructure/   GradingSystem.Infrastructure/
COPY GradingSystem.Api/              GradingSystem.Api/
RUN dotnet publish GradingSystem.Api/GradingSystem.Api.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GradingSystem.Api.dll"]

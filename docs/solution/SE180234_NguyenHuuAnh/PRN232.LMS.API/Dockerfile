# ── Build Stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["PRN232.LMS.API/PRN232.LMS.API.csproj", "PRN232.LMS.API/"]
COPY ["PRN232.LMS.Services/PRN232.LMS.Services.csproj", "PRN232.LMS.Services/"]
COPY ["PRN232.LMS.Repositories/PRN232.LMS.Repositories.csproj", "PRN232.LMS.Repositories/"]
RUN dotnet restore "PRN232.LMS.API/PRN232.LMS.API.csproj"

# Copy all source code and build
COPY . .
WORKDIR /src/PRN232.LMS.API
RUN dotnet build "PRN232.LMS.API.csproj" -c Release -o /app/build

# ── Publish Stage ───────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "PRN232.LMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "PRN232.LMS.API.dll"]

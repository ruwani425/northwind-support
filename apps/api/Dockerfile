# Stage 1: Build & Publish using .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution configuration and project files for caching dependency restore
COPY global.json Directory.Build.props Directory.Packages.props SupportDesk.sln ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
COPY apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj apps/api/tests/SupportDesk.UnitTests/
COPY apps/api/tests/SupportDesk.ArchitectureTests/SupportDesk.ArchitectureTests.csproj apps/api/tests/SupportDesk.ArchitectureTests/

# Restore dependencies
RUN dotnet restore SupportDesk.sln

# Copy backend source files
COPY apps/api/ apps/api/

# Publish Release build without re-restoring
WORKDIR /src/apps/api/src/SupportDesk.Presentation
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime using ASP.NET 10
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root user (app user with UID 1654 built into the .NET runtime image)
USER $APP_UID

# Expose HTTP port 8080
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

# Copy published binaries
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]

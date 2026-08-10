# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartInventoryService.sln Directory.Build.props nuget.config ./
COPY packages/ packages/
COPY src/Api/KartInventoryService.Api.csproj src/Api/
COPY src/Application/KartInventoryService.Application.csproj src/Application/
COPY src/Domain/KartInventoryService.Domain.csproj src/Domain/
COPY src/Infrastructure/KartInventoryService.Infrastructure.csproj src/Infrastructure/
# The cache mount persists extracted NuGet packages under a stable id shared by every other
# kart-*-service Dockerfile, so restore stays fast (no re-download) even on a cache-miss here
# (e.g. after a .csproj change) as long as some other service's build already warmed it.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore src/Api/KartInventoryService.Api.csproj

COPY src/ src/
COPY contracts/ contracts/
# --no-restore only skips re-resolving the dependency graph -- publish still reads the actual
# package DLLs from the global packages folder, so it needs the same cache mount as restore
# above (the mount isn't part of the image; without it here this folder is empty again).
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish src/Api/KartInventoryService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KartInventoryService.Api.dll"]

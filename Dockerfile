FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartInventoryService.sln Directory.Build.props ./
COPY src/Api/KartInventoryService.Api.csproj src/Api/
COPY src/Application/KartInventoryService.Application.csproj src/Application/
COPY src/Domain/KartInventoryService.Domain.csproj src/Domain/
COPY src/Infrastructure/KartInventoryService.Infrastructure.csproj src/Infrastructure/
RUN dotnet restore src/Api/KartInventoryService.Api.csproj

COPY src/ src/
COPY contracts/ contracts/
RUN dotnet publish src/Api/KartInventoryService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KartInventoryService.Api.dll"]

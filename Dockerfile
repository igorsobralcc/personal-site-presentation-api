FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:620e765fe18186c08399f7aa978f79f04b6bbf0ee1b3b8a91e2d5c9619e59da1 AS build
WORKDIR /src

COPY src/PersonalSite.Presentation.Api/PersonalSite.Presentation.Api.csproj src/PersonalSite.Presentation.Api/
RUN dotnet restore src/PersonalSite.Presentation.Api/PersonalSite.Presentation.Api.csproj

COPY src/PersonalSite.Presentation.Api/ src/PersonalSite.Presentation.Api/
RUN dotnet publish src/PersonalSite.Presentation.Api/PersonalSite.Presentation.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine@sha256:d39b72ad1534fa0faf52b488bffb0762df7ba269d120f0338713d4dda2a41bf5 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=6 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "PersonalSite.Presentation.Api.dll"]

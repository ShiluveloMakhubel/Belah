FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Belah-Backend/Belah-Backend.csproj Belah-Backend/
RUN dotnet restore Belah-Backend/Belah-Backend.csproj

COPY Belah-Backend/ Belah-Backend/
WORKDIR /src/Belah-Backend
RUN dotnet publish Belah-Backend.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
RUN mkdir -p /app/wwwroot/uploads \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Belah-Backend.dll"]

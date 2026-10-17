FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Host/Host.csproj --locked-mode
RUN dotnet publish src/Host/Host.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data/files /data/keys && chown -R $APP_UID:$APP_UID /data
VOLUME ["/data"]
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__Root=/data/files \
    DataProtection__KeysPath=/data/keys \
    LoggerSettings__WriteToFile=false
EXPOSE 8080
ENTRYPOINT ["dotnet", "Boilerate.Host.dll"]

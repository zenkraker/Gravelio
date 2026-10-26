# syntax=docker/dockerfile:1
# Build context: the repository root.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY Directory.Packages.props Gravelio.slnx ./
COPY backend/Gravelio.Api/Gravelio.Api.csproj backend/Gravelio.Api/
COPY backend/Gravelio.Tests/Gravelio.Tests.csproj backend/Gravelio.Tests/
RUN dotnet restore backend/Gravelio.Api/Gravelio.Api.csproj
COPY backend/Gravelio.Api backend/Gravelio.Api
RUN dotnet publish backend/Gravelio.Api/Gravelio.Api.csproj -c Release --no-restore -o /out/api

# Self-contained EF migration bundle, so production needs neither the SDK nor the EF tools.
FROM build AS bundle
ARG TARGETARCH
RUN dotnet tool install --global dotnet-ef --version 10.0.14
ENV PATH="$PATH:/root/.dotnet/tools"
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) \
    && dotnet ef migrations bundle --project backend/Gravelio.Api --self-contained -r $RID --force -o /out/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrate
WORKDIR /app
COPY --from=bundle /out/efbundle .
USER $APP_UID
ENTRYPOINT ["/bin/sh", "-c", "./efbundle --connection \"$ConnectionStrings__Default\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# X-Forwarded-* headers are honoured only from private networks (TrustedProxies setting), i.e. the web image's nginx.
ENV ASPNETCORE_HTTP_PORTS=8080 DataProtection__KeysPath=/keys ImportJobs__Directory=/imports
# The login-cookie key ring lives on its own volume (see docker-compose.yml), owned by the app user.
# Uploaded zips wait here for their background import; a volume, so a queued import survives a restart.
RUN mkdir /keys /imports && chown $APP_UID /keys /imports
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gravelio.Api.dll"]

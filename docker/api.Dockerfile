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
# Behind the web image's nginx (and any TLS-terminating proxy in front of it), honour X-Forwarded-* headers.
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gravelio.Api.dll"]

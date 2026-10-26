#!/usr/bin/env bash
# Runs on every start: the dev database from appsettings.json, for `dotnet run` (the tests start their own through Testcontainers).
docker start gravelio-db 2>/dev/null || docker run -d --name gravelio-db --restart unless-stopped \
  -e POSTGRES_USER=gravelio -e POSTGRES_PASSWORD=gravelio -e POSTGRES_DB=gravelio \
  -p 5432:5432 postgres:16-alpine

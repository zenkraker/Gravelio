# syntax=docker/dockerfile:1
# Build context: the repository root.
FROM node:24-alpine AS build
WORKDIR /src/frontend
# pnpm then switches itself to the version pinned in package.json.
RUN npm install --global pnpm@12
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ ./
RUN pnpm exec ng build

FROM nginx:alpine AS web
# The nginx image fills ${TILE_ORIGIN} (the CSP's img-src for map tiles) into this template at start-up; 15-tile-origin.envsh derives it from TILE_URL.
COPY docker/nginx.conf /etc/nginx/templates/default.conf.template
COPY docker/15-tile-origin.envsh /docker-entrypoint.d/15-tile-origin.envsh
COPY --from=build /src/frontend/dist/frontend/browser /usr/share/nginx/html
EXPOSE 8080

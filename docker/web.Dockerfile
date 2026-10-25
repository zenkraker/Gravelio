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
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/frontend/dist/frontend/browser /usr/share/nginx/html
EXPOSE 8080

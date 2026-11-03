# syntax=docker/dockerfile:1.7
FROM node:22-alpine AS build
WORKDIR /app

COPY frontend/branchbooking-web/package.json frontend/branchbooking-web/package-lock.json ./
RUN npm ci

COPY frontend/branchbooking-web/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY docker/nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY docker/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/branchbooking-web/browser /usr/share/nginx/html
EXPOSE 8080

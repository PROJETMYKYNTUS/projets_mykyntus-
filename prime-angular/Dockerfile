# bookworm-slim : plus stable qu'alpine pour ng build (évite EIO sous Docker Desktop / WSL2).
FROM node:20-bookworm-slim AS build
WORKDIR /app

ENV NODE_OPTIONS="--max-old-space-size=4096"
ENV CI=true

COPY package*.json ./
RUN npm ci --no-audit --fund=false

COPY . ./
RUN npm run build -- --configuration production

FROM nginx:alpine AS runtime

COPY --from=build /app/dist/prime-angular/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist/documentation-ui/browser /usr/share/nginx/html
COPY docker/nginx/default.conf /etc/nginx/conf.d/default.conf

# apiBaseUrl vide : le code Angular fait `${apiBaseUrl}/api/documentation/...` → `/api/...` (pas /api/api/...).
RUN printf '%s\n' 'window.__env = {' '  API_BASE_URL: ""' '};' > /usr/share/nginx/html/env.js

EXPOSE 80

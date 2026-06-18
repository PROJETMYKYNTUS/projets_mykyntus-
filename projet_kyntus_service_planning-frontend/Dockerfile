# ── Build Angular ──
FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . ./
RUN npm run build -- --configuration production

# ── Serve avec Nginx ──
FROM nginx:alpine AS runtime

# ✅ Nom exact du projet Angular
COPY --from=build /app/dist/planning-frontend/browser /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
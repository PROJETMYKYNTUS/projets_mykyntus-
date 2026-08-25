# ── Build React (base /qualite-app/ pour iframe MyKyntus)
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json ./
RUN npm install --registry https://registry.npmjs.org/
COPY . ./
ENV VITE_API_URL=/api/qualite
RUN npx vite build --base=/qualite-app/

# ── Nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html/qualite-app
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

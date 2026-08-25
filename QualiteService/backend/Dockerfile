FROM node:20-alpine

WORKDIR /app

COPY package.json ./
RUN npm install --omit=dev --registry https://registry.npmjs.org/

COPY . .
RUN rm -rf src public index.html vite.config.js

EXPOSE 5000

HEALTHCHECK --interval=15s --timeout=5s --retries=12 --start-period=40s \
  CMD node -e "require('http').get('http://127.0.0.1:5000/api/qualite/health',r=>process.exit(r.statusCode===200?0:1)).on('error',()=>process.exit(1))"

CMD ["npm", "start"]

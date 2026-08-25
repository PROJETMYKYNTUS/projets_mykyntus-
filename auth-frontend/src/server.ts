import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { createProxyMiddleware } from 'http-proxy-middleware';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

// Proxy /api/* → API Gateway (même convention que planning-frontend/nginx.conf)
const gatewayTarget = process.env['API_URL'] || 'http://api-gateway:8080';
app.use(
  createProxyMiddleware({
    target: gatewayTarget,
    changeOrigin: true,
    pathFilter: '/api/**',
    on: {
      error: (err, _req, res) => {
        console.error('[auth-frontend proxy]', gatewayTarget, err.message);
        if ('writeHead' in res && typeof res.writeHead === 'function') {
          res.writeHead(502, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ message: 'API Gateway injoignable', detail: err.message }));
        }
      },
    },
  }),
);

// URLs publiques (profil local/deploy) : jamais en cache navigateur
app.get('/kyntus-public-urls.js', (_req, res) => {
  res.setHeader('Cache-Control', 'no-store, no-cache, must-revalidate');
  res.setHeader('Pragma', 'no-cache');
  res.setHeader('Expires', '0');
  res.sendFile(join(browserDistFolder, 'kyntus-public-urls.js'));
});

// Serve static files
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

// Handle Angular SSR
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) throw error;
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

export const reqHandler = createNodeRequestHandler(app);
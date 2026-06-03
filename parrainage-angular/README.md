# parrainage-angular

Frontend Angular du module **Parrainage** MyKyntus (migration depuis `parrainage_service_frontend` React).

## Développement

```bash
npm install
npm start
```

Application : http://localhost:4203

Proxy API dev : `/api` → `http://localhost:5000` (`proxy.conf.json`).

## Docker

```bash
docker compose build parrainage-frontend
docker compose up -d parrainage-frontend
```

URL : http://localhost:4203

## Stack

- Angular 19 (standalone)
- Tailwind CSS v4
- Données démo : localStorage (mêmes clés `parrainage.*` que l’ancien frontend React)

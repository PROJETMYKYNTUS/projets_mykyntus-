"""Restore docker-compose.yml from backup (UTF-8 clean) + runtime URL volumes."""
from pathlib import Path
import re

root = Path(__file__).resolve().parent.parent
src = (root / "docker-compose.yml.backup").read_text(encoding="utf-8")

planning = """  planning-frontend:
     build:
       context: ./projet_kyntus_service_planning-frontend
       dockerfile: Dockerfile
     container_name: kyntus_planning_frontend
     restart: unless-stopped
     ports:
       - "8200:80"
     volumes:
       - ./config/kyntus-public-urls.runtime.js:/usr/share/nginx/html/kyntus-public-urls.js:ro
     depends_on:
       api-gateway:
         condition: service_started
     networks:
       - microservices-network

"""

pattern = r"# #  planning-frontend:.*?#      # - microservices-network\n# \n"
src, n = re.subn(pattern, planning, src, count=1, flags=re.S)
if n != 1:
    raise SystemExit(f"planning block replace failed: {n}")

src = src.replace(
    "    environment:\n      - API_URL=http://api-gateway:8080\n    depends_on:",
    "    environment:\n      - API_URL=http://api-gateway:8080\n    volumes:\n"
    "      - ./config/kyntus-public-urls.runtime.js:/app/dist/auth-frontend/browser/kyntus-public-urls.js:ro\n"
    "    depends_on:",
    1,
)

src = re.sub(
    r'public_gateway_base:\s*"[^"]+"',
    'public_gateway_base: "http://10.10.10.25:8500"',
    src,
    count=1,
)

out = root / "docker-compose.yml"
out.write_text(src, encoding="utf-8", newline="\n")
print(f"restored {out}")

# Arms Fair — VPS Deployment Spec
_Written 2026-05-06. Hand this document to a fresh Claude conversation to execute._

---

## Context

Arms Fair is a multiplayer game with:
- **Server:** ASP.NET Core 8 + SignalR (`ArmsFair.Server/`)
- **Database:** PostgreSQL (via Entity Framework Core)
- **Cache:** Redis (optional — server has in-memory fallback if Redis unavailable)
- **Client:** Unity game (connects over HTTP/WebSocket)

The server currently runs on `http://localhost:5059` in development.

---

## Target Environment

- **VPS:** Hostinger, Ubuntu 24.04
- **Existing stack:** MERN (MongoDB, Express, React, Node) — **DO NOT TOUCH any existing directories or configs belonging to this stack**
- **Isolation method:** Docker Compose (Arms Fair runs in its own containers, separate from MERN)
- **Nginx:** Already installed and serving other subdomains — we add one new server block only
- **Target subdomain:** `armsfair.laynekudo.com` ← replace with actual domain
- **SSL:** Let's Encrypt via Certbot (likely already installed for other subdomains)

---

## Safety Rules

1. Only create new files and directories — do not modify or delete anything outside the Arms Fair deployment directory
2. The Arms Fair directory on the VPS should be: `/home/USERNAME/armsfair/` (or `/root/armsfair/` if preferred)
3. Only add a new Nginx server block — do not edit existing ones
4. Only open new firewall ports if needed — do not close or modify existing rules

---

## Phase 1 — Diagnostics (run first, report findings before proceeding)

SSH into the VPS and run these read-only checks:

```bash
# Docker
docker --version 2>/dev/null || echo "Docker NOT installed"
docker compose version 2>/dev/null || docker-compose --version 2>/dev/null || echo "Docker Compose NOT installed"

# Nginx
nginx -v 2>&1
ls /etc/nginx/sites-available/

# Certbot
certbot --version 2>/dev/null || echo "Certbot NOT installed"

# Ports in use
ss -tlnp | grep -E ':80|:443|:5059|:5432'

# Disk space
df -h /
```

Report all output before proceeding to Phase 2.

---

## Phase 2 — Install Docker (if not installed)

If Docker is not installed:

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
newgrp docker
docker --version
```

---

## Phase 3 — Project Directory + Files

### 3.1 Create deployment directory

```bash
mkdir -p /root/armsfair
cd /root/armsfair
```

### 3.2 Dockerfile

**File:** `/root/armsfair/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ArmsFair.Server/ ./ArmsFair.Server/
COPY ArmsFair.Shared/ ./ArmsFair.Shared/
RUN dotnet publish ArmsFair.Server/ArmsFair.Server.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5059
ENTRYPOINT ["dotnet", "ArmsFair.Server.dll"]
```

### 3.3 docker-compose.yml

**File:** `/root/armsfair/docker-compose.yml`

```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: armsfair-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: armsfair
      POSTGRES_USER: armsfair
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    networks:
      - armsfair-net

  server:
    build:
      context: ../   # root of the git repo
      dockerfile: deploy/Dockerfile
    container_name: armsfair-server
    restart: unless-stopped
    depends_on:
      - postgres
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:5059
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=armsfair;Username=armsfair;Password=${POSTGRES_PASSWORD}"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: armsfair
      Jwt__Audience: armsfair
    ports:
      - "127.0.0.1:5059:5059"   # only expose to localhost — Nginx proxies externally
    networks:
      - armsfair-net

volumes:
  postgres_data:

networks:
  armsfair-net:
    driver: bridge
```

### 3.4 .env file

**File:** `/root/armsfair/.env`  
_(not committed to git — set on server only)_

```env
POSTGRES_PASSWORD=choose_a_strong_password_here
JWT_SECRET=choose_a_long_random_secret_here_at_least_32_chars
```

Generate a strong JWT secret:
```bash
openssl rand -base64 32
```

---

## Phase 4 — Deploy Source Code

Two options — pick one:

### Option A: Git clone (recommended)
```bash
cd /root/armsfair
git clone https://github.com/YOUR_USERNAME/arms-fair.git repo
```
Then update `docker-compose.yml` `context:` to `/root/armsfair/repo`

### Option B: SCP from local machine
```bash
# Run from local machine (not VPS)
scp -r ./ArmsFair.Server USERNAME@VPS_IP:/root/armsfair/
scp -r ./ArmsFair.Shared USERNAME@VPS_IP:/root/armsfair/
```

---

## Phase 5 — Database Migrations

Run EF Core migrations against the containerised Postgres:

```bash
cd /root/armsfair

# Start only the postgres container first
docker compose up -d postgres

# Run migrations from local machine (requires dotnet-ef installed)
# Update this connection string to point at the VPS postgres temporarily
# (open port 5432 briefly if needed, or use dotnet ef from inside a temp container)

# Easiest: run migrations from inside a temporary SDK container
docker run --rm \
  --network armsfair_armsfair-net \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=armsfair;Username=armsfair;Password=YOUR_POSTGRES_PASSWORD" \
  -v /root/armsfair/repo:/src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -c "cd /src && dotnet tool install --global dotnet-ef && export PATH=\"$PATH:/root/.dotnet/tools\" && dotnet ef database update --project ArmsFair.Server"
```

---

## Phase 6 — Nginx Configuration

Add a new server block — do NOT modify existing ones.

**File:** `/etc/nginx/sites-available/armsfair`

```nginx
server {
    listen 80;
    server_name armsfair.laynekudo.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name armsfair.laynekudo.com;

    ssl_certificate     /etc/letsencrypt/live/armsfair.laynekudo.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/armsfair.laynekudo.com/privkey.pem;
    include             /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam         /etc/letsencrypt/ssl-dhparams.pem;

    location / {
        proxy_pass         http://127.0.0.1:5059;
        proxy_http_version 1.1;

        # Required for SignalR WebSocket upgrade
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";

        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;

        # SignalR long-polling timeout
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }
}
```

Enable and test:
```bash
ln -s /etc/nginx/sites-available/armsfair /etc/nginx/sites-enabled/armsfair
nginx -t
systemctl reload nginx
```

---

## Phase 7 — SSL Certificate

If Certbot is already installed (check output from Phase 1):
```bash
certbot --nginx -d armsfair.laynekudo.com
```

If Certbot is not installed:
```bash
apt install certbot python3-certbot-nginx -y
certbot --nginx -d armsfair.laynekudo.com
```

---

## Phase 8 — Start Everything

```bash
cd /root/armsfair
docker compose up -d
docker compose logs -f server   # watch for startup errors
```

Confirm server is up:
```bash
curl https://armsfair.laynekudo.com/health
# or
curl http://127.0.0.1:5059/health
```

---

## Phase 9 — Update Unity Client

In the Bootstrap scene, select the `AccountManager` GameObject and update the **Server URL** field in the Inspector:

```
https://armsfair.laynekudo.com
```

Also update `GameClient.cs` — find the SignalR connection URL and change it from:
```
ws://localhost:5059/gamehub
```
to:
```
wss://armsfair.laynekudo.com/gamehub
```

---

## Phase 10 — Verify End-to-End

1. Start the Unity client in play mode
2. Register a new account on the Register screen
3. Confirm login lands on Main Menu with correct username
4. Check VPS logs: `docker compose logs -f server`

---

## Known Server Gaps (from handoff.md)

These are NOT blocking for initial deploy but note them:
- `StatsService` — lifetime stat updates at game end not wired
- `ChatRepository` — chat not persisted to DB
- Treaty system stubbed (0 values)
- Redis not configured — server uses in-memory fallback (fine for dev testing)

---

## Rollback

To tear down without affecting anything else on the server:
```bash
cd /root/armsfair
docker compose down
# data is preserved in the postgres_data volume
# to also delete data: docker compose down -v
```

To remove the Nginx config:
```bash
rm /etc/nginx/sites-enabled/armsfair
nginx -t && systemctl reload nginx
```

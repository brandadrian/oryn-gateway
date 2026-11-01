# Neue App hinzufügen

[← zurück zur README](../README.md)

1. Neuen Ordner anlegen, z.B. `meineapp/`
2. `docker-compose.yml` mit Netzwerk `proxy` und Traefik-Labels:

```yaml
services:
  meineapp:
    image: dein/image
    restart: unless-stopped
    labels:
      - traefik.enable=true
      - traefik.http.routers.meineapp.rule=Host(`meineapp.homelab.brandadrian.com`)
      - traefik.http.routers.meineapp.entrypoints=websecure
      - traefik.http.routers.meineapp.tls.certresolver=le
      - traefik.http.routers.meineapp.service=meineapp
      # optional lokal: https://meineapp.localhost
      - traefik.http.routers.meineapp-local.rule=Host(`meineapp.localhost`)
      - traefik.http.routers.meineapp-local.entrypoints=websecure
      - traefik.http.routers.meineapp-local.tls=true
      - traefik.http.routers.meineapp-local.service=meineapp
      - traefik.http.services.meineapp.loadbalancer.server.port=8080  # interner Port der App
    networks:
      - proxy

networks:
  proxy:
    external: true
```

3. `docker compose up -d`

Router-Name (`meineapp`) muss pro App eindeutig sein. Kein DNS-Eintrag nötig (Wildcard), keine Änderung an Traefik.

# Troubleshooting

[← zurück zur README](../README.md)

## Browser zeigt `404 page not found`

Die Seite kommt von Traefik, aber kein Router passt auf den Host. Meist läuft der Container noch mit alten Labels → im App-Ordner `docker compose up -d --force-recreate`. Prüfen mit `docker logs traefik` (dort steht die aktuelle `rule=`).

## `acme: error: 403 ... Invalid response from http://.../.well-known/acme-challenge/...`

Let's Encrypt erreicht Port 80 nicht. Prüfen: DNS-Eintrag (`nslookup`), Portweiterleitung 80 → `192.168.1.117`, Windows-Firewall.

## Port 80 vom Provider gesperrt / belegt → TLS-Challenge

Statt HTTP-Challenge (Port 80) kann Traefik das Zertifikat über Port 443 holen. In `traefik/docker-compose.yml`:

```yaml
      # - --certificatesresolvers.le.acme.httpchallenge.entrypoint=web
      - --certificatesresolvers.le.acme.tlschallenge=true
```

Danach `acme.json` löschen und Traefik neu erstellen (`docker compose up -d --force-recreate`).

## `Cannot issue for "....localhost": Domain name does not end with a valid public suffix`

`*.localhost` steht im selben Router wie die echte Domain. Lokalen Host in einen eigenen Router ohne `certresolver` auslagern (siehe [Lokal testen](lokal-testen.md)).

## `Error response from daemon: network proxy not found`

Das Docker-Netzwerk `proxy` existiert noch nicht. Einmalig anlegen und neu starten:

```bash
docker network create proxy
docker compose up -d
```

Das Netzwerk bleibt auch nach einem Neustart von Docker Desktop erhalten.

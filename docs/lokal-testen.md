# Lokal testen

[← zurück zur README](../README.md)

Ohne Domain, DNS und Port-Forwarding, direkt auf dem PC:

1. `docker network create proxy` (falls noch nicht passiert)
2. `traefik`, `whoami` und `website` mit `docker compose up -d` starten
3. Im Browser öffnen:
   - https://website.localhost
   - https://whoami.localhost

Die **Zertifikatswarnung ist normal**: Let's Encrypt stellt für `localhost` keine Zertifikate aus, Traefik nimmt ein selbstsigniertes. Im Browser „Trotzdem fortfahren“ klicken.

Dafür hat jede App einen zweiten Router `<app>-local` mit `tls=true` **ohne** `certresolver`. `*.localhost` darf nie im selben Router wie die echte Domain stehen, sonst lehnt Let's Encrypt das ganze Zertifikat ab (`Cannot issue for "...localhost"`).

`*.localhost` zeigt in Chrome/Edge/Firefox automatisch auf `127.0.0.1`, ein Eintrag in der `hosts`-Datei ist nicht nötig.

# DynDNS (Swisscom Internet-Box)

[← zurück zur README](../README.md)

Die öffentliche IP von Swisscom ändert sich gelegentlich. Damit die Domain trotzdem stimmt:

1. In der Internet-Box DynDNS aktivieren (Weboberfläche → Netzwerk / Einstellungen → DynDNS).
2. Hostname: `brandadrian.internet-box.ch`. Die Box aktualisiert diesen Namen automatisch bei jeder IP-Änderung.
3. Bei IONOS zeigt der CNAME `*.oryn` auf diesen Namen (siehe [IONOS DNS](dns-ionos.md)). Deshalb ist **kein A-Record mit fester IP** nötig.

```
Browser → website.oryn.brandadrian.com
        → CNAME brandadrian.internet-box.ch   (IONOS)
        → aktuelle öffentliche IP              (Swisscom DynDNS)
        → Internet-Box → 192.168.1.117:443 → Traefik
```

Prüfen:

```powershell
nslookup brandadrian.internet-box.ch          # muss deine aktuelle öffentliche IP liefern
nslookup website.oryn.brandadrian.com      # muss auf brandadrian.internet-box.ch zeigen
```

Öffentliche IP vergleichen: https://ifconfig.me

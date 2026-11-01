# IONOS DNS

[← zurück zur README](../README.md)

Apps laufen unter `<app>.homelab.brandadrian.com`. Dafür reicht **ein** Eintrag in der IONOS Domain-Verwaltung:

| Typ   | Host        | Zeigt auf |
|-------|-------------|-----------|
| CNAME | `*.homelab` | `brandadrian.internet-box.ch` |

`brandadrian.internet-box.ch` ist der DynDNS-Name der Swisscom Internet-Box und zeigt immer auf die aktuelle öffentliche IP. Kein A-Record und kein eigenes DynDNS nötig.

Durch die Wildcard funktioniert jede neue App (`website.homelab.brandadrian.com`, ...) ohne weiteren DNS-Eintrag.

Ein bestehender `A *`-Record auf der Hauptdomain stört nicht (`*.homelab` ist spezifischer), wird für das Homelab aber auch nicht gebraucht.

Prüfen: `nslookup website.homelab.brandadrian.com` → muss auf `brandadrian.internet-box.ch` bzw. deine öffentliche IP zeigen. DNS-Änderungen können bis zu 1 h (TTL) dauern.

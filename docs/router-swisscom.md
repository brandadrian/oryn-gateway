# Router (Swisscom Internet-Box)

[← zurück zur README](../README.md)

1. **DHCP-Reservierung:** Dem Windows-PC fest die IP `192.168.1.117` geben (in der Weboberfläche der Internet-Box, http://192.168.1.1).
2. **Portweiterleitung** in der Internet-Box, Protokoll TCP:

| Externer Port | Ziel | Interner Port |
|---------------|------|---------------|
| 80  | `192.168.1.117` | 80  |
| 443 | `192.168.1.117` | 443 |

Port 80 wird für die Let's-Encrypt-HTTP-Challenge gebraucht (und leitet danach auf HTTPS um).

# Ollama-API von aussen

[← zurück zur README](../README.md)

Direkter Zugriff auf die lokalen Modelle (z.B. aus VS Code, Skripten, anderen Apps), abgesichert mit einem API-Key (GUID). Ollama selbst hat keine Auth, deshalb steht nginx davor und prüft den Key.

- Öffentlich: https://ollama.homelab.brandadrian.com
- Lokal: https://ollama.localhost

```mermaid
flowchart LR
    client([Client mit API-Key]) --> traefik[Traefik · HTTPS] --> nginx["ollama-api (nginx)<br/>prüft Bearer-Key"]
    nginx -- "nur Abfragen und Inferenz" --> ollama[(Ollama nativ · GPU<br/>host.docker.internal:11434)]
```

## Dateien

```
ollama-api/docker-compose.yml                nginx + Traefik-Labels
ollama-api/templates/default.conf.template   Key-Prüfung und erlaubte Endpunkte
ollama-api/.env.example                      Vorlage für den Key
ollama-api/.env                              der echte Key (nicht im Git)
```

## Starten

`.env` aus `.env.example` erstellen, Key setzen (PowerShell: `[guid]::NewGuid()`), dann:

```bash
docker compose up -d
```

Key ändern: `.env` anpassen, dann `docker compose up -d --force-recreate`.

## Benutzen

Header `Authorization: Bearer <Key>` mitschicken.

```bash
curl https://ollama.homelab.brandadrian.com/api/tags -H "Authorization: Bearer <Key>"

curl https://ollama.homelab.brandadrian.com/api/chat -H "Authorization: Bearer <Key>" \
  -d '{"model":"gpt-oss:20b","messages":[{"role":"user","content":"Hallo"}]}'
```

PowerShell (`curl` ist dort ein Alias für `Invoke-WebRequest`, deshalb `curl.exe` oder):

```powershell
Invoke-RestMethod https://ollama.homelab.brandadrian.com/api/tags -Headers @{ Authorization = "Bearer <Key>" }
```

OpenAI-kompatible Clients: Base-URL `https://ollama.homelab.brandadrian.com/v1`, API-Key = der Key.

GitHub Copilot (VS Code): Chat: Manage Language Models → OpenAI Compatible → Base-URL `https://ollama.homelab.brandadrian.com/v1`, API-Key, Modell-ID (z.B. `qwen3.8:27b-agent`).

## Modelle

GPU: AMD Radeon AI PRO R9700 (32 GB). Alle Modelle laufen komplett auf der GPU (Messung 2026-11-05, 1,8k Token Prompt).

| Modell | Wofür | Prompt / Ausgabe | VRAM |
|--------|-------|------------------|------|
| `qwen3.8:27b-agent` | Agenten und Tool-Calls (Copilot): temperature 0.3, 128k Kontext. Modelfile: [ollama-api/Modelfile.qwen38-agent](../ollama-api/Modelfile.qwen38-agent) | 1000 / 41–65 t/s | 18 GB |
| `qwen3.8:27b` | Code und Analysen im Chat (gleiche Dateien wie `qwen3.8:27b-agent`) | 1000 / 41 t/s | 18 GB |
| `gpt-oss:20b` | Schneller Chat, schwierige Fragen | 3600 / 102 t/s | 13 GB |
| `qwen3.5:9b` | Schnell und klein, versteht Bilder | 3550 / 79 t/s | 8,6 GB |
| `qwen3-vl:8b-instruct` | Bilder und Rechnungen auslesen | | 6 GB |

`gemma3:27b` ist als Basis für [Saba](saba.md) installiert (`saba` nutzt dieselben Dateien).

Die `ollama`-CLI (`OLLAMA_HOST=...`) kann keinen Header mitschicken und funktioniert deshalb nicht von aussen.

## Erlaubte Endpunkte

| Ollama | OpenAI-kompatibel |
|--------|-------------------|
| `/api/chat`, `/api/generate`, `/api/embed`, `/api/embeddings` | `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings` |
| `/api/tags`, `/api/show`, `/api/ps`, `/api/version` | `/v1/models` |

`/api/models`, `/api/chat/completions` und `/api/completions` werden auf `/v1/...` umgeleitet, weil GitHub Copilot diese Pfade aufruft.

Gesperrt (404): `pull`, `push`, `create`, `copy`, `delete`, `blobs`. Modelle verwalten nur lokal oder über [Open WebUI](openwebui.md).

## Sicherheit

- Ohne gültigen Key: `401`.
- Nur über HTTPS erreichbar (Traefik leitet HTTP auf HTTPS um).
- Der Key wird nicht an Ollama weitergegeben.
- Key nie ins Repo schreiben. Bei Verdacht neuen Key erzeugen.

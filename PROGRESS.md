# PROGRESS - TeamChat

> Journal d'avancement. Tenu a jour a la fin de chaque sprint.
> `CLAUDE.md` est la source de verite ; ce fichier ne coche que ce qui est reellement termine.

## Etat des sprints

- [x] **Sprint 0 : Initialisation** - termine
- [ ] Sprint 1 : Authentification, roles, annuaire
- [ ] Sprint 2 : Conversations privees et temps reel
- [ ] Sprint 3 : Groupes, modification, suppression, non lus
- [ ] Sprint 4 : Fichiers et messages vocaux
- [ ] Sprint 5 : Appels audio, video et partage d'ecran
- [ ] Sprint 6 : Dashboard admin, qualite, deploiement

---

## Sprint 0 : Initialisation

### Fait

- Depot Git initialise (branche `main`), `.gitignore` (.NET + Node), `README.md` minimal.
- Depot publie sur GitHub : `https://github.com/ademhmercha/teamchat` (remote `origin`, branche `main`).
- `PROGRESS.md`, `.env.example` (variables de la section 10 du cahier des charges).
- Solution .NET `backend/TeamChat.sln` (.NET 10 LTS) :
  - `src/TeamChat.Domain`, `src/TeamChat.Application`,
    `src/TeamChat.Infrastructure`, `src/TeamChat.Api`
  - `tests/TeamChat.UnitTests`, `tests/TeamChat.IntegrationTests`
  - References conformes a la Clean Architecture (voir Decisions).
- Interface `ICacheService` (Application) + implementation `HybridCacheService`
  (Infrastructure) : memoire L1 par defaut, Redis L2 si `Redis__ConnectionString`,
  enregistree dans `Program.cs` via `AddInfrastructure`.
- Application Angular 21 (standalone, signals) dans `frontend/`.
- `docker-compose.yml` : PostgreSQL (volume + healthcheck) et Redis (profil `redis`).
- CI GitHub Actions : `backend-ci.yml` (build + tests), `frontend-ci.yml` (build).

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D1 | Backend sur **.NET 10 LTS** (SDK 10.0.401 installe) | Conforme au cahier des charges ; seule version LTS disponible. Le SDK 9.0.102 etait present mais STS/hors support. |
| D2 | **HybridCache natif** (pas de fallback `IMemoryCache`+`IDistributedCache`) | .NET 10 fournit `Microsoft.Extensions.Caching.Hybrid` ; Redis active en L2 via `AddStackExchangeRedisCache`. |
| D3 | Frontend **Angular 21.2.26** | Derniere version compatible avec Node 20.20.2 (Angular 22 exige Node >= 22.22.3). Composants standalone + signals. |
| D4 | References projets : Application -> Domain ; Infrastructure -> Application ; Api -> Application + Infrastructure ; tests -> projets concernes | Clean Architecture, aucune dependance inverse. |
| D5 | `ICacheService` avec `GetOrCreateAsync(key, factory, ttl)`, `RemoveAsync(key)`, `RemoveByTagAsync(tag)` utilisant le type `ValueTask` | Aligne sur l'API `HybridCache`. |
| D6 | Cles de cache prefixees/versionnees (`tc:v1:...`) centralisees dans `CacheKeys` | Regle 9 et section 8 bis. |
| D7 | Pas de `global.json` de pinning du SDK | CI via `setup-dotnet` 10.0.x ; evite les conflits de version de patch. |
| D8 | Branche `main` initiale ; protection de branche a activer cote GitHub | Aucun remote configure pour l'instant (a faire a la creation du depot distant). |

### Problemes connus / non verifie

- [ ] **Protection de branche `main`** non configuree : le depot distant existe, mais la protection
      doit etre activee manuellement dans les reglages GitHub (pas de `gh`/token disponible ici).
- [ ] **CI GitHub Actions** : declenchee par le push sur `main`, resultat non confirme localement.
- [ ] `docker-compose` ne contient pas encore les services `api` et `coturn` (prevus sprints 5 et 6).
- [ ] Tests Angular (`npm test`) non executes : necessite Chrome headless, non requis pour le Sprint 0
      (`ng build` suffit). A brancher au Sprint 1.
- [ ] Pas de `Dockerfile` backend/frontend (prevu Sprint 6).
- [ ] `appsettings.Development.json` volontairement ignore par git (peut contenir des secrets locaux) ;
      l'application demarre avec `appsettings.json` + variables d'environnement.

### Resultats de verification (reels, executes le 2026-10-09)

Environnement : Windows, SDK .NET `10.0.401`, Node `v20.20.2`, npm `10.9.8`,
Angular CLI local `21.2.26`, Docker `29.3.1` / Compose `v5.1.1`.

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln -c Release` | OK, **0 erreur / 0 avertissement** |
| Tests backend | `dotnet test backend/TeamChat.sln -c Release --no-build` | OK, **5/5** (4 unitaires + 1 integration) |
| Build frontend | `ng build --configuration production` (dans `frontend/`) | OK, bundle initial ~214 kB |
| Docker config | `docker compose config --quiet` | OK |
| Docker services | `docker compose up -d postgres` + `--profile redis up -d redis` | Postgres **healthy**, Redis **healthy** |
| API au runtime | `dotnet run ... --no-launch-profile` puis `GET /api/health` | `{"status":"ok","service":"TeamChat.Api","cache":"HybridCacheService"}` |
| OpenAPI (dev) | `GET /openapi/v1.json` | HTTP 200 |
| Cache L2 Redis | API avec `Redis__ConnectionString=localhost:6379` + `GET /api/health` | cle `tc:v1:health` presente dans Redis (`redis-cli --scan`) |

Notes : le warning `Failed to determine the https port` apparait uniquement en execution HTTP locale
sans HTTPS configure (normal a ce stade). Les conteneurs Postgres et Redis sont laisses demarres ;
arret via `docker compose --profile redis down`.

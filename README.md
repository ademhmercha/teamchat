# TeamChat

Messagerie interne pour les employes d'une societe : annuaire, conversations privees,
groupes (crees par l'admin), messages temps reel, fichiers, vocaux et appels WebRTC 1-a-1.

Le cahier des charges complet et la source de verite du projet sont dans [`CLAUDE.md`](./CLAUDE.md).
L'avancement est suivi dans [`PROGRESS.md`](./PROGRESS.md).

## Stack

- **Backend** : ASP.NET Core Web API (.NET 10 LTS) + SignalR, Clean Architecture
- **Base de donnees** : PostgreSQL + EF Core (Npgsql)
- **Auth** : ASP.NET Core Identity + JWT (access + refresh avec rotation) + Google
- **Frontend** : Angular 21 (composants standalone, signals)
- **Appels** : WebRTC (signalisation via SignalR), STUN/TURN
- **Cache** : HybridCache (memoire L1 par defaut, Redis L2 optionnel). Redis active aussi le backplane SignalR (multi-instances)
- **Tests** : xUnit, Moq, FluentAssertions, Testcontainers ; Jasmine/Jest cote frontend
- **DevOps** : Docker, docker-compose, GitHub Actions

## Prerequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` >= 10)
- [Node.js](https://nodejs.org/) 20.19+ (Angular 21)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) avec Docker Compose
- Git

## Structure

```
teamchat/
├── CLAUDE.md              # cahier des charges (source de verite)
├── PROGRESS.md            # journal d'avancement
├── docker-compose.yml     # PostgreSQL (+ Redis en profil optionnel)
├── .env.example           # variables d'environnement (copier en .env)
├── .github/workflows/     # CI backend + frontend
├── backend/
│   ├── TeamChat.sln
│   ├── src/               # Domain, Application, Infrastructure, Api
│   └── tests/             # UnitTests, IntegrationTests
└── frontend/              # application Angular
```

## Lancer en local

```bash
# 1. Base de donnees (+ Redis optionnel)
docker compose up -d postgres
docker compose --profile redis up -d         # ajoute Redis (cache L2)

# 2. Backend
dotnet run --project backend/src/TeamChat.Api

# 3. Frontend
cd frontend && npm install && npm start      # http://localhost:4200
```

## Tests et build

```bash
dotnet build backend/TeamChat.sln
dotnet test  backend/TeamChat.sln
cd frontend && npm run build
```

## Variables d'environnement

Voir [`.env.example`](./.env.example). Aucun secret ne doit etre committe :
utiliser `.env` (ignore par git) ou `dotnet user-secrets` en developpement.

## Administration

L'espace `/admin` (role `Admin`) propose :
- un **tableau de bord** : utilisateurs actifs, messages sur 7 jours (graphique), groupes les plus actifs ;
- la **gestion des utilisateurs** : creation avec invitation par email, changement de role, activation/desactivation ;
- la **gestion des groupes** : creation, membres, archivage.

La desactivation d'un compte revoque les tokens, invalide les caches (profil, annuaire, appartenances)
et ferme les sessions temps reel (SignalR) via un evenement `ForceLogout`.

## Appels WebRTC (STUN / TURN)

Les appels audio/video utilisent WebRTC. La signalisation passe par SignalR ; l'API fournit
la liste des serveurs ICE via `GET /api/calls/ice-servers`.

- **STUN** (par defaut) : suffit sur la plupart des reseaux. Le navigateur decouvre son
  adresse publique via `Turn__StunUrls` (defaut `stun:stun.l.google.com:19302`).
- **TURN** (recommande en production) : necessaire derriere un NAT symetrique (reseau mobile,
  entreprise stricte). L'API ne renvoie TURN que si `Turn__Urls` **et** `Turn__Secret` sont
  definis, avec des identifiants **temporaires** generes par requete (HMAC-SHA1, mecanisme
  "TURN REST API" de coturn) — aucun secret statique n'est expose au client.

Lancer un serveur TURN local avec le profil Compose `turn` :

```bash
# TURN_URLS = URL joignable par le navigateur ; TURN_SECRET doit valoir Turn__Secret.
TURN_URLS=turn:localhost:3478 TURN_SECRET=un-secret-long \
  docker compose --profile turn up -d
```

Variables associees (voir [`.env.example`](./.env.example)) :

| Variable | Role |
|---|---|
| `Turn__StunUrls` | URLs STUN (separees par des virgules) |
| `Turn__Urls` | URLs TURN distribuees au navigateur (vide = pas de TURN) |
| `Turn__Secret` | Secret partage avec coturn (`--static-auth-secret`) |
| `TURN_SECRET` / `TURN_REALM` / `TURN_PORT` / `TURN_URLS` | Reglages du service `coturn` du compose |

En production, herberger coturn sur un **VPS** (les hebergeurs web classiques ne gerent pas
l'UDP) et exposer les ports 3478 (UDP/TCP) + la plage de relais UDP (49160-49200 par defaut).

## Deploiement (fichiers prepares)

Aucun deploiement automatique n'est effectue par le depot ; les fichiers suivants servent de base.

- **Frontend (Vercel)** : [`frontend/vercel.json`](./frontend/vercel.json) configure le build Angular
  et la reecriture SPA vers `index.html`. Renseigner `apiBaseUrl` dans `frontend/src/environments/`.
- **Backend (conteneur)** : [`backend/Dockerfile`](./backend/Dockerfile) construit une image runtime
  (.NET 10, port 8080). `docker compose --profile app up -d --build` lance l'API avec PostgreSQL
  (profil `app` dans [`docker-compose.yml`](./docker-compose.yml)).
- **Variables requises en production** : `ConnectionStrings__Default`, `Jwt__Secret`, `Cors__AllowedOrigins`,
  `Admin__SeedEmail`/`Admin__SeedPassword`, et optionnellement `Redis__ConnectionString` (cache L2 + backplane),
  `Turn__*` (appels via TURN) et `Storage__*` (S3).

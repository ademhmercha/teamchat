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
- **Cache** : HybridCache (memoire L1 par defaut, Redis L2 optionnel)
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

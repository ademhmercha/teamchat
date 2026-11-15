# TeamChat

Messagerie interne pour les employes d'une societe : annuaire, conversations privees,
groupes (crees par l'admin), messages temps reel, fichiers, vocaux et appels WebRTC 1-a-1.

## Stack

- **Backend** : ASP.NET Core Web API (.NET 10 LTS) + SignalR, Clean Architecture
- **Base de donnees** : PostgreSQL + EF Core (Npgsql)
- **Auth** : ASP.NET Core Identity + JWT (access + refresh avec rotation) + Google
- **Frontend** : Angular 21 (composants standalone, signals)
- **Appels** : WebRTC (signalisation via SignalR), STUN/TURN
- **Cache** : HybridCache (memoire L1 par defaut, Redis L2 optionnel). Redis active aussi le backplane SignalR (multi-instances)
- **Tests** : xUnit, Moq, Testcontainers (PostgreSQL) ; Vitest cote frontend
- **DevOps** : Docker, docker-compose
- **Hebergement AWS** : CloudFront + S3 (frontend), EC2 + ECR (API), RDS PostgreSQL, S3 (fichiers), SSM Parameter Store, CloudWatch
- **CI/CD AWS** : CodePipeline, CodeBuild (tests + build, verification des PR), CodeDeploy. Voir [`aws/README.md`](./aws/README.md)

## Prerequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` >= 10)
- [Node.js](https://nodejs.org/) 20.19+ (Angular 21)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) avec Docker Compose
- Git

## Structure

```
teamchat/
├── docker-compose.yml     # PostgreSQL (+ Redis en profil optionnel)
├── .env.example           # variables d'environnement (copier en .env)
├── buildspec.yml          # build du pipeline (CodeBuild)
├── infra/                 # infrastructure AWS (Terraform)
├── aws/                   # scripts de deploiement, buildspec des PR, fonction CloudFront
├── docs/                  # restauration, securite, recette
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

## Deploiement (AWS)

L'application est hebergee sur AWS, region `eu-north-1`, entierement decrite en Terraform (`infra/`) :
CloudFront (frontend S3 + `/api/*` et `/hubs/*` vers l'EC2), EC2 + Docker (API), RDS PostgreSQL, S3 (fichiers),
SES (emails), SSM (secrets), CodePipeline/CodeBuild/CodeDeploy (CI/CD sur `main`).

- **Documentation** : [`aws/README.md`](./aws/README.md) (architecture, commandes, CI/CD, supervision, arret nocturne),
  [`docs/RESTORE.md`](./docs/RESTORE.md) (restauration), [`docs/SECURITY.md`](./docs/SECURITY.md) (revue IAM),
  [`docs/RECETTE.md`](./docs/RECETTE.md) (checklist de recette manuelle).
- **URL de la demo** : `terraform output cloudfront_url` (dans `infra/`). La demo dort de 23h a 8h (UTC+1).
- **Backend en conteneur** : [`backend/Dockerfile`](./backend/Dockerfile) (.NET 10, port 8080) ;
  `docker compose --profile app up -d --build` lance l'API avec PostgreSQL en local.
- **Variables requises en production** (parametres SSM `/teamchat/prod/`, crees par Terraform) : `ConnectionStrings__Default`,
  `Jwt__*`, `Cors__AllowedOrigins`, `App__PublicUrl`, `Email__*`, `Storage__*`, `Admin__Seed*`, `Google__ClientId`, `Turn__*`.

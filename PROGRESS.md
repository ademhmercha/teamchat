# PROGRESS - TeamChat

> Journal d'avancement. Tenu a jour a la fin de chaque sprint.
> `CLAUDE.md` est la source de verite ; ce fichier ne coche que ce qui est reellement termine.

## Etat des sprints

- [x] **Sprint 0 : Initialisation** - termine
- [x] **Sprint 1 : Authentification, roles, annuaire** - termine et pousse sur `sprint-1-auth` (PR manuelle a creer)
- [x] **Sprint 2 : Conversations privees et temps reel** - termine (en attente de validation utilisateur)
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
- [x] **CI GitHub Actions** verte sur le push `main` : Backend CI #1 et Frontend CI #1 (commit `94fb050`).
      Avertissement non bloquant : actions `checkout@v4`/`setup-node@v4`/`setup-dotnet@v4` executees
      sous Node 24 (deprecation Node 20) ; migration possible vers les versions d'action plus recentes.
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

---

## Sprint 1 : Authentification, roles, annuaire

### Fait

**Backend**
- Entites `AppUser` (herite de `IdentityUser<Guid>`) et `RefreshToken` ; `TeamChatDbContext`
  (Identity + refresh tokens) ; migration `InitialIdentity` ; seed des roles et du compte admin
  via configuration (`Admin__SeedEmail` / `Admin__SeedPassword`, jamais en dur).
- Identity : politique de mot de passe, verrouillage apres 5 echecs (5 min). JWT access token
  (15 min) ; refresh token avec **rotation**, stocke **hache** (SHA-256), envoye en cookie
  `HttpOnly` `Path=/api/auth` `SameSite=Lax` (`Secure` hors dev).
- Endpoints `auth/*` : `login`, `google`, `refresh`, `logout`, `accept-invite`, `me`.
- Connexion Google : validation de l'ID token cote serveur (`Google.Apis.Auth`, audience = Client ID),
  refus des comptes absents ou inactifs (pas d'auto-inscription).
- Creation d'utilisateur par l'admin + invitation (`POST /api/admin/users`) : lien avec token,
  `DevEmailSender` journalise le lien en dev (pas d'envoi SMTP reel).
- `PATCH /api/admin/users/{id}` : nom, activation (revocation de tous les refresh tokens) et **role**.
- `GET /api/users` : recherche (nom/email) + pagination, resultats **mis en cache 60 s**
  (tag `tc:v1:directory`), invalidation a chaque creation/modification/desactivation.
- Profil minimal (role, `IsActive`, nom, avatar) **mis en cache 1 min**, revalide a chaque requete
  authentifiee (`JwtBearerEvents.OnTokenValidated`), invalide immediatement a la desactivation.
- Rate limiting sur `login` et `google` (fenetre fixe par IP).
- Erreurs au format **ProblemDetails** (handler global + filtre de validation FluentValidation).

**Frontend** (Angular 21, standalone + signals)
- Page de connexion (email/mot de passe + bouton Google rendu seulement si un Client ID est configure).
- `AuthService` (signals), `authInterceptor` (ajout du Bearer, `withCredentials`, **refresh
  automatique sur 401**), guards `authGuard` / `adminGuard` / `guestGuard`.
- Layout principal : barre laterale (navigation + section Admin), barre superieure (utilisateur,
  deconnexion), zone centrale `router-outlet`.
- Page annuaire : liste des utilisateurs actifs, recherche avec **debounce 300 ms**, pagination.
- Environnements `environment.ts` / `environment.production.ts` + `fileReplacements` (prod).

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D9 | Access token **non persiste** cote client (memoire) ; restauration de session via `POST /auth/refresh` (cookie) au chargement, dans les guards | Evite d'exposer le JWT au stockage navigateur ; le refresh token HttpOnly reste la seule persistance. |
| D10 | Frontend dev pointe directement sur `http://localhost:5285` (CORS + `withCredentials`) plutot qu'un proxy | Valide la config CORS/credentials reelle. `environment.production.ts` laisse vide (URL relative) jusqu'au deploiement (Sprint 6). |
| D11 | Bouton Google affiche uniquement si `environment.googleClientId` est renseigne | Pas de Client ID en dev ; evite un bouton casse. |
| D12 | Abstraction `IIdentityService` (Application) implementee par `UserManager` (Infrastructure) | Respect de la Clean Architecture : aucune dependance Identity dans l'Application/Domain. |
| D13 | Rotation du refresh token + detection de reutilisation (revocation de toute la famille) | Renforce la securite (section 8). |
| D14 | Cookie refresh `SameSite=Lax` | `localhost:4200` -> `localhost:5285` reste *same-site* en dev. En prod cross-site, prevoir sous-domaines `app.` / `api.` ou reecriture (piege Vercel, section 8). |

### Problemes connus / non verifie

- [ ] **Google** non teste de bout en bout : aucun Client ID configure, le bouton est donc masque en dev.
- [ ] **Tests Angular** (`npm test`) toujours non executes (necessite un navigateur headless) ;
      seul `ng build --configuration production` a ete execute. A brancher.
- [ ] Verrouillage de compte couvert par un test d'integration mais pas re-verifie manuellement.
- [ ] `environment.production.ts` : `apiBaseUrl` vide a completer lors du deploiement (Sprint 6).
- [ ] Pas de mecanisme de revocation des access tokens deja emis (duree de vie 15 min) : la
      desactivation est verifiee via le cache profil a chaque requete (immediat), l'access token
      lui-meme n'est pas rejoue.

### Resultats de verification (reels, executes le 2026-10-10)

Environnement : Windows, SDK .NET `10.0.401`, Node `v20.20.2`, npm `10.9.8`, Angular `21.2.26`,
PostgreSQL 16 (conteneur `teamchat-postgres`).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln -c Debug` | OK, **0 erreur / 0 avertissement** |
| Tests backend | `dotnet test backend/TeamChat.sln --no-build` | OK, **20/20** (4 unitaires + 16 integration) |
| Build frontend | `npm run build -- --configuration production` (dans `frontend/`) | OK, total initial ~256 kB |
| API au runtime | `dotnet run` (env vars) puis `GET /api/health` | `{"status":"ok","service":"TeamChat.Api","cache":"HybridCacheService"}` |
| Login admin | `POST /api/auth/login` | 200, role=Admin, cookie `tc_refresh` emis |
| Refresh | `POST /api/auth/refresh` (cookie) | 200, nouveau access token |
| `me` | `GET /api/auth/me` | 200, email + role |
| Annuaire | `GET /api/users?search=admin` | 200, `totalCount=1` |
| Creation utilisateur | `POST /api/admin/users` | **201** + `Location: /api/users/{id}` (bug `CreatedAtAction` corrige) |
| Doublon email | `POST /api/admin/users` (meme email) | 409 |
| Role / email invalide | `POST /api/admin/users` | 400 (FluentValidation) |
| Route admin sans token | `POST /api/admin/users` | 401 |
| Invitation | create -> lien journalise -> `POST /api/auth/accept-invite` -> login | OK, role=User |
| Controle de role | `POST /api/admin/users` avec token User | **403** |
| Desactivation | `PATCH /api/admin/users/{id}` puis `GET /api/auth/me` (ancien JWT) | 200 puis **401** (cache profil invalide immediatement) |

Notes : l'API a ete lancee sur `http://localhost:5285` avec la base PostgreSQL locale ; la creation
d'un utilisateur initialise son mot de passe uniquement via le flux d'invitation (aucun mot de passe
en clair cote admin). Le fichier `backend/.config/dotnet-tools.json` (outil `dotnet-ef`) est ajoute au suivi.

---

## Sprint 2 : Conversations privees et temps reel

### Fait

**Backend**
- Entites `Conversation`, `ConversationParticipant`, `Message` (avec `Sequence` bigint identity) et
  enums `ConversationType` / `MessageType` ; migrations `AddConversations` + `AddMessageSequence`
  (index unique `DirectKey`, index `(ConversationId, Sequence)`) appliquees sur la base de dev.
- Application : DTOs (`ConversationDto`, `ConversationListItemDto`, `MessageDto`, `MessageHistoryResult`,
  `CreateDirectConversationRequest`, `SendMessageRequest`), `IConversationService`, `IConversationQueries`,
  `IMembershipService`, `IPresenceTracker`, `ConversationService`, validators ; `BadRequestException`
  ajoutee et mappee en **400**.
- Infrastructure : `ConversationQueries` (direct idempotent sous rafle `DirectKey`, historique pagine,
  liste avec dernier message), `MembershipService` (cache 5 min + invalidation), `PresenceTracker`
  (en memoire, multi-onglets).
- API : `GET /api/conversations`, `POST /api/conversations/direct`, `GET /api/conversations/{id}/messages`
  (`before` / `limit` 1-100) ; hub SignalR `/hubs/chat` (`SendMessage`, `StartTyping`, `StopTyping`,
  events `ReceiveMessage`, `UserTyping`, `UserStoppedTyping`, `PresenceChanged`, `PresenceSnapshot`) ;
  `app.UseWebSockets()`, auth JWT du hub via `access_token` en query (`OnMessageReceived`), rate
  limiting « chat » (defaut 120/min/IP).
- Tests `ConversationsEndpointTests` (7) + `ChatHubTests` (4) ; **31/31 verts** (4 unitaires + 27 integration).

**Frontend** (Angular 21, standalone + signals)
- `@microsoft/signalr` installe ; `ChatService` : connexion avec access token, **reconnexion
  automatique**, rejoint les groupes cote serveur a chaque `OnConnected` (resubscribe implicite),
  signals de presence et de typing (expiration 2,5 s), observable `receiveMessage$`.
- Page conversations : liste des echanges (1er message affiche + heure), pastille de presence en
  surimpression, bouton « Nouvelle conversation » (recherche utilisateur + creation directe
  idempotente, redirection vers le fil).
- Fil de discussion `/conversations/:id` : chargement des messages les plus recents, **scroll infini
  ascendant** (curseur `Sequence`), reception temps reel avec deduplication par `id`, envoi via le hub
  (echo serveur), indicateur « X ecrit… », presence de l'interlocuteur, auto-scroll si l'utilisateur
  est en bas.

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D15 | Les messages sont envoyes **uniquement via le hub** (pas d'endpoint REST de send) | Diffusion temps reel garantie ; le hub fait office de bus. |
| D16 | Presence **en memoire par instance** (`IPresenceTracker` singleton, multi-onglets dedoubles) | Un seul processus en dev ; a revisiter si scale-out (Sprint 6). |
| D17 | Curseur d'historique = **`Message.Sequence`** (bigint identity, ordre global) | Le couple `(CreatedAt, Guid.CompareTo)` est fragile (ordre .NET vs Postgres, egalites horodatees). |
| D18 | L'envoi au groupe conversation se fait **y compris a l'expediteur** (echo serveur) | Un seul chemin de rendu ; le client deduplique par `message.id`. |
| D19 | Membership **mis en cache 5 min** avec tags `tc:v1` et invalidation a la creation d'une conversation | Envol categorie meme si faible ; evite 1 requete/s/message. |
| D20 | Conversation directe **idempotente** via `DirectKey` `d:{min}:{max}` + gestion de course (`DbUpdateException` -> refetch) | Deux onglets peuvent cliquer simultanement sur « Nouvelle conversation ». |
| D21 | Non lus (`LastReadMessageId`), groupes (`Group`) et fichiers/vocal reportes **Sprint 3 / Sprint 4** | Perimetre du Sprint 2 strict (prive + temps reel + presence). |

### Problemes connus / non verifie

- [ ] **Sprint 1 non fusionne** : `sprint-2-conversations` est basse sur `sprint-1-auth` ; a revoir
      au moment des PR (base/rebase) et de la creation manuelle des PR (pas de `gh`).
- [ ] **Test manuel navigateur (2 onglets)** non realisable ici (pas de navigateur automatise) : la
      verification runtime a ete faite au niveau API avec le client `@microsoft/signalr` reel sur
      WebSockets (equivalent fonctionnel). A re-tester visuellement par l'utilisateur.
- [ ] Presence **par instance** : deux instances d'API = presences incoherentes (accepte en dev).
- [ ] Les echecs d'envoi (`send()`) sont silencieux cote UI (pas encore de gestion d'erreur visible).
- [ ] Message non-texte rendu « Contenu non texte » cote UI (pieces jointes : Sprint 4).

### Resultats de verification (reels, executes le 2026-10-10)

Environnement : identique au Sprint 1 (SDK .NET 10.0.401, Node v20.20.2, PostgreSQL conteneur).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln -c Debug` | OK, **0 erreur / 0 avertissement** |
| Tests backend | `dotnet test backend/TeamChat.sln --no-build` | OK, **31/31** (4 unitaires + 27 integration) |
| Build frontend | `npm run build` (dans `frontend/`) | OK, chunk `thread` + `conversations-list` generes |
| Runtime realtime | script node (`@microsoft/signalr` 10.0.12, transport **WebSockets** via `ws`) contre `localhost:5285` | **0 echec** : invitation + login A/B, conversation directe + idempotence, connexion WS 2 clients, `PresenceChanged`, `ReceiveMessage`, historique, liste |

Note : le script de verification runtime est en dehors du depot
(`%TEMP%/opencode/teamchat-realtime-check.cjs`, non committe). L'API tournait en arriere-plan sur
`http://localhost:5285` avec les logs rediriges pour lire les liens d'invitation.

# PROGRESS - TeamChat

> Journal d'avancement. Tenu a jour a la fin de chaque sprint.
> `CLAUDE.md` est la source de verite ; ce fichier ne coche que ce qui est reellement termine.

## Etat des sprints

- [x] **Sprint 0 : Initialisation** - termine
- [x] **Sprint 1 : Authentification, roles, annuaire** - termine et pousse sur `sprint-1-auth` (PR manuelle a creer)
- [x] **Sprint 2 : Conversations privees et temps reel** - termine et valide
- [x] **Sprint 3 : Groupes, modification, suppression, non lus** - termine, valide et pousse sur `sprint-3-groups` (PR manuelle a creer)
- [x] **Sprint 4 : Fichiers et messages vocaux** - termine, valide et fusionne sur `main` (branche `sprint-4-files`)
- [x] **Sprint 5 : Appels audio, video et partage d'ecran** - termine, valide et fusionne sur `main` (branche `sprint-5-calls`)
- [~] **Sprint 6 : Dashboard admin, qualite, deploiement** - en cours (backend + frontend realises, en attente de validation)

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

---

## Sprint 3 : Groupes, modification, suppression, non lus

### Fait

**Backend**
- `Conversation.ArchivedAt` (nullable) : archivage **logique**. Migration `AddGroupsAndUnread`
  (ajoute uniquement `ArchivedAt` ; `ConversationParticipant.LastReadMessageId` existait deja) appliquee
  sur la base de dev.
- Non lus : `ConversationParticipant.LastReadMessageId` sert de curseur. `ConversationListItemDto`
  expose `ParticipantCount` et `UnreadCount`. Un message compte comme non lu si
  `Sequence > seqOf(LastReadMessageId)` **et** `SenderId != userId` **et** non supprime.
- `IConversationService` etendu : `EditMessageAsync`, `DeleteMessageAsync`, `MarkAsReadAsync`
  (auteur uniquement, message supprime non modifiable, idempotent). `IConversationQueries` etendu :
  `FindMessageAsync`, `UpdateMessageAsync`, `SoftDeleteMessageAsync`, `GetLastMessageIdAsync`,
  `SetLastReadAsync`. `ConversationQueries` reecrit : liste des conversations incluant les groupes,
  excluant les conversations archivees, avec `UnreadCount`/`ParticipantCount` (une seule requete
  agregee) ; `GetMemberConversationIdsAsync` exclut les archives.
- `MembershipService` : une conversation archivee n'est **jamais** un membre (acces refuse).
- **Groupes (admin)** : `IAdminGroupService`/`IAdminGroupQueries` + `AdminGroupService` (validators
  FluentValidation) ; creation par un admin (createur auto-membre, **>= 2 membres**, utilisateurs
  existants verifies), remplacement complet des membres (`PUT`, **>= 1 membre**), archivage. Chaque
  mutation **invalide le cache d'appartenance** de la conversation. Endpoints
  `GET/POST /api/admin/groups`, `PUT /api/admin/groups/{id}/members`, `DELETE /api/admin/groups/{id}`
  (role `Admin` requis).
- Temps reel : `IChatClient` + `ChatHub` etendus (`EditMessage`, `DeleteMessage`, `MarkAsRead` ;
  events `MessageEdited`, `MessageDeleted`, `AddedToGroup`, `ConversationUpdated`). `ChatGroups`
  (helper de nommage) partage entre hub et controllers. `AdminGroupsController` synchronise les groupes
  SignalR via `IHubContext<ChatHub, IChatClient>` + `IPresenceTracker.GetConnectionIds` (ajout/retrait
  de connexions + notifications). Endpoint `POST /api/conversations/{id}/read` (body optionnel
  `{ messageId }`, `null` = dernier message).
- Tests `AdminGroupsEndpointTests` (4), `MessageEditDeleteTests` (2), `UnreadTests` (1) ;
  **4 unitaires + 34 integration = 38/38 verts**.

**Frontend** (Angular 21, standalone + signals)
- `ChatService` : nouveaux flux `messageEdited$`, `messageDeleted$`, `addedToGroup$`,
  `conversationUpdated$` ; invocation `editMessage`/`deleteMessage`.
- Fil de discussion : menu **Modifier/Supprimer** sur ses propres messages (edition en ligne),
  mise a jour live via les events (message modifie/supprime), **marquage comme lu** (REST
  `POST .../read`) au chargement et a la reception ; en-tete adapte aux **groupes** (nom + nombre
  de membres, plus de pastille de presence).
- Liste des conversations : **badge de non lus**, rendu des groupes (nom du groupe), rafraichissement
  a chaque `ConversationUpdated`/`AddedToGroup`.
- Espace admin : page `/admin/groups` (liste, creation avec selection de membres, edition des
  membres, archivage), lien dans la navigation + carte sur le tableau de bord.
- `AdminGroupsService`, modeles `GroupDto`/`GroupMemberDto`, `conversation.models.ts` etendu.

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D22 | Archivage **logique** (`ArchivedAt`), pas de suppression physique | Conserve l'historique et les references ; reversible. |
| D23 | Non lus bases sur `LastReadMessageId` (curseur) plutot qu'un compteur denormalise | Un seul champ, coherent avec la pagination par `Sequence` ; pas de derive de compteur. |
| D24 | Edition/suppression de message **via le hub uniquement** (pas de REST) | Meme chemin temps reel que l'envoi ; broadcast garanti aux membres connectes. |
| D25 | Groupes **crees et geres par les admins** (`Roles.Admin`) ; `PUT members` = remplacement complet | Pas de creation de groupe en libre-service dans ce sprint (section 9). |
| D26 | Synchronisation des groupes SignalR cote **controller** (via `IHubContext`) et non dans le service | Le service reste agnostique du transport ; le controller connait presence + hub. |
| D27 | Marquage comme lu cote front via **REST** (`POST .../read`) | Simplifie le cas « dernier message » (messageId null) ; la methode hub `MarkAsRead` existe pour les clients temps reel. |

### Problemes connus / non verifie

- [ ] **Test manuel navigateur (2 onglets)** non realisable ici : verification faite par tests
      d'integration (hub SignalR reel en LongPolling). A re-tester visuellement par l'utilisateur.
- [ ] **Verification runtime API (script node)** du Sprint 3 non encore rejouee (couverture assuree par
      les tests d'integration ; un passage bout-en-bout reste souhaitable).
- [ ] `MarkAsRead` du hub prend un `Guid messageId` non nullable (le cas « tout lire » passe par REST).
- [ ] Test de presence historique rendu **deterministe** : l'assertion verifie desormais que l'appelant
      figure dans son propre snapshot (l'ancienne assertion attendait un utilisateur connecte plus tard,
      source de flakiness).
- [ ] Google login toujours non teste de bout en bout (pas de Client ID).
- [ ] Suppression de message **logique** cote UI : le contenu original n'est pas recuperable.

### Resultats de verification (reels, executes le 2026-10-11)

Environnement : identique aux sprints precedents (SDK .NET 10.0.401, Node v20.20.2, PostgreSQL conteneur).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln -c Debug` | OK, **0 erreur / 0 avertissement** |
| Migration | `dotnet ef migrations add AddGroupsAndUnread` + `database update` | OK (colonne `ArchivedAt`), base de dev a jour |
| Tests backend | `dotnet test backend/TeamChat.sln -c Debug` | OK, **38/38** (4 unitaires + 34 integration) |
| Build frontend | `npm run build` (dans `frontend/`) | OK, chunks `admin-groups` + `thread` + `conversations-list` generes |

---

## Sprint 4 : Fichiers et messages vocaux

### Fait

**Backend**
- Entite `Attachment` (Id, `StorageKey` GUID aleatoire **jamais** derive du nom d'origine,
  `OriginalFileName`, `ContentType`, `SizeBytes`, `DurationSec?`, `UploadedByUserId`, `CreatedAt`) ;
  `Message.AttachmentId` (nullable) ; index unique sur `StorageKey`. Migration `AddAttachments`
  (`20261011192107`) appliquee sur la base de dev.
- Detection du **type reel par signature binaire** (`FileSignatureDetector`, magic bytes) : images
  (png/jpeg/gif/webp), PDF, ZIP/OOXML, audio (webm/ogg/mp4/mpeg), texte brut ; **rejet explicite**
  des executables (`MZ`) et binaires ELF ; tout type inconnu -> `BadRequestException`
  « Type de fichier non autorise. »
- Abstraction `IFileStorage` (`SaveAsync`, `OpenReadAsync`, `DeleteAsync`) : `LocalFileStorage`
  (dev, ecriture fichier temporaire + move atomique, garde-fou de chemin) ; `S3FileStorage`
  (stockage objet, `AmazonS3Config` `ServiceURL`/`ForcePathStyle` + `BasicAWSCredentials`), selection
  par `Storage:Provider`. Lib `AWSSDK.S3` 4.0.104.2.
- `AttachmentService` : controle de taille sur les **octets reels** (`Storage:MaxFileSizeBytes`,
  `Storage:MaxVoiceSizeBytes`), persistance de la piece jointe + du message (`MessageType`
  Image/Voice/File) avec `Sequence`, visibilite verifiee via `MembershipService`.
- `ConversationQueries.Full` projette la piece jointe ; `MessageDto` expose `Attachment`
  (`MessageAttachmentDto` : Id, FileName, ContentType, SizeBytes, DurationSec).
- API : `POST /api/conversations/{id}/attachments` (multipart `file`, `isVoice`, `durationSec` ;
  retourne le `MessageDto` et **diffuse `ReceiveMessage`** au groupe via `IHubContext`) ;
  `GET /api/attachments/{id}` (`FileStreamResult`, `EnableRangeProcessing`, `Content-Disposition`
  `inline` pour image/audio sinon `attachment`, `Cache-Control: private, max-age=300`, ETag).
  Limites Kestrel/`FormOptions` relevees a la taille max d'upload + 1 Mo.
- Tests `AttachmentTests` (5) : edition/visibilite, image OK, vocal OK, exe refuse, type inconnu
  refuse, nom d'origine conserve / cle non devinable ; **4 unitaires + 39 integration = 43/43 verts**.

**Frontend** (Angular 21, standalone + signals)
- `AttachmentsService` : upload multipart avec **progression** (`HttpRequest` +
  `HttpEventType.UploadProgress`), recuperation des pieces jointes privees via **HttpClient (blob)**
  puis URL d'objet mise en cache (`getBlobUrl`), telechargement, `releaseAll` a la destruction.
- `Conversation`/`MessageDto` etendus (`attachment`) ; `display.ts` : `formatFileSize`,
  `formatDuration`.
- Fil de discussion : bouton **Fichier** (input file cache) et bouton **Vocal**
  (`MediaRecorder`, chronometre, limite `MAX_VOICE_SECONDS`, envoi/annulation) ; barre de progression
  d'upload ; rendu des bulles **image** (`<img>`), **vocal** (`<audio controls>` + duree) et
  **fichier** (carte + taille + telechargement ; telechargement force via blob).
- `thread.scss` : styles image/vocal/fichier + composer (boutons, recorder, progression) ; budget de
  style de composant releve (`angular.json` : avertissement 4->6 kB, erreur 8->10 kB).

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D28 | Detection du type **par magic bytes**, jamais par extension ni Content-Type client | Section 8 (analyse du type reel) ; empeche le contournement par renommage. |
| D29 | `StorageKey` = GUID aleatoire, nom d'origine stocke separement | Empeche la traversee de chemin et la devination d'URL. |
| D30 | Pieces jointes **privees** : recuperation via HttpClient (blob) + URL d'objet cote client | `<img src>`/`<audio src>` n'envoient pas le Bearer ; le token transite par l'intercepteur. |
| D31 | Abstraction `IFileStorage` avec implementations Local (dev) et S3 (via `Storage:Provider`) | Portabilite sans changer le code applicatif (section 2.1). |
| D32 | `Content-Disposition: inline` pour image/audio, `attachment` sinon | Affichage direct des medias, telechargement force des fichiers. |
| D33 | Limite de taille verifiee sur les octets reels + `MaxRequestBodySize` Kestrel | Coherence entre limite logique et limite de transport. |

### Problemes connus / non verifie

- [ ] **Enregistrement vocal** (`MediaRecorder`) et rendu image/audio non valides en navigateur ici
      (pas de navigateur automatise) : le **flux API** a ete valide de bout en bout (upload image +
      vocal, telechargement, refus exe/inconnu). A tester visuellement par l'utilisateur.
- [ ] `S3FileStorage` compile mais **non teste contre un vrai S3** (non configure en dev ; Local par
      defaut).
- [ ] Detection `text/plain` : pas de signature fiable, repose sur le Content-Type declare.
- [ ] Le frontend supprime les URL d'objet a la destruction du composant mais garde un cache par
      piece jointe tant que la page vit (pas d'eviction LRU).
- [ ] Google login toujours non teste de bout en bout (pas de Client ID).

### Resultats de verification (reels, executes le 2026-10-12)

Environnement : identique aux sprints precedents (SDK .NET 10.0.401, Node v20.20.2, PostgreSQL conteneur).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln -c Debug` | OK, **0 erreur / 0 avertissement** |
| Migration | `dotnet ef migrations add AddAttachments` + `database update` | OK (table `Attachments`), base de dev a jour |
| Tests backend | `dotnet test backend/TeamChat.sln --nologo` | OK, **43/43** (4 unitaires + 39 integration) |
| Build frontend | `npm run build` (dans `frontend/`) | OK, **0 erreur / 0 avertissement**, chunk `thread` genere |
| Upload image (runtime) | `POST /api/conversations/{id}/attachments` (png) | 200, `type=3`, piece jointe (fileName/size/contentType) |
| Upload vocal (runtime) | idem (`audio/webm`, `isVoice=true`, `durationSec=3`) | 200, `type=1`, `durationSec=3` |
| Telechargement (runtime) | `GET /api/attachments/{id}` | 200, `Content-Type=image/png`, `inline`, octets **identiques** a l'upload |
| Refus executable | upload `MZ` (`evil.exe`) | **400** « Type de fichier non autorise. » |
| Refus type inconnu | upload binaire sans signature | **400** |

---

## Sprint 5 : Appels audio, video et partage d'ecran

### Fait

**Backend**
- Domaine : `CallType` (Audio=0, Video=1), `CallStatus` (Ringing=0, Accepted=1, Rejected=2, Missed=3,
  Ended=4, Failed=5), entite `CallLog` (Id, `CallerId`, `CalleeId`, `Type`, `Status`, `StartedAt`,
  `AnsweredAt?`, `EndedAt?`). Migration `AddCallLog` (`20261012183052`) - table `CallLogs`, FK
  `AspNetUsers` en `Restrict` (pas de suppression en cascade des journaux d'appel).
- `CallService` : `StartCallAsync` (interdit a soi-meme ; destinataire existant et actif ; destinataire
  **en ligne** sinon `BadRequestException` « Le destinataire est hors ligne. » ; ni appelant ni
  destinataire deja en appel actif sinon `ConflictException`) ; `AcceptCallAsync` (callee,
  Ringing->Accepted) ; `RejectCallAsync` (callee, ->Rejected + `EndedAt`) ; `EndCallAsync`
  (participant ; Ringing->Missed, Accepted->Ended) ; expiration des sonneries (`ExpireStaleCallsAsync`,
  Ringing->Missed apres delai) ; `EndCallForDisconnectedUserAsync` (deconnexion) ;
  `AuthorizeRelayAsync` (seul caller/callee, appel Accepted, retourne le peer -> **un tiers ne peut pas
  relayer la signalisation**). `GetIceServersAsync` et `GetHistoryAsync` (limite 1-100, defaut 50).
- Messages **systeme** (`MessageType.System=4`) postes via `IConversationQueries.AddSystemMessageAsync`
  dans la conversation directe : « {Appel audio|video} {manque|refuse|termine (duree X)} »,
  `SenderId` = `CallerId`, diffuses au groupe conversation (`ReceiveMessage`).
- `IChatClient` : `IncomingCall(CallSessionDto)`, `CallAccepted(CallSessionDto)`,
  `CallEnded(callId, status)`, `ReceiveOffer`, `ReceiveAnswer`, `ReceiveIceCandidate` (pas de
  `CallRejected` separe : le refus passe par `CallEnded(Rejected)`).
- `ChatHub` : `CallUser` (retourne `CallSessionDto`), `AcceptCall`, `RejectCall`, `EndCall`,
  `SendOffer`, `SendAnswer`, `SendIceCandidate` ; relais via `Clients.Group(ChatGroups.User(peerId))`.
  Helpers `CallAsync<T>` (`AppException` -> `HubException`, messages clairs cote client) et
  `NotifyCallEndedAsync` (2 groupes user + message systeme). `CallUser` ajoute aussi les connexions des
  deux participants au groupe conversation (conversation directe creee a la volee).
- `CallTimeoutService : BackgroundService` (Api) : `PeriodicTimer(SweepSeconds)`, expire les sonneries
  au-dela de `RingingTimeoutSeconds`, diffuse `CallEnded` + message systeme.
- `CallsController` : `GET /api/calls/ice-servers` (STUN public + TURN configurable) ;
  `GET /api/calls/history`.
- ICE : STUN public (`TurnOptions.StunUrls`, defaut `stun:stun.l.google.com:19302`) + TURN optionnel.
  Identifiants **TURN REST API** coturn (username = expiry Unix, credential =
  base64(HMAC-SHA1(secret, username))), generes par requete (`IIceServerProvider`/`TurnIceServerProvider`,
  `TimeProvider`). `TurnOptions.Secret` jamais committe (`Turn__Secret`).
- Options : `CallOptions` (SectionName `Calls`, `RingingTimeoutSeconds=30`, `SweepSeconds=5`),
  `TurnOptions` (SectionName `Turn`).
- `docker-compose.yml` : service `coturn` (profil `turn`, `--use-auth-secret`, ports 3478 + plage
  UDP 49160-49200) ; `.env.example` : `Turn__*`, `Calls__*`.
- Tests `CallTests` (5) : flux complet (offre/reponse/ICE/raccrochage) + journal + message systeme ;
  relais par un tiers **refuse** ; appel vers utilisateur **hors ligne** refuse ;
  `GET /api/calls/ice-servers` ; `GET /api/calls/history`. Helper `WaitUntilOnlineAsync` (poll
  `IPresenceTracker`) pour rendre le test d'historique deterministe.

**Frontend** (Angular 21, standalone + signals)
- `core/models/call.models.ts` : `CallType`, `CallStatus`, `CallSessionDto`, `IceServerDto`,
  `CallHistoryItemDto`, `IceCandidatePayload`, `CallEndedPayload`, `SdpPayload`.
- `ChatService` etendu : subjects `incomingCall$`, `callAccepted$`, `callEnded$`, `receiveOffer$`,
  `receiveAnswer$`, `receiveIceCandidate$` ; enregistrement des 6 handlers hub ; methodes `callUser`,
  `acceptCall`, `rejectCall`, `endCall`, `sendOffer`, `sendAnswer`, `sendIceCandidate` (helper
  `invoke<T>`).
- `core/calls/call.service.ts` : machine d'etat (`phase` idle/outgoing/incoming/active/ended),
  WebRTC (`RTCPeerConnection`, `getUserMedia`, `getDisplayMedia`), buffers ICE, renegociation,
  `replaceTrack` pour camera/partage d'ecran ; `hangup`, `rejectIncoming`, `acceptIncoming`,
  `startCall`, `toggleMute`, `toggleCamera`, `toggleScreenShare` ; ICE chargees via
  `GET /api/calls/ice-servers` (fallback STUN) ; nettoyage des medias + message d'erreur epure.
- Overlay d'appel global `layout/shell/call-overlay.{ts,html,scss}` (branche dans `shell` via
  `<app-call-overlay />`) : etat entrant (accepter/refuser), sortant/en communication
  (video distante + apercu local mute, micro, camera, partage d'ecran, raccrocher), etat termine.
- `thread.{ts,html,scss}` : boutons **Audio**/**Video** dans l'en-tete (conversation directe
  uniquement) ; rendu des messages **systeme** (`type === 4`) sur une ligne centree, sans bulle ni
  actions.

### En cours

- Aucun.

### Decisions

| # | Decision | Raison |
|---|---|---|
| D34 | Appels **1-a-1 uniquement** (audio/video), restriction aux conversations directes | Perimetre des sections 10-12 ; pas d'appel de groupe dans ce sprint. |
| D35 | Signalisation (offre/reponse/ICE) relayee **via le hub** `ChatGroups.User(peer)` ; le serveur ne voit pas les media | Un seul canal temps reel ; le serveur reste un simple relais de signalisation, sans SFU. |
| D36 | Un **tiers** ne peut pas relayer la signalisation (`AuthorizeRelayAsync` : caller/callee + `Accepted`) | Empeche l'injection d'offre/ICE dans un appel d'autrui. |
| D37 | Expiration des sonneries cote serveur (`CallTimeoutService`) apres `Calls:RingingTimeoutSeconds` (30 s) | L'appelant ne doit pas rester bloque si le client ne rappelle pas ; trace `Missed` fiable. |
| D38 | Messages systeme (`MessageType.System`) postes dans la conversation directe avec `SenderId = CallerId` | Trace persistante et visible dans l'historique (« appel manque/refuse/termine »). |
| D39 | Etat d'appel tient compte de la **presence** (`IPresenceTracker`) : appel refuse si le destinataire est hors ligne | Evite une sonnerie fantome vers un utilisateur deconnecte. |
| D40 | TURN via **TURN REST API** (identifiants temporaires HMAC), secret cote env (`Turn__Secret`) | Identifiants ephemères, aucun secret statique dans le bundle client. |
| D41 | `AppException` -> `HubException(message)` (`CallAsync<T>`) plutot que `EnableDetailedErrors` | Messages d'erreur clairs cote client sans activer le mode verbeux global. |

### Problemes connus / non verifie

- [ ] **Flux media WebRTC non verifiable en environnement automatise** : offre/reponse/ICE, bascule
      camera et **partage d'ecran** (avec renegociation) reposent sur un **test manuel navigateur
      (2 onglets)**, non realisable ici. Le resultat n'est **pas** garanti par les tests d'integration
      (qui ne couvrent que la signalisation et les regles metier serveur).
- [ ] La migration `AddCallLog` doit etre verifiee appliquee en dev (`database update`) puis
      confirmee par un smoke test runtime.
- [ ] TURN (`coturn`) ajoute au `docker-compose` avec profil `turn` **non demarre/teste** ici ; en
      l'absence de TURN, seuls les profils reseau permissifs passent (STUN seul).
- [ ] Le partage d'ecran sur mobile et le changement de source pendant un partage ne sont pas geres.
- [ ] Google login toujours non teste de bout en bout (pas de Client ID).
- [ ] `S3FileStorage` toujours non teste contre un vrai S3 (prexistant, hors perimetre Sprint 5).

### Resultats de verification (reels, executes le 2026-10-12)

Environnement : identique aux sprints precedents (SDK .NET 10.0.401, Node v20.20.2, PostgreSQL conteneur).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln` | OK, **0 erreur / 0 avertissement** |
| Tests backend | `dotnet test backend/TeamChat.sln` | OK, **48/48** (4 unitaires + 44 integration) |
| Build frontend | `npm run build` (dans `frontend/`) | OK, **0 erreur / 0 avertissement**, chunks `shell`/`thread` generes |
| Migration | `dotnet ef migrations add AddCallLog` | OK (table `CallLogs`) - `database update` **a confirmer en dev** |

---

## Sprint 6 : Dashboard admin, qualite, deploiement

### Fait

**Lot A - Tableau de bord admin (statistiques)**

- `Application/Admin/Dtos/AdminStatsDto.cs` : `AdminStatsDto` (utilisateurs actifs/total, messages,
  conversations, groupes, fichiers) + `DailyMessageCountDto` + `ActiveGroupDto`.
- `Application/Abstractions/IAdminStatsQueries.cs` + `Infrastructure/Persistence/AdminStatsQueries.cs`
  (lecture en base ; group-by par jour calcule cote client sur une fenetre bornee de 7 jours).
- `Application/Admin/AdminStatsService.cs` + `Abstractions/IAdminStatsService.cs` : mise en cache
  (`CacheKeys.AdminStats()`, TTL 5 min).
- `Api/Controllers/AdminStatsController.cs` : `GET /api/admin/stats` (`[Authorize(Roles=Roles.Admin)]`).

**Lot B - Durcissement de la desactivation**

- `CacheKeys.UserMembershipTag(userId)` + `IMembershipService.InvalidateUserAsync(userId)`.
- `MembershipService` : verification `IsActive` (un utilisateur desactive n'est plus membre) et
  invalidation du tag d'appartenance par utilisateur.
- `Application/Abstractions/IUserSessionTerminator.cs` + `Api/Infrastructure/SignalRUserSessionTerminator.cs` :
  envoi de `ForceLogout` au groupe de l'utilisateur, eviction de la presence.
- `Api/Hubs/IChatClient.cs` : ajout de `ForceLogout()`.
- `AdminUserService` : a la desactivation -> `RevokeAllForUserAsync` + `InvalidateUserAsync` +
  `IUserSessionTerminator.TerminateAsync`.

**Lot B bis - Backplane Redis (SignalR)**

- Package `Microsoft.AspNetCore.SignalR.StackExchangeRedis` (10.0.12).
- `Program.cs` : backplane Redis active seulement si `Redis:ConnectionString` est defini.

**Lot C - Frontend admin**

- Backend support : `IUserQueries.AdminSearchAsync` (inclut les comptes inactifs), `IAdminUserService.ListAsync`,
  `GET /api/admin/users` dans `AdminUsersController`.
- `core/models/admin.models.ts`, `core/api/admin-users.service.ts`, `core/api/admin-stats.service.ts`.
- `features/admin/admin-users.*` (liste, creation, role, activation/desactivation) et
  `features/admin/admin-home.ts` (metriques + graphique Chart.js + top groupes).
- Route `admin/users` (`app.routes.ts`), lien nav dans `layout/shell/shell.html`.
- `chat.service.ts` : `forceLogout$` (evenement `ForceLogout`) ; `layout/shell/shell.ts` : deconnexion
  forcee (arret du hub, purge session, redirection `/login`).
- Dependance ajoutee : `chart.js@4.5.1`.

**Lot D - Qualite**

- `Program.cs` : middleware `Cache-Control: no-store` sur les reponses JSON (via `Response.OnStarting`).
- OpenAPI mappe hors production uniquement (`if (!app.Environment.IsProduction())`).

**Lot D bis - Tests (Redis + cache)**

- Package `Testcontainers.Redis` (4.16.0) ; `TeamChatApiFactory` accepte une chaine Redis optionnelle ;
  overloads `ApiTestHelper` ; `Api/RedisBackplaneTests.cs` (Postgres + Redis via Testcontainers) ;
  `Api/MembershipCacheTests.cs` (invalidation du cache d'appartenance apres desactivation).

**Lot E - Deploiement (fichiers prepares, aucun deploiement reel)**

- `backend/Dockerfile` (multi-stage .NET 10 -> runtime, port 8080) + `backend/.dockerignore`.
- `docker-compose.yml` : service `api` (profil `app`) reliant PostgreSQL, avec variables d'environnement.
- `frontend/vercel.json` (build + reecriture SPA).
- `.github/workflows/backend-ci.yml` : job `docker` (build de l'image API).
- `README.md` : sections Administration et Deploiement.

### En cours

- Validation utilisateur du sprint (resume a presenter).

### Decisions

| # | Decision | Raison |
|---|---|---|
| D42 | Statistiques admin servies par requetes dediees + cache 5 min, sans invalidation active | Donnees de supervision, faible criticite : la fraicheur a 5 min suffit et evite des invalidations couteuses. |
| D43 | Group-by jour calcule cote client sur une fenetre bornee (7 j) | `DateTimeOffset.UtcDateTime.Date` n'est pas traduisible par EF/Npgsql (erreur de coercion `DateTimeOffset`/`DateTime`). |
| D44 | Redis optionnel : active a la fois le cache L2 et le backplane SignalR | Un seul parametre (`Redis:ConnectionString`) ; en l'absence de Redis, fonctionne mono-instance (presence en memoire). |
| D45 | Desactivation = revocation des tokens + invalidation des caches + `ForceLogout` SignalR | Ferme immediatement les sessions actives et evite de servir des donnees mises en cache. |
| D46 | Presence (SignalR) en **memoire par instance** | Limite connue pour le deploiement multi-instances ; le backplane Redis synchronise les messages, pas l'etat de presence applicatif. |
| D47 | Deploiement : fichiers de configuration seulement (pas de deploiement effectif) | Choix explicite de l'utilisateur ; les credentials des fournisseurs ne sont pas disponibles. |
| D48 | Graphiques via `chart.js` directement (pas de `ng2-charts`) | Evite les incompatibilites de version avec Angular 21. |

### Problemes connus / non verifie

- [ ] **Presence multi-instances** : le backplane Redis diffuse les messages entre instances, mais la
  presence reste en memoire par instance (D46) ; un deploiement horizontal necessiterait un store partage.
- [ ] **Deploiement reel non effectue** : les fichiers (Dockerfile, `vercel.json`, compose) sont prepares
  mais aucun deploiement Vercel/Render/Neon n'a ete execute (D47).
- [ ] Verifications navigateur du dashboard admin (graphique, gestion des utilisateurs) non executees ici.
- [ ] Google login toujours non teste de bout en bout (pas de Client ID).
- [ ] `S3FileStorage` toujours non teste contre un vrai S3 (prexistant).

### Resultats de verification (reels, executes le 2026-10-13)

Environnement : identique aux sprints precedents (SDK .NET 10, Node v20.20.2, PostgreSQL + Redis conteneurs).

| Verification | Commande | Resultat |
|---|---|---|
| Build backend | `dotnet build backend/TeamChat.sln` | OK, **0 erreur / 0 avertissement** |
| Tests backend | `dotnet test backend/TeamChat.sln` | OK, **53/53** (4 unitaires + 49 integration, Redis via Testcontainers inclus) |
| Build frontend (dev) | `npm run build` (dans `frontend/`) | OK |
| Build frontend (prod) | `npm run build -- --configuration production` | OK, sortie `dist/frontend/browser` |


---

## Refonte UI/UX (post-Sprint 7, interface chat/appels)

Base : etat Sprint 7 (`730ea6f`) restaure sur `main`. Objectif : rendre le chat, l'annuaire et
les appels plus professionnels et "smooth".

### Fait

- Composant d'icones SVG inline `app-icon` (`frontend/src/app/shared/icon/icon.ts`), style lignes,
  sans dependance externe.
- Jetons de design CSS (`:root` dans `styles.scss`) : palette, rayons, ombres, transitions
  (`cubic-bezier`), scrollbars fines, animations partagees (`tc-fade-up`, `tc-pop`, `tc-fade-in`).
- **Chat (thread)** : passage en pleine largeur (plus de conteneur centre a 720px), bulles avec
  gradient pour les messages envoyes, animations d'apparition, meta/actions au survol.
  Compositeur : icones trombone (fichier), micro (vocal), et bouton d'envoi en icone (avion en papier)
  a la place du bouton texte "Envoyer" ; enregistrement vocal avec boutons valider/annuler en icones.
- **Appels** : boutons de controle en icones rondes (micro, camera, partage d'ecran, agrandir,
  plein ecran, reduire, raccrocher), ecran d'appel entrant avec anneau anime, carte plus soignee.
  Le widget minimise et les modes agrandi/plein ecran existants sont conserves.
- **Conversations** : liste en cartes arrondies avec survol "lift", avatars gradients, badge non-lus,
  etat vide, modale de nouvelle conversation retravaillee.
- **Annuaire** : grille de cartes responsives, pastille de presence, badge actif/inactif,
  recherche arrondie, pagination en boutons icones.
- Coquille (shell) : sidebar avec icones de navigation, bloc compte en bas, topbar translucide.
- **Admin** : tableau de bord (metriques avec icones, courbe `chart.js` aux couleurs de la charte,
  groupes les plus actifs, raccourcis), Utilisateurs (recherche arrondie, bouton "Nouvel utilisateur"
  en icone, roles, badges actif/inactif, actions activer/desactiver en icones, modale), Groupes
  (actions Membres/Archiver en icones, modale avec case a cocher en icone). Nouvelles icones
  `user-check`, `user-x`, `archive`.

### Budget de style

- `anyComponentStyle` releve (warning 20 kB / error 26 kB) car `thread.scss` depasse l'ancien seuil.

### Theme sombre + disposition 3 colonnes (suite, meme session)

Direction design validee : theme **sombre par defaut**, accent bleu electrique `#3B82F6`, typo Inter,
et disposition type Outlook/Slack en 3 colonnes (navigation | liste | fil + panneau de details).
Regle : aucune couleur en dur dans les composants, tout passe par des jetons `--tc-*`.

- `styles.scss` reecrit : `color-scheme: dark`, jetons surfaces/texte/bordure/accent/etats/ombres/motion,
  et helpers (`--tc-accent-rgb`, `--tc-danger-rgb`, `--tc-success-rgb`, `--tc-scrim`, `--tc-glass*`,
  `--tc-on-accent`, `--tc-group-avatar*`) pour supprimer les couleurs en dur.
- **Layout conversations 3 colonnes** : nouveaux `ConversationsLayoutComponent` (nav + `router-outlet`),
  `ConversationsEmptyComponent` (etat vide central), `ConversationsUiService` (drawer mobile).
  Routes `conversations` restructurees en enfants `''` et `':id'`.
- **Liste de conversations** (`conversations-list`) : colonne de navigation, filtrage cote client,
  lien actif surligne, fermeture du drawer mobile au clic, modale de nouvelle conversation.
- **Fil** (`thread`) : en-tete avec action Infos, messages groupes (avatars, bulles), panneau de details
  a droite (profil, "A propos", "Fichiers partages" derives des pieces jointes) masquable, popover emoji,
  responsive (details/nav en tiroirs).
- **Coquille (shell)** : sidebar rail sombre (icones + libelles), topbar avec **recherche globale**
  connectee a l'annuaire (`/directory?q=`) et raccourci `Cmd/Ctrl+K`, bascule des libelles en mobile.
- **Annuaire** : lit le parametre `q` pour pre-remplir la recherche ; racine scrollable avec padding.
- **Login** : carte sombre, marque, bouton Google en theme `filled_black`, messages d'erreur tokenises.
- **Appels** : couleurs du `call-overlay` migrees vers les jetons.
- **Admin** : racines scrollables/paddees (le `.content` de la coquille ne pad plus les pages) ;
  couleurs de la courbe `chart.js` lues depuis les jetons CSS ; couleurs en dur remplacees.

---

## Refonte UX/UI majeure — Sprint 1 : theme clair + composants partages

Base : `730ea6f` (Sprint 7) + refonte sombre (annulee/remplacee par le theme clair demande).
Objectif global : interface claire et professionnelle type Outlook, messagerie et administration
separees, sans casser l'existant. Travail par sprints, valides un a un.

### Inspection prealable (faits verifies)

- Frontend : Angular 21 standalone + signals (zoneless), SCSS a jetons `--tc-*`, aucune lib UI.
- Backend : ASP.NET Core 10, EF/Postgres, Identity (roles en base), JWT + refresh cookie, SignalR.
- Reellement connecte : chat temps reel, presence, saisie, appels WebRTC (audio/video/ecran),
  fichiers (upload 25 Mo / vocal 10 Mo, detection magic-bytes), images, vocaux, emoji, edition/
  suppression, pagination, dashboard admin, gestion users/groupes.
- Non implemente (a ne pas presenter comme fonctionnel) : notifications (bouton cloche sans handler),
  repondre/citer, reactions, epinglage, transfert, accusés de lecture visibles (auto seulement),
  Google sign-in (inert), endpoints membres/fichiers partages, cote admin : pas de suppression
  d'utilisateur, pas de protection "dernier admin", pas de journal d'activite, pas de parametres.

### Fait (Sprint 1)

- `styles.scss` reecrit en **theme clair** : palette demandee (`#F6F8FB`, `#FFFFFF`, `#F1F5F9`,
  texte `#172033`/`#526176`, bordures `#E2E8F0`, bleu `#2563EB`/`#1D4ED8`, selection `#EFF6FF`,
  succes `#15803D`, alerte `#B45309`, erreur `#B91C1C`), base 14 px, rayons 6-8 px, ombres discretes,
  transitions 120-220 ms, `color-scheme: light`. Tous les jetons restent centralises.
- Gradients decoratifs neutralises au niveau des jetons (`--tc-gradient` = aplat bleu).
- **Coquille** : navigation claire 220 px, selection fond bleu tres clair + texte/icone bleus,
  suppression du glow et des barres d'accent decoratives ; avatars/badge via composants partages ;
  infobulles sur les elements sans libelle.
- **Login** : theme clair, marque, bouton via composant partage.
- **Appels** : `call-overlay` adapte au theme clair (texte lisible, plus de gradient decoratif).
- **Composants partages** crees (`frontend/src/app/shared/ui/`) : `tc-avatar`, `tc-button`,
  `tc-badge`, `tc-modal` (focus trap + Echap), `tc-empty-state`, `tc-spinner`, directive `tcTooltip`
  (nom accessible + infobulle).
- Integration validee de ces composants : coquille (avatar/badge/tooltip), login (bouton),
  liste de conversations (avatars, infobulle, modale nouvelle conversation via `tc-modal`),
  etat vide (`tc-empty-state`).
- Nettoyage : code mort (`initials` dupliques, CSS `.avatar`/`.presence-dot`/modale locale).

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK, composant mis a jour |
| Backend (deja en cours) | `dotnet run --project backend/src/TeamChat.Api --launch-profile http` | OK, port 5285 |

---

## Refonte UX/UI majeure — Sprint 2 : repartition de l'espace et panneaux

### Pre-requis verifies

- Structure 3 colonnes deja en place (Sprint precedent) : navigation 220 px, liste de conversations
  320 px, conversation flexible, panneau de details 300 px.
- En-tete de conversation et zone de saisie deja hors de la zone de defilement (donc persistants).
- Chaque zone possede son propre defilement (`.conv-scroll`, `.scroll-area`, `.details-body`).

### Fait

- **Rail de navigation repliable et persistant** : bouton dans la barre de titre de la sidebar,
  etat memorise dans `localStorage` (`tc.sidebar-collapsed`), repli en rail d'icones de 72 px,
  infobulles sur les liens. La reouverture est toujours possible (bouton present en rail).
- **Panneau de details en surcouche des qu'il n'y a pas assez de place** : bascule en panneau
  fixe (avec voile cliquable) sous 1024 px au lieu de comprimer la conversation ; largeur inline
  264 px sous 1180 px ; restauration de l'espace a la fermeture (panneau masque si ferme).
- **Barre de saisie / en-tete persistants** et defilement independant confirmes.
- **Couleurs** : suppression des halos bleus decoratifs residuels (bouton nouvelle conversation,
  bouton envoyer, boutons primaires admin) remplaces par des ombres de jeton (`--tc-shadow-sm`/
  `--tc-shadow`) et des transitions de couleur courtes, conformement aux regles de design.
- Mise en page responsive ajustee (padding messages sous 640 px).

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Rendu navigateur non verifie automatise (pas de test e2e) : validation visuelle a faire cote utilisateur.
- [ ] Regressions residuelles possibles liees au passage clair ; serons traitees sprint par sprint.

---

## Refonte UX/UI majeure — Sprint 3 : centre de conversation (messagerie)

### Fait

- **Separateurs de jour** dans le fil : « Aujourd'hui », « Hier » ou date longue francaise
  (annee affichee seulement si differente), places au changement de jour et a chaque systeme.
- **Bouton « Revenir en bas » / nouveaux messages** : apparait des que l'on remonte le fil,
  affiche le nombre de nouveaux messages recus hors du bas, redefile et remet le compteur a zero.
- **Action « Copier le texte »** sur les messages texte, avec retour visuel (icone coche ~1,4 s).
  Action locale (presse-papiers), ne necessite aucune API.
- **Etat vide de conversation** unifie via le composant partage `tc-empty-state`.
- **Accessibilite** : fil en `role="log"` avec libelle, actions de message visibles au survol,
  **au focus clavier** et **en permanence sur ecrans tactiles** (`@media (hover: none)`),
  libelle sur le champ de saisie.
- Ajout des icones `copy` et `arrow-down` au jeu d'icones partage.

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Non implemente (necessite backend, non simule) : repondre/citer, reactions, epinglage,
  transfert. Non presentes comme fonctionnels.
- [ ] Validation visuelle manuelle a faire cote utilisateur (fil, separateurs, bouton bas).

---

## Refonte UX/UI majeure — Sprint 4 : liste de conversations (colonne gauche)

### Fait

- **Filtres segmentes** : Tout / Non lus / Groupes, avec compteur sur « Non lus ». Filtrage local
  (les donnees viennent du DTO), cumulable avec la recherche texte.
- **Horodatage intelligent** de la liste : heure (aujourd'hui), « Hier », jour abrege (cette semaine),
  puis date jj/mm/aa — au lieu de toujours afficher l'heure.
- **Emphase des non lus** : nom en gras, apercu assombri, heure en bleu ; badges deja existants.
- **Compteur global de non lus** dans l'en-tete de la liste.
- **Squelette de chargement** (6 lignes animees) a la place du simple texte « Chargement… ».
- **Etats vides** unifies via le composant partage `tc-empty-state`.
- **Accessibilite** : `aria-current="page"` sur la conversation active, libelle ARIA des badges
  de non lus, filtres en `role="tablist"`/`role="tab"` avec `aria-selected`.

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Tri explicite (par nom / activite) non ajoute : l'ordre reste celui du serveur (activite).
- [ ] Archiver une conversation non disponible : le DTO de liste n'expose pas d'etat d'archivage
  (necessite un champ backend).
- [ ] Validation visuelle manuelle a faire cote utilisateur (filtres, horodatage, squelette).

---

## Refonte UX/UI majeure — Sprint 5 : espace Administration + coherence

### Fait

- **Espace d'administration distinct** : nouveau composant de mise en page `admin-layout`
  (barre « Administration » + onglets Tableau de bord / Utilisateurs / Groupes + `router-outlet`),
  avec son propre en-tete et une navigation interne. Les routes `admin`, `admin/users`,
  `admin/groups` deviennent des enfants d'une route parent protegee (`adminGuard` conserve).
- **Coherence des pages admin** avec la bibliotheque de composants partagee :
  - avatars via `tc-avatar` (fini les `.avatar` en dur + initiales calculees) ;
  - statuts et tags via `tc-badge` (Actif/Desactive, Archivé, nombre de membres) ;
  - modales via `tc-modal` (piege de focus, Echap, clic dehors, restauration du focus) ;
  - etats vides via `tc-empty-state`.
- Suppression du code et du CSS morts (`.avatar`, `.status`, `.tag`, `.modal-backdrop`,
  `.modal`, `.modal-header`, methodes `initials`/`initialsOfUser`).
- Ameliorations d'accessibilite : libelles ARIA sur la recherche et les selects de role,
  `aria-pressed` sur la selection de membres, `data-autofocus` dans les modales.

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Les fonctions d'administration absentes du backend restent absentes de l'UI (pas de
  suppression d'utilisateur, pas de protection « dernier admin », pas de journal d'activite,
  pas de parametres) : a faire cote backend, non simule ici.
- [ ] Validation visuelle manuelle a faire cote utilisateur (barre et onglets admin, modales).

### Suivi des manques backend

Toutes les logiques manquantes cote backend (a implementer plus tard) sont recensees dans
`BACKEND_TODO.md` a la racine du depot : utilisateurs (suppression, dernier admin), profil
courant, conversations/messages (reponses, reactions, epingles, fichiers partages, archivage),
notifications, groupes (desarchivage, renommage), audit, parametres, authentification
(mot de passe oublie, Google), appels de groupe, quotas de pieces jointes.
- [ ] Fonctions a connecter (necessitent backend) : repondre/citer, reactions, notifications persistees,
  protection "dernier admin", journal d'activite, parametres, endpoints membres/fichiers partages.
- [ ] Comptes de test : `admin@teamchat.local` / `Admin123!`,
  `demo.alpha@teamchat.local` et `demo.beta@teamchat.local` / `Demo123!`.

---

## Refonte UX/UI majeure — Sprint 6 : Annuaire & recherche

### Fait

- **Annuaire redessine** avec la bibliotheque de composants partagee :
  - avatars via `tc-avatar` (fini les `.avatar` + initiales calculees en dur) ;
  - statut de compte via `tc-badge` (Actif/Inactif) ;
  - etats vides via `tc-empty-state`, **distingues** selon le contexte (aucun resultat pour une
    recherche vs annuaire vide) ;
  - boutons de pagination via `tc-button`.
- **Presence reelle** : la pastille en ligne utilise desormais la presence SignalR
  (`ChatService.presence`) au lieu de confondre activite du compte et presence (bug corrige).
- **Action « Message »** sur chaque utilisateur : cree la conversation directe
  (`createDirect`, idempotent) puis navigue vers `/conversations/{id}`, avec etat de chargement
  par ligne et desactivation des comptes inactifs.
- **Chargement** : squelette anime (au lieu de « Chargement… ») coherent avec la liste des
  conversations ; respect de `prefers-reduced-motion`.
- **Animation decorative supprimee** (plus de `translateY` au survol) conformement aux regles UX.
- **Accessibilite** : compteurs, `aria-label` sur la recherche et les actions, `aria-hidden`
  sur le squelette.
- **Responsive** : en-tete empile et grille mono-colonne sous 640px ; CSS mort supprime
  (`.avatar`, `.presence-dot`, `.badge`, `.card`, `.state`).

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Validation visuelle manuelle a faire cote utilisateur (grille, presence, action Message,
  squelette, pagination, responsive).
- [ ] Le regroupement en direct d'une conversation creee depuis l'annuaire depend du backend
  (`createDirect`) : fonctionnel via la creation idempotente existante, non reteste en e2e ici.

---

## Refonte UX/UI majeure — Sprint 7 : Fichiers, vocal & medias

### Fait

- **Apercu plein ecran des images** : nouveau composant partage `tc-lightbox` (fond assombri,
  clic hors image / bouton / Echap pour fermer, focus visible). Les images du fil sont cliquables
  avec un badge de zoom au survol.
- **Lecteur de message vocal sur mesure** : nouveau composant `app-voice-player`
  (bouton lecture/pause, barre de progression deformable **au clavier**, minuteur temps courant/
  duree). Remplace le lecteur `<audio>` natif pour un rendu homogene au theme.
- **Cartes de fichier ameliorees** : affichage de l'extension (PDF, DOCX, ZIP…) dans la vignette.
- **Icones ajoutees** : `play`, `pause` (jeu d'icones partage).
- **Tokens** : ajout de `--tc-scrim-strong` (fond de visionneuse) ; aucun couleur codee en dur
  dans les composants (lightbox, zoom, lecteur).
- CSS mort supprime (`.voice`, `.voice-icon`, `.voice-duration`, ancien lecteur natif).

### Resultats de verification (reels, executes le 2026-10-14)

| Verification | Commande | Resultat |
|---|---|---|
| Build frontend (prod) | `npm run build -- --configuration production` | OK, 0 erreur / 0 warning |
| Dev server (HMR) | recompilation auto | OK |

### Problemes connus / non verifie

- [ ] Validation visuelle manuelle a faire cote utilisateur (apercu image, lecteur vocal, cartes fichier).
- [ ] L'onglet « Fichiers partages » du panneau Détails est **derive des messages charges**
  (cote client) faute d'endpoint backend ; la liste complete necessite
  `GET /api/conversations/{id}/attachments` (voir `BACKEND_TODO.md` §3).
- [ ] La duree reelle du vocal depend des metadonnees du blob (fallback sur `durationSec`).

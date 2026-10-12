# PROGRESS - TeamChat

> Journal d'avancement. Tenu a jour a la fin de chaque sprint.
> `CLAUDE.md` est la source de verite ; ce fichier ne coche que ce qui est reellement termine.

## Etat des sprints

- [x] **Sprint 0 : Initialisation** - termine
- [x] **Sprint 1 : Authentification, roles, annuaire** - termine et pousse sur `sprint-1-auth` (PR manuelle a creer)
- [x] **Sprint 2 : Conversations privees et temps reel** - termine et valide
- [x] **Sprint 3 : Groupes, modification, suppression, non lus** - termine, valide et pousse sur `sprint-3-groups` (PR manuelle a creer)
- [~] **Sprint 4 : Fichiers et messages vocaux** - en cours (backend + frontend realises, en attente de validation)
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

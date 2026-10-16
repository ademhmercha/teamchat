# TeamChat : guide de construction pour Claude Code

> Ce fichier est la **source de vérité** du projet. Lis-le en entier avant d'écrire du code.
> Travaille **un sprint à la fois**, coche les cases `[x]` au fur et à mesure, et **arrête-toi à la fin de chaque sprint** pour que je valide.

---

## 1. Le projet

**TeamChat** est une messagerie interne **multi-sociétés** : chaque société dispose de son propre espace totalement isolé (utilisateurs, conversations, groupes, fichiers, appels).

### Rôles

| Rôle | Pouvoirs |
|---|---|
| **SuperAdmin** | Rôle plateforme, hors société : administration globale des sociétés (`/api/platform/tenants` : liste, détail, renommage, activation/désactivation) |
| **Owner** | Créateur de la société. Gère les admins **et** les membres, les groupes, voit les statistiques |
| **Admin** | Gère les **membres** uniquement (ne peut pas créer/modifier/supprimer un admin ou l'Owner), les groupes, voit les statistiques |
| **User** | Voit l'annuaire, recherche, écrit, envoie fichiers et vocaux, appelle |

### Multi-société (isolation)

- Isolation en **base unique** : `TenantId` sur les entités + **filtres globaux EF Core** (option A).
- Email **unique global**, sans restriction de domaine : un compte appartient à une seule société.
- À l'inscription publique, un visiteur **crée sa société** (il devient Owner, actif immédiatement) ou **rejoint** la société dont il est membre via `/login`.
- Les données existantes sont rattachées à une **société par défaut** par la migration.
- Définition des rôles de gestion par société : Owner (admins + membres) > Admin (membres uniquement) ; garde-fou « dernier gestionnaire » par société.
- **Points de sortie limités** : toute requête écrite/lecture passe par le filtre global ; les seuls endroits autorisés à le contourner sont le seeding et `PlatformQueries` (`IgnoreQueryFilters`), appelé uniquement par `PlatformController` protégé par `[Authorize(Roles = Roles.SuperAdmin)]`.
- **Endpoints plateforme (SuperAdmin)** : `GET /api/platform/tenants`, `GET /api/platform/tenants/{id}`, `PATCH /api/platform/tenants/{id}` avec `{ name?, isActive? }` (un champ au moins, nom 1..100 caractères).
- **Société désactivée** : `AuthService.EnsureTenantActiveAsync` refuse connexion, renouvellement et connexion Google (401 « Societe desactivee. »).
- **Avatar réservé à la société** : `GET /api/users/{id}/avatar` est authentifié (le filtre global = même société). Le frontend doit donc l'appeler avec le token (réglage Sprint 3).
- **Traitements d'arrière-plan** : `CallTimeoutService` boucle sur les sociétés actives (`ITenantService.GetActiveIdsAsync`) et résout la société dans un scope dédié (`ICurrentTenantSetter.SetTenant`) pour que `StampTenant` attribue le bon `TenantId`.
- **Clés de cache** : `tc:v1:t:{tenant}:user:{userId}` (profil utilisateur), `tc:v1:t:{tenant}:directory:...`, `tc:v1:t:{tenant}:admin:stats`.

### Fonctionnalités (périmètre complet)

1. Connexion email + mot de passe **et** connexion Google
2. Annuaire des utilisateurs : liste paginée, recherche (nom, email), pastille en ligne / hors ligne
3. Conversation privée avec n'importe quel utilisateur actif
4. **Groupes créés uniquement par l'admin** (les membres ne créent ni ne quittent un groupe)
5. Messages texte en temps réel, **modifier** et **supprimer** ses propres messages
6. « En train d'écrire… » (éphémère, jamais stocké)
7. Messages **vocaux** (enregistrement navigateur)
8. Envoi de **fichiers** et d'images
9. Messages non lus
10. **Appel audio 1-à-1** (WebRTC), sonnerie, accepter / refuser
11. **Appel vidéo** : la caméra est **optionnelle** (on peut l'activer / couper pendant l'appel)
12. **Partage d'écran** pendant un appel
13. Dashboard admin : statistiques, utilisateurs, groupes
14. **Multi-société** : création d'une société (Owner) depuis une page publique, isolation totale des données par société

### Hors périmètre (ne pas faire)

- Appels de groupe (nécessiterait un serveur SFU)
- Intelligence artificielle
- Application mobile native

---

## 2. Stack technique

| Couche | Choix |
|---|---|
| Backend | **ASP.NET Core Web API** (dernière version LTS installée, .NET 10 ou 8) + **SignalR** |
| Architecture | Clean Architecture (Domain / Application / Infrastructure / Api) |
| Base de données | **PostgreSQL** + **EF Core** (provider Npgsql), migrations |
| Auth | ASP.NET Core Identity + **JWT** + refresh token + **Google** (ID token) |
| Frontend | **Angular** (dernière version stable, composants standalone, signals) |
| Appels | **WebRTC** (signalisation via SignalR), STUN + TURN |
| Cache | **HybridCache** (.NET 9+, sinon `IMemoryCache` + `IDistributedCache`) : mémoire locale par défaut, **Redis** en option (obligatoire dès qu'il y a plusieurs instances) |
| Fichiers | Abstraction `IFileStorage` : disque local en dev, stockage objet S3-compatible en prod |
| Tests | xUnit, Moq, FluentAssertions, Testcontainers (PostgreSQL) ; Jasmine/Jest côté Angular |
| DevOps | Docker, docker-compose, GitHub Actions |
| Déploiement | Frontend sur **Vercel** ; API + SignalR en Docker (Azure App Service ou Render) ; PostgreSQL sur Neon |

---

## 3. Structure du dépôt

```
teamchat/
├── CLAUDE.md                      ← ce fichier
├── PROGRESS.md                    ← journal d'avancement (tu le tiens à jour)
├── docker-compose.yml             (api, postgres, redis, coturn)
├── .github/workflows/
│   ├── backend-ci.yml
│   └── frontend-ci.yml
├── backend/
│   ├── TeamChat.sln
│   ├── src/
│   │   ├── TeamChat.Domain/           (entités, enums, règles métier pures)
│   │   ├── TeamChat.Application/      (cas d'usage, DTOs, interfaces, validators)
│   │   ├── TeamChat.Infrastructure/   (EF Core, Identity, stockage, Google, email)
│   │   └── TeamChat.Api/              (controllers, hubs, middleware, Program.cs)
│   └── tests/
│       ├── TeamChat.UnitTests/
│       └── TeamChat.IntegrationTests/
└── frontend/                          (application Angular)
```

---

## 4. Règles de travail (à respecter en permanence)

1. **Un sprint à la fois.** À la fin : tests verts, `PROGRESS.md` mis à jour, résumé court, puis **attends ma validation**.
2. **Petits commits**, format Conventional Commits : `feat:`, `fix:`, `test:`, `chore:`, `docs:`, `refactor:`. Une branche par sprint (`sprint-1-auth`), pull request vers `main`.
3. **Aucun secret dans le dépôt.** Clés, chaînes de connexion et secrets JWT passent par les variables d'environnement ou `dotnet user-secrets`. Fournis un `.env.example`.
4. **Toute règle de sécurité se vérifie côté serveur.** Ne fais jamais confiance au client (rôle, auteur d'un message, appartenance à une conversation).
5. **Pas de logique métier dans les controllers ni dans le hub** : ils appellent des services de la couche Application.
6. **Teste ce qui compte** : règles d'autorisation, modification/suppression de message, non lus, accès aux conversations. Pas besoin de tester le code trivial.
7. Les messages d'erreur de l'API suivent le format **ProblemDetails** (RFC 7807).
8. Demande-moi si une décision change le périmètre. Sinon, choisis la solution la plus simple et note-la dans `PROGRESS.md`.
9. **Cache** : toute donnée mise en cache a un TTL, une clé incluant l'identifiant concerné, et une invalidation testée (section 8 bis). Jamais de cache sur une décision d'autorisation sans invalidation.
10. Avant de déclarer un sprint terminé, **fais tourner** build, tests et l'application, ne te contente pas de relire le code.

---

## 5. Modèle de données

```
AppUser (hérite de IdentityUser)
  Id, DisplayName, Email, AvatarUrl, IsActive, CreatedAt, LastSeenAt

Conversation
  Id, Type (Direct | Group), Name (null si Direct), CreatedByUserId, CreatedAt
  DirectKey (string unique, "idPetit_idGrand", évite les doublons de conversations privées)

ConversationParticipant
  ConversationId, UserId, JoinedAt, LastReadMessageId (ou LastReadAt)

Message
  Id, ConversationId, SenderId,
  Type (Text | Voice | File | Image | System),
  Content (texte, null pour vocal/fichier),
  AttachmentId (nullable),
  CreatedAt, EditedAt (nullable), DeletedAt (nullable)

Attachment
  Id, StorageKey, OriginalFileName, ContentType, SizeBytes,
  DurationSec (vocaux), UploadedByUserId, CreatedAt

CallLog
  Id, CallerId, CalleeId, Type (Audio | Video),
  Status (Ringing | Accepted | Rejected | Missed | Ended | Failed),
  StartedAt, AnsweredAt, EndedAt

RefreshToken
  Id, UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedAt
```

Règles :
- **Suppression douce** : `DeletedAt` renseigné, le contenu n'est plus renvoyé aux clients (afficher « message supprimé »).
- Seul l'**auteur** peut modifier ou supprimer son message ; la modification d'un message supprimé est interdite.
- Index : `Message(ConversationId, CreatedAt DESC)`, `Conversation(DirectKey)` unique, `AppUser(Email)` unique, recherche sur `DisplayName`.

---

## 6. Contrat API REST

Toutes les routes sont préfixées `/api` et exigent un JWT, sauf `auth/*`.

### Auth
| Méthode | Route | Description |
|---|---|---|
| POST | `/auth/login` | Email + mot de passe → access token + refresh cookie |
| POST | `/auth/google` | Reçoit un ID token Google, le valide, retourne nos tokens |
| POST | `/auth/refresh` | Rotation du refresh token |
| POST | `/auth/logout` | Révoque le refresh token |
| POST | `/auth/accept-invite` | Définit le mot de passe via le lien d'invitation |
| GET | `/auth/me` | Profil de l'utilisateur courant |

### Utilisateurs
| Méthode | Route | Description |
|---|---|---|
| GET | `/users?search=&page=&pageSize=` | Annuaire (utilisateurs actifs, avec statut de présence) |
| GET | `/users/{id}` | Profil |
| POST | `/admin/users` | **Admin** : crée un utilisateur / envoie une invitation |
| PATCH | `/admin/users/{id}` | **Admin** : rôle, activation, nom |

### Conversations et messages
| Méthode | Route | Description |
|---|---|---|
| GET | `/conversations` | Mes conversations (dernier message, nombre de non lus) |
| POST | `/conversations/direct` | Corps `{ userId }` : crée ou retrouve la conversation privée |
| GET | `/conversations/{id}/messages?before=&limit=` | Historique paginé (curseur) |
| POST | `/conversations/{id}/read` | Marque comme lu |
| POST | `/admin/groups` | **Admin** : crée un groupe `{ name, memberIds }` |
| PUT | `/admin/groups/{id}/members` | **Admin** : modifie les membres |
| DELETE | `/admin/groups/{id}` | **Admin** : archive un groupe |

### Fichiers
| Méthode | Route | Description |
|---|---|---|
| POST | `/conversations/{id}/attachments` | Upload multipart (fichier ou vocal), crée le message |
| GET | `/attachments/{id}` | Téléchargement, uniquement pour les participants de la conversation |

### Appels et admin
| Méthode | Route | Description |
|---|---|---|
| GET | `/calls/ice-servers` | Liste STUN/TURN (identifiants TURN temporaires) |
| GET | `/calls/history` | Historique d'appels de l'utilisateur |
| GET | `/admin/stats` | Utilisateurs actifs, messages par jour, groupes les plus actifs |

---

## 7. Contrat SignalR : `ChatHub` (`/hubs/chat`)

Authentification par JWT : le navigateur ne peut pas envoyer d'en-tête sur WebSocket, donc **lire le token dans `?access_token=`** (événement `OnMessageReceived` de `JwtBearerEvents`) pour les routes `/hubs`.

À la connexion : ajouter la connexion à un groupe SignalR par conversation dont l'utilisateur est participant, et à un groupe personnel `user:{id}`.

### Client → Serveur
| Méthode | Rôle |
|---|---|
| `SendMessage(conversationId, content)` | Texte |
| `EditMessage(messageId, newContent)` | Auteur uniquement |
| `DeleteMessage(messageId)` | Auteur uniquement |
| `StartTyping(conversationId)` / `StopTyping(conversationId)` | Saisie en cours |
| `MarkAsRead(conversationId, messageId)` | Non lus |
| `CallUser(calleeId, type)` | Lance un appel |
| `AcceptCall(callId)` / `RejectCall(callId)` / `EndCall(callId)` | Cycle de l'appel |
| `SendOffer(callId, sdp)` / `SendAnswer(callId, sdp)` | Signalisation WebRTC |
| `SendIceCandidate(callId, candidate)` | Signalisation WebRTC |

### Serveur → Client
`ReceiveMessage`, `MessageEdited`, `MessageDeleted`, `UserTyping`, `UserStoppedTyping`,
`PresenceChanged`, `ConversationUpdated`, `AddedToGroup`,
`IncomingCall`, `CallAccepted`, `CallRejected`, `CallEnded`, `ReceiveOffer`, `ReceiveAnswer`, `ReceiveIceCandidate`.

Règles côté hub :
- Vérifier **à chaque appel** que l'utilisateur est participant de la conversation.
- Présence : compter les connexions par utilisateur (plusieurs onglets). « En ligne » tant que ≥ 1 connexion ; « hors ligne » quand la dernière se ferme (avec petit délai de grâce pour les rechargements de page). Un service singleton `IPresenceTracker` suffit (note dans `PROGRESS.md` qu'il faudrait Redis pour plusieurs instances).
- Un appel n'est relayé qu'entre les deux participants de cet appel. Refuser un appel vers un utilisateur inactif, hors ligne ou déjà en appel.
- Appel sans réponse après ~30 s : statut `Missed`.

---

## 8. Sécurité (checklist transversale)

- [ ] Mots de passe hachés (Identity), verrouillage après plusieurs échecs, politique de mot de passe
- [ ] Access token JWT court (≈ 15 min) ; refresh token avec **rotation**, stocké haché en base, envoyé en cookie `HttpOnly` + `Secure` + `SameSite` adapté
- [ ] Google : valider l'ID token côté serveur (`Google.Apis.Auth`, audience = notre Client ID), refuser les comptes absents de la base ou inactifs (pas d'auto-inscription), option de restriction au domaine de la société
- [ ] Autorisations par rôle (`[Authorize(Roles = "Admin")]`) sur toutes les routes `/admin`
- [ ] Contrôle d'appartenance à la conversation sur **chaque** lecture/écriture (REST et hub)
- [ ] Modification/suppression : seul l'auteur
- [ ] Fichiers : liste blanche de types, taille maximale, nom de stockage aléatoire (jamais le nom d'origine), `Content-Disposition` correct, analyse du type réel (pas seulement l'extension)
- [ ] Contenu des messages affiché en texte échappé côté Angular (jamais `innerHTML` non assaini)
- [ ] CORS limité au domaine du frontend (credentials autorisés), HTTPS partout, en-têtes de sécurité
- [ ] Rate limiting sur `/auth/login` et sur l'envoi de messages (middleware `RateLimiter` intégré)
- [ ] Validation de toutes les entrées (FluentValidation), taille maximale d'un message
- [ ] Logs structurés (Serilog) sans données sensibles
- [ ] **Piège Vercel** : si le front (`*.vercel.app`) et l'API sont sur des domaines différents, le cookie du refresh token est « cross-site » (`SameSite=None; Secure` obligatoire, et certains navigateurs le bloquent). Solution recommandée : domaine personnalisé avec deux sous-domaines (`app.exemple.com`, `api.exemple.com`), ou réécriture `/api` via `vercel.json`.

---

## 8 bis. Stratégie de cache

**Principe : mettre en cache ce qui est lu souvent et change rarement. Ne jamais mettre en cache une décision de sécurité sans l'invalider quand les données changent.**

### Mise en place
- Interface `ICacheService` (couche Application) avec `GetOrCreateAsync(key, factory, ttl)`, `RemoveAsync(key)` et `RemoveByTagAsync(tag)`.
- Implémentation Infrastructure basée sur **HybridCache** (ou `IMemoryCache` + `IDistributedCache` si .NET 8) : cache local (L1) + Redis (L2) si `Redis__ConnectionString` est renseignée, sinon mémoire seule. Le code métier ne sait pas lequel est utilisé.
- Clés **préfixées et versionnées** : `tc:v1:user:{id}`, `tc:v1:membership:{conversationId}:{userId}`. Toujours un **TTL**.
- Protection contre le « cache stampede » : une seule requête recalcule une valeur expirée (comportement natif de HybridCache).

### Ce qu'on met en cache

| Donnée | TTL | Invalidation |
|---|---|---|
| **Appartenance à une conversation** (userId, conversationId) : lue à chaque appel du hub et de l'API | 5 min | Quand un membre est ajouté/retiré, qu'un groupe est archivé ou qu'un utilisateur est désactivé |
| **Profil minimal d'un utilisateur** (rôle, `IsActive`, nom, avatar) | 1 min | À toute modification par l'admin ; **désactivation = invalidation immédiate** |
| **Annuaire** (liste des utilisateurs actifs, sans la présence) | 60 s | Création, modification ou désactivation d'un utilisateur |
| **Présence** : nombre de connexions par utilisateur | Heartbeat + expiration | Connexion/déconnexion SignalR (Redis si plusieurs instances, mémoire sinon) |
| **Statistiques du dashboard admin** | 5 min | Aucune (données approximatives acceptées) |
| **Configuration STUN/TURN** statique | 1 h | Changement de configuration. Les identifiants TURN temporaires sont générés à la demande, **jamais partagés entre utilisateurs** |

### Ce qu'on ne met PAS en cache
- Le contenu des messages et l'historique : la base est la source de vérité, et la pagination par curseur est déjà rapide.
- Les compteurs de non lus : ils doivent être exacts (calcul en base avec index).
- Les mots de passe, tokens et refresh tokens.
- Les statuts d'appel (état éphémère géré par le hub et `CallLog`).

### Cache HTTP et frontend
- Pièces jointes : `Cache-Control: private, max-age=...` + `ETag`, jamais `public` (les fichiers sont privés).
- Réponses JSON de l'API : `Cache-Control: no-store` par défaut ; exceptions explicites seulement.
- Angular : un **store en signals** garde l'annuaire et les conversations en mémoire, évite de recharger à chaque navigation, et est mis à jour par les événements SignalR (pas de polling).

### SignalR et plusieurs instances
- Si le backend tourne sur plus d'une instance : activer le **backplane Redis** (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) et basculer `IPresenceTracker` sur Redis.
- Sur une seule instance (démo gratuite), la mémoire suffit : le noter dans `PROGRESS.md`.

### Tests obligatoires sur le cache
- Après le retrait d'un membre d'un groupe, il **ne peut plus** lire ni envoyer (le cache a bien été invalidé).
- Après désactivation d'un utilisateur, ses requêtes suivantes sont refusées sans attendre l'expiration du TTL.
- Un utilisateur ne peut jamais recevoir une valeur mise en cache pour un autre (clés incluant l'identifiant).
- Le comportement est identique avec et sans Redis.

---

## 9. Plan par sprints

Chaque sprint a une **définition de terminé**. Ne passe au suivant qu'après ma validation.

### Sprint 0 : Initialisation

- [ ] Créer le dépôt, `.gitignore` (.NET + Node), `README.md` minimal, branche `main` protégée, convention de branches
- [ ] `PROGRESS.md` créé (sections : fait, en cours, décisions, problèmes connus)
- [ ] Solution .NET avec les 4 projets + 2 projets de tests, références entre projets respectant la Clean Architecture
- [ ] `docker-compose.yml` avec PostgreSQL (volume, healthcheck) et **Redis** (optionnel via profil Compose)
- [ ] Application Angular créée dans `frontend/`
- [ ] Interface `ICacheService` + implémentation HybridCache (mémoire par défaut, Redis si configuré) enregistrée dans Program.cs
- [ ] CI de base : build + tests backend, build frontend

**Terminé quand** : `dotnet build`, `dotnet test`, `ng build` passent, `docker compose up` démarre PostgreSQL, la CI est verte.

### Sprint 1 : Authentification, rôles, annuaire

**Backend**
- [ ] Entité `AppUser`, `DbContext`, première migration, seed d'un compte admin via configuration (pas de mot de passe en dur dans le code)
- [ ] Identity + JWT + refresh token avec rotation
- [ ] Endpoints `auth/*` (login, refresh, logout, me)
- [ ] Connexion Google (`/auth/google`)
- [ ] Création d'utilisateur par l'admin + invitation (lien avec token ; l'envoi d'email peut être un `IEmailSender` factice qui journalise le lien en dev)
- [ ] `GET /users` avec recherche et pagination, liste des utilisateurs actifs **mise en cache (60 s)** et invalidée à chaque modification d'utilisateur
- [ ] Profil minimal de l'utilisateur (rôle, `IsActive`) en cache 1 min, vérifié à chaque requête authentifiée ; invalidé à la désactivation
- [ ] Rate limiting sur le login

**Frontend**
- [ ] Page de connexion (email/mot de passe + bouton Google)
- [ ] Intercepteur HTTP (ajout du token, refresh automatique sur 401), guards par rôle
- [ ] Page annuaire : liste, champ de recherche (avec debounce), pagination
- [ ] Layout principal (barre latérale + zone centrale)

**Tests** : login valide/invalide, verrouillage, refresh avec rotation, accès interdit d'une route admin pour un User, recherche d'utilisateurs.

**Terminé quand** : je peux me connecter (email et Google), voir l'annuaire, chercher, et un User ne peut pas appeler une route admin.

### Sprint 2 : Conversations privées et temps réel

**Backend**
- [ ] Entités `Conversation`, `ConversationParticipant`, `Message`
- [ ] `POST /conversations/direct` (création idempotente grâce à `DirectKey`)
- [ ] `ChatHub` : connexion authentifiée, groupes par conversation, `SendMessage`
- [ ] `IPresenceTracker` + événement `PresenceChanged` (implémentation mémoire, implémentation Redis prête derrière la même interface)
- [ ] Contrôle d'appartenance à une conversation **mis en cache (5 min)** pour le hub et l'API, avec invalidation
- [ ] `StartTyping` / `StopTyping` (non persistés)
- [ ] Historique paginé par curseur

**Frontend**
- [ ] Service SignalR (connexion, reconnexion automatique, ré-abonnement après reconnexion)
- [ ] Clic sur un utilisateur de l'annuaire → ouvre la conversation
- [ ] Liste des conversations, fil de messages (scroll infini vers le haut), zone de saisie
- [ ] Pastille en ligne / hors ligne, indicateur « X est en train d'écrire… » (disparaît après quelques secondes sans frappe)

**Tests** : pas de doublon de conversation directe, un non-participant ne reçoit/lit rien, présence avec plusieurs connexions.

**Terminé quand** : deux navigateurs connectés avec deux comptes s'échangent des messages instantanément, avec présence et saisie en cours.

### Sprint 3 : Groupes, modification, suppression, non lus

- [ ] `POST /admin/groups`, gestion des membres, archivage ; événement `AddedToGroup` pour que la conversation apparaisse en direct chez les membres
- [ ] Interface admin de gestion des groupes (liste, création, choix des membres)
- [ ] `EditMessage` et `DeleteMessage` (auteur uniquement), événements `MessageEdited` / `MessageDeleted`
- [ ] Interface : menu d'un message (modifier, supprimer), mention « modifié », « message supprimé »
- [ ] Non lus : `LastReadMessageId`, compteur par conversation, `MarkAsRead` quand la conversation est visible
- [ ] Ajout/retrait de membres et archivage **invalident** le cache d'appartenance
- [ ] Un User ne peut pas créer de groupe (test explicite)

**Tests** : modification/suppression par un non-auteur refusée, groupe créé par un non-admin refusé, calcul des non lus.

**Terminé quand** : l'admin crée un groupe, ses membres le voient apparaître en direct, tout le monde peut discuter, éditer et supprimer ses messages, et les compteurs de non lus sont exacts.

### Sprint 4 : Fichiers et messages vocaux

- [ ] Interface `IFileStorage` + implémentation disque (dev) + implémentation S3-compatible (prod, par configuration)
- [ ] `POST /conversations/{id}/attachments` : validation (type, taille, type réel), création de l'`Attachment` et du `Message`, diffusion SignalR
- [ ] `GET /attachments/{id}` réservé aux participants
- [ ] Frontend : bouton pièce jointe, aperçu des images, carte de fichier (nom, taille, téléchargement), barre de progression d'upload
- [ ] Message vocal : enregistrement via `MediaRecorder` (demande de permission micro, minuteur, annuler/envoyer), lecteur audio avec durée dans la bulle
- [ ] Compatibilité : Chrome/Firefox produisent du `webm/opus`, Safari de l'`mp4` ; stocker le `ContentType` réel et tester la lecture

**Tests** : fichier interdit refusé, fichier trop gros refusé, téléchargement par un non-participant refusé.

**Terminé quand** : j'envoie un fichier, une image et un vocal, et l'autre personne les reçoit en direct et peut les ouvrir.

### Sprint 5 : Appels audio, vidéo et partage d'écran

**Signalisation (backend)**
- [ ] Méthodes du hub pour le cycle d'appel, `CallLog` mis à jour à chaque étape
- [ ] Contrôles : destinataire actif et en ligne, pas déjà en appel, délai de 30 s → `Missed`
- [ ] `GET /calls/ice-servers` (STUN public + TURN configurable par variables d'environnement)

**Client WebRTC (frontend)**
- [ ] Service `CallService` : `RTCPeerConnection`, gestion des candidats ICE, états de l'appel
- [ ] Écran d'appel entrant (sonnerie, accepter / refuser) et d'appel sortant (annuler)
- [ ] Appel **audio** par défaut ; bouton **caméra** pour activer/couper la vidéo pendant l'appel (renégociation SDP quand une piste est ajoutée)
- [ ] Bouton **micro** (mute), bouton **raccrocher**
- [ ] **Partage d'écran** : `getDisplayMedia()`, ajout/retrait de la piste, gérer l'arrêt via le bouton natif du navigateur
- [ ] Libérer proprement caméra/micro/écran à la fin de l'appel
- [ ] Message système « Appel manqué / terminé (durée) » dans la conversation

**Notes techniques**
- Caméra, micro et partage d'écran exigent **HTTPS** (ou `localhost`).
- STUN seul suffit souvent ; **TURN** est nécessaire derrière des réseaux stricts. Prévoir `coturn` dans `docker-compose` pour les essais, et documenter dans le README où l'héberger en prod (VPS ; les hébergeurs web classiques ne gèrent pas l'UDP). Utiliser des identifiants TURN temporaires générés par l'API.
- Appels **1-à-1 uniquement**.

**Tests** : un tiers ne peut pas recevoir/relayer la signalisation d'un appel, appel vers utilisateur hors ligne refusé, transitions de statut de `CallLog`. Test manuel documenté : appel entre deux navigateurs, avec et sans caméra, avec partage d'écran.

**Terminé quand** : deux utilisateurs peuvent s'appeler en audio, activer/couper la caméra en cours d'appel et partager leur écran.

### Sprint 6 : Dashboard admin, qualité, déploiement

- [ ] `GET /admin/stats` + page dashboard (utilisateurs actifs, messages par jour, groupes les plus actifs ; graphiques), résultat **mis en cache 5 min**
- [ ] Page de gestion des utilisateurs (créer/inviter, désactiver, changer le rôle) ; la désactivation **déconnecte** l'utilisateur (révocation des refresh tokens, fermeture des connexions SignalR)
- [ ] Gestion d'erreurs globale (ProblemDetails), états de chargement et d'erreur côté Angular
- [ ] Tests d'intégration avec Testcontainers (PostgreSQL, et Redis pour les tests de cache) sur les parcours principaux
- [ ] Tests d'invalidation du cache (section 8 bis)
- [ ] Backplane Redis SignalR activable par configuration ; Redis managé en production si plusieurs instances (offre gratuite à vérifier, ex. Upstash)
- [ ] `Dockerfile` backend (multi-stage), `Dockerfile` frontend si utile, `docker-compose` complet (api, db, coturn)
- [ ] GitHub Actions : build, tests, image Docker, déploiement
- [ ] Frontend déployé sur **Vercel** (variables d'environnement pour l'URL de l'API ; `vercel.json` avec réécriture vers `index.html` pour le routage Angular)
- [ ] Backend déployé en Docker (Azure App Service ou Render) avec **WebSockets activés**, base sur Neon, stockage objet pour les fichiers
- [ ] Swagger activé hors production
- [ ] `README.md` final : description, captures d'écran, schéma d'architecture, installation locale en 3 commandes, variables d'environnement, lien de démo, comptes de test

**Terminé quand** : l'application est en ligne, la CI est verte, le README permet à un inconnu de la lancer en local, et la démo (deux onglets, chat, vocal, fichier, appel) fonctionne.

> **Offres gratuites** : les paliers gratuits des hébergeurs s'endorment après inactivité (premier appel lent) et changent souvent. Vérifie les conditions actuelles et mentionne le démarrage à froid dans le README.

---

## 10. Variables d'environnement (à documenter dans `.env.example`)

```
ConnectionStrings__Default=
Jwt__Issuer=
Jwt__Audience=
Jwt__Secret=
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=7
Google__ClientId=
Cors__AllowedOrigins=
Redis__ConnectionString=          # vide = cache mémoire seul
Cache__DefaultTtlSeconds=60
Storage__Provider=Local|S3
Storage__S3__Endpoint=
Storage__S3__Bucket=
Storage__S3__AccessKey=
Storage__S3__SecretKey=
Turn__Urls=
Turn__Secret=
Admin__SeedEmail=
Admin__SeedPassword=
```

---

## 11. Définition de terminé (valable pour chaque tâche)

- Le code compile sans avertissement bloquant et suit la structure en couches
- Les règles de sécurité de la section 8 concernées sont appliquées côté serveur
- Les tests du sprint passent ; les cas d'autorisation sont couverts
- Si des données sont mises en cache : TTL, clé et invalidation vérifiés par un test
- Le comportement a été **essayé réellement** (application lancée), pas seulement relu
- `PROGRESS.md` est à jour et les cases du sprint sont cochées
- Les commits sont propres et la pull request est prête

---

## 12. Premier pas

Commence par le **Sprint 0**, puis arrête-toi et fais-moi un résumé de ce qui a été créé, des commandes pour le lancer, et de tes éventuelles questions avant le Sprint 1.
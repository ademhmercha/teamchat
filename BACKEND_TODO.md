# Backend — logiques manquantes (à implémenter plus tard)

Liste des **logiques manquantes côté backend**, identifiées pendant la refonte UX/UI du
frontend. Objectif : pouvoir tout brancher plus tard sans revenir dessus. Chaque entrée
décrit le **besoin**, ce qui **manque** (endpoint / DTO / entité / règle métier) et l'**impact
frontend** en attente.

> Statut des endpoints existants (référence rapide) :
> `api/conversations` (liste, direct, messages, read), `api/admin/users` (liste, create, patch),
> `api/admin/groups` (liste, create, members, archive), `api/admin/stats`, `api/users`
> (recherche, détail), `api/auth` (login, google, refresh, logout, accept-invite, me),
> `api/attachments`, `api/calls` (ice-servers, history) + `hubs/chat` (messages, édition,
> suppression, typing, appels 1:1, présence).

---

## Priorité haute — bloquent des fonctions déjà visibles dans l'UI

### 1. Utilisateurs (administration)
- [ ] **Supprimer un compte** — `DELETE /api/admin/users/{id}`. Service + garde métier.
- [ ] **Protection du dernier administrateur actif** — interdire la désactivation/rétrogradation
      si c'est le seul admin actif (règle dans `AdminUserService.UpdateAsync`).
- [ ] **Anonymiser / conserver les messages** d'un compte supprimé (choix produit à valider :
      suppression logique du compte, conservation des messages avec auteur « Utilisateur supprimé »).
- [ ] **Réinitialiser le mot de passe par un admin** — `POST /api/admin/users/{id}/reset-password`
      (déclenche une nouvelle invitation / un mot de passe temporaire).
- [ ] **Invitation réellement envoyée** — `CreateAsync` génère un lien mais l'envoi e-mail est un
      no-op en dev ; intégrer un vrai fournisseur SMTP + renvoi/expiration/regénération du token.
- [ ] **Champs de profil étendus** côté admin (téléphone, fonction/poste) si souhaités dans le tableau.

### 2. Profil de l'utilisateur courant
- [ ] **Modifier son profil** — `PATCH /api/users/me` (nom affiché, avatar, statut/message perso).
- [ ] **Uploader un avatar** — `POST /api/users/me/avatar` (aucun endpoint d'envoi n'existe ;
      `AvatarUrl` existe dans les DTO mais n'est jamais alimenté). Stockage local/S3 à réutiliser.
- [ ] **Changer son mot de passe** — `POST /api/auth/change-password` (avec mot de passe actuel).
- [ ] **Déconnexion globale** — révoquer tous les refresh tokens (`/api/auth/logout-all`).
- [ ] (Impact front) : aucune page « Mon compte / Profil » n'existe encore.

### 3. Conversations & messages
- [ ] **Détail d'une conversation** — `GET /api/conversations/{id}` (participants complets, nom,
      date de création, description) pour l'en-tête et le panneau Détails.
- [ ] **Membres d'une conversation** — `GET /api/conversations/{id}/members` (les `Participants`
      existent dans `ConversationDto` mais aucun endpoint ne les expose pour un groupe existant).
- [ ] **Quitter un groupe** — `POST /api/conversations/{id}/leave`.
- [ ] **Archiver / masquer côté utilisateur** — `PATCH /api/conversations/{id}` `{ archived }`
      **+ champ `Archived` dans `ConversationListItemDto`** (actuellement absent → impossible
      d'afficher/filtrer les archivées).
- [ ] **Supprimer une conversation 1:1** (masquage par utilisateur).
- [ ] **Répondre à un message (citation)** — champ `ReplyToMessageId` + relation + rendu.
- [ ] **Réactions emoji** — entité dédiée + `POST/DELETE` + diffusion temps réel (`IChatClient`).
- [ ] **Épingler un message** — champ `Pinned` + endpoint + liste des épinglés.
- [ ] **Accusés de lecture des autres (« vu par »)** — actuellement self-only.
- [ ] **Recherche dans les messages** — `GET /api/conversations/{id}/messages/search?q=`.
- [ ] **Fichiers partagés d'une conversation** — `GET /api/conversations/{id}/attachments`
      (alimente l'onglet « Fichiers » du panneau Détails).
- [ ] **Marquer comme non lu (manuel)** et fiabilité du compteur `UnreadCount`.
- [ ] **Notification hors application** (push / e-mail) à la réception d'un message — absente.

### 4. Notifications
- [ ] **Persistance** : entité `Notification` + `GET /api/notifications` +
      `PATCH /api/notifications/{id}/read` + `POST /api/notifications/read-all`.
- [ ] **Événement temps réel dédié** dans `IChatClient` (le client déduit aujourd'hui via
      `ReceiveMessage` / `ConversationUpdated`).
- [ ] (Impact front) : la **cloche de la barre latérale n'a aucun handler**.

### 5. Groupes (administration)
- [ ] **Désarchiver** — `POST /api/admin/groups/{id}/unarchive` (`ArchiveAsync` existe, pas l'inverse).
- [ ] **Renommer** — `PUT /api/admin/groups/{id}` (seul `Create` fixe le nom aujourd'hui).
- [ ] **Détail d'un groupe** — `GET /api/admin/groups/{id}`.
- [ ] **Supprimer définitivement** un groupe (distinct de l'archivage).

---

## Priorité moyenne — compléments fonctionnels

### 6. Journal d'activité / audit
- [ ] **Table d'audit** (qui a créé/désactivé un compte, archivé un groupe, modifié les membres…).
- [ ] Endpoint de consultation + filtres, réservé aux admins.
- [ ] (Les stats agrégées existent via `api/admin/stats` ; l'audit détaillé n'existe pas.)

### 7. Paramètres de l'application
- [ ] Aucun endpoint : nom/logo de l'organisation, politiques, serveurs TURN, limites de taille,
      types de fichiers autorisés (aujourd'hui figés dans la configuration).

### 8. Authentification
- [ ] **Mot de passe oublié** — `POST /api/auth/forgot-password` + `POST /api/auth/reset-password`.
- [ ] **Vérification d'e-mail** à l'activation du compte.
- [ ] **Google Sign-in** — l'endpoint `POST /api/auth/google` existe ; il manque la configuration
      (clientId / audience) et la validation d'audience côté serveur.
- [ ] **Rotation / révocation de la famille de refresh tokens** (à vérifier).

### 9. Appels
- [ ] **Appels de groupe / conférence** (aujourd'hui 1:1 uniquement).
- [ ] **Enregistrement d'appel** (si fonction requise).
- [ ] (L'historique `GET /api/calls/history` existe déjà côté backend.)

---

## Priorité basse — transversal

### 10. Pièces jointes & infrastructure
- [ ] **Quotas** par utilisateur/organisation, **taille max** et **validation des types** de fichiers.
- [ ] **Rétention / suppression définitive** des messages et pièces jointes (RGPD).
- [ ] **Présence persistée** (aujourd'hui en mémoire via `IPresenceTracker` ; OK mono-instance,
      à durcir si montée en charge malgré le backplane Redis).

---

## Côté front (à raccorder une fois le backend prêt)

Ces éléments existent ou sont amorcés côté frontend mais **dépendent des endpoints ci-dessus** :
- Cloche de notifications (aucun handler) → items §4.
- Page « Mon compte / Profil » → items §2.
- Boutons « Supprimer un utilisateur » → item §1.
- Onglet « Fichiers » du panneau Détails → item §3 (`attachments`).
- Filtre/affiche « archivées » des conversations → item §3.
- Historique des appels (endpoint prêt, **pas d'UI**) → item §9.
- Réponses / réactions / épingles → item §3.

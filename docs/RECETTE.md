# Recette manuelle de TeamChat (production AWS)

À dérouler sur l'URL CloudFront (`terraform output cloudfront_url`), **en HTTPS**, avec deux navigateurs (ou un navigateur
et une fenêtre de navigation privée) et deux adresses email réelles. Cochez au fur et à mesure.

## 0. Avant de commencer

- [ ] **Google Sign-In** : console Google Cloud > APIs et services > Identifiants > votre *ID client OAuth* (type « Application Web »)
  > **Origines JavaScript autorisées** : ajouter `https://<votre-distribution>.cloudfront.net` (et plus tard votre domaine
  personnalisé). Pas d'URI de redirection à ajouter : la connexion utilise le jeton d'identité Google côté navigateur.
  Le changement peut mettre quelques minutes à s'appliquer.
- [ ] Le même identifiant client est dans `TF_VAR_google_client_id` : il alimente `Google__ClientId` (API, SSM) **et**
  `GOOGLE_CLIENT_ID` (build du frontend, sinon le bouton Google est masqué). Après l'avoir défini : `terraform apply`
  puis un nouveau build (push sur `main` ou *Release change*).
- [ ] L'EC2 et RDS sont démarrés (hors 23h-8h UTC+1), le pipeline `teamchat-prod` est vert.
- [ ] SES : l'adresse d'expédition est vérifiée. En *sandbox*, **les destinataires doivent aussi être vérifiés** dans SES.
- [ ] Ouvrir les outils de développement (onglet **Réseau** et **Console**) pour les points marqués 🔍.

## 1. Entreprise et comptes

- [ ] **Créer une entreprise** (`/register`) : l'inscription réussit, l'utilisateur est Owner et arrive sur `/app`.
- [ ] Email de **bienvenue** reçu (SES) avec un lien vers l'application.
- [ ] **Déconnexion**, puis **connexion** email + mot de passe (`/login`).
- [ ] **Mauvais mot de passe** : message d'erreur propre, pas de plantage ; plusieurs échecs : verrouillage temporaire.
- [ ] **Connexion Google** : le bouton apparaît, la connexion fonctionne avec un compte déjà créé dans l'entreprise
  (un compte Google inconnu doit être refusé : pas d'auto-inscription).
- [ ] **Mot de passe oublié** : l'email SES arrive, le lien ouvre la page de réinitialisation **sur le domaine CloudFront**
  (pas sur l'adresse de l'EC2), le nouveau mot de passe permet de se connecter.

## 2. Messagerie temps réel (deux navigateurs, deux comptes)

- [ ] L'**annuaire** liste l'autre utilisateur ; pastille **en ligne / hors ligne** correcte ; recherche (nom, email).
- [ ] Ouvrir une **conversation privée** ; un message envoyé apparaît **instantanément** chez l'autre (🔍 Réseau : une connexion
  WebSocket vers `/hubs/chat`, statut 101).
- [ ] « **X est en train d'écrire…** » apparaît puis disparaît.
- [ ] **Modifier** puis **supprimer** son message : visible en direct des deux côtés (« modifié », « message supprimé »).
- [ ] **Non lus** : le compteur monte chez le destinataire, retombe à zéro à l'ouverture de la conversation.
- [ ] **Fichier** : envoi d'un PDF et d'une image ; aperçu de l'image, carte de fichier, téléchargement chez l'autre.
- [ ] **Message vocal** : autoriser le micro, enregistrer, envoyer ; lecture chez l'autre (durée affichée).
- [ ] **Avatar** : changer sa photo dans « Mon compte » ; elle s'affiche partout. 🔍 Console : **aucune** erreur *Mixed Content*,
  aucune requête `http://` (l'URL de l'avatar est `/api/users/<id>/avatar`).
- [ ] 🔍 Réseau : **aucune** requête vers `http://ec2-…amazonaws.com` ni vers `railway.app` / `vercel.app`.

## 3. Appels WebRTC

- [ ] **Appel audio** : sonnerie chez l'appelé, accepter ; on s'entend dans les deux sens. Refuser et raccrocher fonctionnent.
- [ ] **Caméra** : l'activer puis la couper pendant l'appel (renégociation).
- [ ] **Partage d'écran** : démarrer, arrêter, et arrêter via le bouton natif du navigateur.
- [ ] Appel vers un utilisateur **hors ligne** : refusé avec un message clair. Sans réponse après ~30 s : « appel manqué ».
- [ ] Caméra, micro et écran sont **libérés** à la fin de l'appel (le témoin du navigateur s'éteint).
- ⚠️ Seul le STUN public est configuré tant que `Turn__Urls`/`Turn__Secret` ne sont pas renseignés (prompt 6.2) : un appel
  entre deux réseaux stricts (NAT symétrique, entreprise) peut échouer sans serveur TURN. Tester d'abord sur deux réseaux
  « ordinaires » (ex. Wi-Fi et 4G).

## 4. Administration (Owner / Admin)

- [ ] **Utilisateurs** : créer un utilisateur (mot de passe saisi), changer son rôle, le désactiver : il est **déconnecté**
  immédiatement et ne peut plus se reconnecter ; le réactiver.
- [ ] Un **Admin** ne peut pas créer, modifier ou supprimer un Admin ni l'Owner (403) ; un **User** n'a pas accès aux pages admin.
- [ ] **Groupes** : créer un groupe avec des membres ; ils le voient apparaître **en direct** ; discuter dedans ; retirer un
  membre (il ne peut plus lire ni écrire) ; archiver le groupe.
- [ ] **Tableau de bord** : utilisateurs actifs, messages par jour, groupes les plus actifs (graphiques affichés).
- [ ] **Renommer l'entreprise** (Owner) ; un Admin reçoit une erreur 403.

## 5. Navigation et robustesse

- [ ] **Rechargement d'une page profonde** (F5 sur `/app/conversations/<id>`, puis ouverture directe de l'URL dans un nouvel
  onglet) : l'application se charge (pas de page d'erreur CloudFront / S3), la session est conservée.
- [ ] Une URL inconnue (`/n-importe-quoi`) affiche l'application (pas une erreur XML de S3).
- [ ] **Mise à jour de l'application** : après un déploiement, un rechargement récupère la nouvelle version (service worker).
- [ ] **Hors ligne** : couper le réseau de l'ordinateur → page « hors ligne » ; le rétablir → retour à l'application.
- [ ] **Mode veille** : lancer la Lambda `teamchat-prod-scheduler` avec `{"action":"stop"}` (console Lambda), attendre l'arrêt
  de l'EC2, recharger l'application → redirection vers **« La démo est en veille (23h-8h UTC+1) »** ; lancer
  `{"action":"start"}` → la page revient seule à l'application (jusqu'à ~10 min : démarrage de RDS puis de l'EC2).
- [ ] **Alerte** : `aws cloudwatch set-alarm-state --alarm-name teamchat-prod-api-errors --state-value ALARM --state-reason recette`
  → email SNS reçu.

## 6. Avant de dire « terminé »

- [ ] Pipeline vert sur un push de test ; statut `teamchat-ci` visible sur une pull request.
- [ ] `docs/RESTORE.md` §5 : test de restauration RDS réalisé au moins une fois.
- [ ] Budget et alertes SNS reçus ; `import_existing_budget` repassé à `false`.
- [ ] Anciennes clés d'accès AWS désactivées (voir `docs/SECURITY.md` §2).

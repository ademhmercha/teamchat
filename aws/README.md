# Deploiement AWS

```
Navigateur ──HTTPS──> CloudFront ─┬─ /*        -> S3 (frontend Angular)  + CloudFront Function spa-rewrite
                                  ├─ /api/*    -> EC2 :80 (conteneur API .NET)
                                  └─ /hubs/*   -> EC2 :80 (SignalR / WebSockets)
EC2 (Docker) ──> RDS PostgreSQL     EC2 ──> S3 (pieces jointes, via role IAM)
EC2 <── SSM Parameter Store (secrets)   EC2 ──> CloudWatch Logs (/teamchat/api)

GitHub (push main) -> CodePipeline -> CodeBuild (tests, image ECR, frontend S3) -> CodeDeploy (EC2)
GitHub (pull request) -> CodeBuild teamchat-ci (aws/buildspec-ci.yml), statut affiche sur la PR
```

## Fichiers

| Fichier | Role |
|---|---|
| `buildspec.yml` | Build du pipeline : tests, image Docker -> ECR, frontend -> S3, invalidation CloudFront |
| `aws/buildspec-ci.yml` | Verification des pull requests (aucun deploiement) |
| `aws/codedeploy/appspec.yml` + `scripts/` | Deploiement sur EC2 : secrets SSM, `docker pull`, redemarrage, sonde `/api/health` |
| `aws/cloudfront/spa-rewrite.js` | CloudFront Function : routes Angular -> `index.html` |

## Infrastructure : Terraform (`infra/`)

Toute l'infrastructure de production est decrite en Terraform (region eu-north-1, tags `Project` et `Environment`).

| Fichier | Contenu |
|---|---|
| `infra/bootstrap/` | Bucket S3 du state (versioning, chiffre, prive), state local, lance une seule fois |
| `network.tf` | VPC par defaut ; groupes de securite EC2 (port 80 depuis CloudFront seulement, pas de SSH) et RDS (5432 depuis l'EC2) |
| `database.tf` | RDS PostgreSQL 16, `db.t4g.micro`, 20 Go gp3, prive, chiffre, sauvegardes 1 jour (Free plan ; `rds_backup_retention_days = 7` apres passage au plan payant) |
| `storage.tf` | Buckets frontend (OAC) et uploads (versioning, anciennes versions purgees a 30 jours) |
| `ecr.tf` | Depot `teamchat-api` (scan a l'envoi, 10 images conservees) |
| `ssm.tf` | Parametres `/teamchat/<env>/...` (nom du parametre = nom de la variable d'environnement) |
| `compute.tf` | Role IAM EC2 (moindre privilege), EC2 t3.micro AL2023 (IMDSv2, Docker, agent CodeDeploy, swap), IP elastique, log group `/teamchat/api` |
| `cdn.tf` | CloudFront : `/*` -> S3 (+ `spa-rewrite.js`), `/api/*` et `/hubs/*` -> EC2 |
| `ses.tf` | Identite d'expedition SES |
| `scheduler.tf` + `lambda/scheduler/` | Lambda Python 3.13 + EventBridge Scheduler : arret/demarrage nocturne de l'EC2 et de RDS |
| `alerts.tf` | Sujet SNS `teamchat-alerts-<env>` (notifications du scheduler, alarmes) |
| `cicd.tf` | Connexion GitHub, bucket d'artefacts, CodeBuild (`teamchat-build`, `teamchat-ci`), CodeDeploy, CodePipeline V2 |

### Commandes

```bash
# 0. Une fois : connexion AWS (jamais de cle dans le depot)
aws configure sso && aws sts get-caller-identity

# 1. Bootstrap du state (une seule fois par compte)
cd infra/bootstrap && terraform init && terraform apply
#    -> affiche state_bucket = teamchat-tfstate-<ACCOUNT_ID>

# 2. Infrastructure
cd ../
terraform init -backend-config=envs/prod.backend.hcl -backend-config="bucket=teamchat-tfstate-<ACCOUNT_ID>"
export TF_VAR_ses_from_address="no-reply@ton-domaine.tld"        # obligatoire
export TF_VAR_admin_seed_email="..." TF_VAR_admin_seed_password="..."   # optionnels (SuperAdmin initial)
terraform plan  -var-file=envs/prod.tfvars     # verifier : pas de NAT Gateway, pas d'ALB
terraform apply -var-file=envs/prod.tfvars
```

Les secrets (`admin_seed_password`, `google_client_id`) ne vont jamais dans un fichier committe :
variables `TF_VAR_*` ou fichier `*.auto.tfvars` local (ignore par git). Les valeurs sensibles
(mot de passe RDS, secret JWT) sont aussi dans le state, d'ou le bucket prive et chiffre.

### Region : eu-north-1 (Stockholm)

Le compte est en Free plan : une SCP refuse la creation de ressources hors de `eu-north-1`.
Tout le projet y est donc deploye. Les services de la region et du compte :
`t3.micro`, `db.t4g.micro` (PostgreSQL 16), CodeBuild/CodePipeline/CodeDeploy, SES et ECR y sont proposes ;
verifie-le sur ton compte (lecture seule) :

```bash
aws ec2 describe-instance-type-offerings --region eu-north-1 --filters Name=instance-type,Values=t3.micro
aws rds describe-orderable-db-instance-options --region eu-north-1 --engine postgres --engine-version 16   --db-instance-class db.t4g.micro --query 'OrderableDBInstanceOptions[0].DBInstanceClass'
```

Ressources imposees en **us-east-1** (optionnelles, `enable_us_east_1_resources = false` par defaut, voir `infra/us_east_1.tf`) :
certificat ACM d'un domaine personnalise pour CloudFront (`custom_domain`) et alarme 5xx CloudFront + sujet SNS (`alert_email`).
Les ressources CloudFront elles-memes et IAM sont des services globaux : si la SCP les refuse aussi, le `apply` le dira
(erreur `AccessDenied` explicite) ; il faudra alors demander l'exemption a AWS Support.

### Etapes manuelles restantes

1. **Budget** : AWS Budgets, budget mensuel avec alerte email (a faire avant tout `apply`).
2. **SES** : cliquer sur le lien de verification recu a `ses_from_address`, puis demander l'acces
   production (sinon sandbox : envoi seulement vers des adresses verifiees).
3. **Premier deploiement** : l'URL CloudFront (`terraform output cloudfront_url`) affiche une erreur
   tant que le pipeline n'a pas publie le frontend et l'image API (phase 3).
4. **TURN / Google** (si utilises) : creer a la main les parametres `Turn__Urls`, `Turn__Secret`
   sous `/teamchat/prod/` (non geres par Terraform), `Google__ClientId` via `TF_VAR_google_client_id`.
5. **Connexion GitHub** : apres le `apply`, console AWS > Developer Tools > Settings > Connections >
   `teamchat-github` > *Update pending connection* (autorise l'application AWS sur le depot).
   Tant qu'elle est `PENDING`, l'etape Source du pipeline echoue. Ensuite : *Release change* sur le pipeline
   (ou un push sur `main`).
6. **Verification des pull requests** : une fois la connexion validee, mettre `enable_pr_checks = true`
   dans `envs/prod.tfvars` et refaire un `apply` (cree `teamchat-ci`, son identifiant GitHub et le webhook).

Notes :
- La chaine de connexion utilise `SSL Mode=Require;Trust Server Certificate=true` : le trafic vers RDS est chiffre,
  mais l'autorite de certification RDS n'est pas dans l'image `aspnet`.
- Le groupe de securite de l'EC2 consomme ~55 regles de quota a cause de la liste de prefixes CloudFront.

## Arret nocturne (economies)

La Lambda `teamchat-<env>-scheduler` (`infra/lambda/scheduler/handler.py`) agit sur les ressources taguees `AutoSchedule=true`
(l'EC2 et la base RDS). Fuseau `Africa/Lagos`, heures modifiables (`scheduler_stop_hour`, `scheduler_start_hour`) :

| Planification | Action |
|---|---|
| 23h00 | `stop` : arrete l'EC2, puis RDS |
| 08h00 | `start` : demarre RDS, **attend** qu'elle soit disponible (jusqu'a 10 min), puis demarre l'EC2 |
| toutes les heures | `enforce` : hors des heures d'activite, re-arrete RDS si elle a redemarre seule (AWS la relance apres 7 jours d'arret) |

- Resultat de chaque action : email via le sujet SNS (l'`enforce` ne previent que s'il a agi). Journaux 14 jours.
- Role IAM : `Start/Stop` uniquement sur les ressources portant le tag (condition `aws:ResourceTag/AutoSchedule`).
- Tout couper : `scheduler_enabled = false` puis `apply` (les planifications passent en `DISABLED`, l'infra reste allumee).
- **Suspendre sans redeployer** : mettre le tag `AutoSchedule=false` sur l'EC2 et/ou RDS. La Lambda ignore alors la ressource
  pour `stop`, `start` et `enforce` (et le signale dans la notification). Terraform n'ecrase pas ce tag (`ignore_changes`).
  ```bash
  aws ec2 create-tags --resources <instance_id> --tags Key=AutoSchedule,Value=false
  aws rds add-tags-to-resource --resource-name <arn-de-la-base> --tags Key=AutoSchedule,Value=false   # ... Value=true pour reprendre
  ```
- **Premier deploiement** : comme Terraform ignore ce tag, une EC2/RDS creee AVANT l'ajout du tag ne le recevra pas par `apply`.
  Ajoute-le une fois a la main (commandes ci-dessus avec `Value=true`). Si le tag manque partout, la Lambda s'arrete en erreur
  et le dit dans l'email SNS au lieu de ne rien faire.
- **Delais** : timeout de la Lambda 900 s (maximum), attente de RDS `RDS_WAIT_SECONDS` = 780 s (un garde-fou Terraform impose
  au moins 60 s de marge) ; le code garde en plus 45 s de securite avant de demarrer l'EC2.
- **Page de veille** : CloudFront remplace les 502/503/504 par `/maintenance.html` (`frontend/public/`, servie par le bucket frontend,
  deployee avec le build). Les autres reponses de l'API, 404 compris, ne sont pas modifiees.
  - **Redirection Angular** : l'intercepteur HTTP (et l'echec de negociation SignalR) redirige vers
    `/maintenance.html?from=<URL courante>` sur 502/503/504, ou erreur reseau (status 0) quand le navigateur est en ligne,
    **uniquement** pour `/api/*` et `/hubs/*` ; jamais sur 401/403/404/500. Hors ligne, c'est la page `/offline`.
  - **Pas de boucle** : aucune redirection depuis `/maintenance.html`, ni deux fois en moins de 30 s (`sessionStorage`).
    La page sonde `/api/health` (5 s puis toutes les 30 s) et revient a `from` (chemin du meme site uniquement).
  - **Service worker** : `ngsw-config.json` exclut `/maintenance.html`, `/api/**` et `/hubs/**` de `navigationUrls`
    (le SW ne sert donc jamais `index.html` a leur place).
  - **Horaires affiches** : Terraform genere `maintenance-config.json` dans le bucket frontend (`scheduler_*`), lu par la page.
    Texte de repli dans le HTML si le fichier est absent. `buildspec.yml` l'exclut du `s3 sync --delete`.
  - Tests : `cd frontend && npx ng test --no-watch`.

Economie estimee (prix indicatifs eu-north-1, a verifier sur la page de prix AWS) : 9 h d'arret par jour = ~270 h/mois ;
EC2 t3.micro ~0,009 $/h (~2,5 $) + RDS db.t4g.micro ~0,016 $/h (~4,3 $) = **~7 $/mois** sur ~26 $ (reste facture :
stockage RDS, volume EBS, IPv4 publique ~3,6 $). Eteindre aussi le week-end ajouterait ~4 a 5 $/mois (non implemente).

## CI/CD

- **Push sur `main`** : CodePipeline V2 -> `teamchat-build` (`buildspec.yml` : tests unitaires et d'integration,
  image ECR, frontend S3 + invalidation CloudFront) -> CodeDeploy (`prod`, `AllAtOnce`, rollback si echec).
- **Pull request** : `teamchat-ci` (`aws/buildspec-ci.yml`) : build, tests unitaires, image Docker et frontend.
  Les tests d'integration n'y tournent pas par defaut (`RUN_INTEGRATION_TESTS=true` sur le projet pour les activer).
- Image CodeBuild `aws/codebuild/standard:7.0` : Node 22 et .NET 10 sont deja installes (pas de `dotnet-install.sh`).
- Les rapports de tests (TRX) apparaissent dans CodeBuild > Report groups.
- Si Docker Hub limite les telechargements (erreur `toomanyrequests` dans les tests d'integration), definir
  `RUN_INTEGRATION_TESTS=false` sur `teamchat-build` le temps de corriger, ou passer a `BUILD_GENERAL1_MEDIUM`
  (`codebuild_compute_type`) si les tests sont lents.

Estimation (non mesuree, a verifier apres les premiers builds) : ~12 a 15 min par execution `main`. Pour 20 deploiements
par mois : ~250 min de CodeBuild `general1.small` (100 min gratuites/mois, puis ~0,005 $/min, soit ~1 $)
et ~350 min d'actions CodePipeline V2 (100 min gratuites, puis 0,002 $/min, soit ~0,5 $). Environ 1 a 2 $/mois.

## Migrer les donnees depuis Railway (optionnel)

```bash
pg_dump --no-owner --no-acl -Fc "<url-postgres-railway>" -f teamchat.dump
pg_restore --no-owner --no-acl -d "<url-postgres-rds>" teamchat.dump
```
(a lancer depuis l'EC2 via Session Manager, RDS n'etant pas public).
Les fichiers deja envoyes sur Railway (stockage local) sont a copier dans le bucket uploads
en conservant les memes cles.

## Couts indicatifs (eu-north-1)

EC2 t3.micro ~7,5 $/mois, RDS db.t4g.micro + 20 Go ~15 $/mois, IPv4 publique ~3,6 $/mois,
S3/CloudFront/ECR/CloudWatch ~1 $/mois, CodePipeline/CodeBuild quelques centimes.
Arreter l'EC2 et la base RDS quand le projet n'est pas utilise.

## Amazon SES (emails)

- Verifier l'adresse (ou le domaine) d'expediteur dans SES, region `eu-north-1`.
- Un compte SES neuf est en **sandbox** : envoi uniquement vers des adresses verifiees, 200 mails/jour.
  Demander l'acces production (SES > Account dashboard) avant d'ecrire a de vrais utilisateurs.
- Un echec d'envoi est journalise (CloudWatch `/teamchat/api`) sans faire echouer la requete.
- Emails envoyes aujourd'hui : mot de passe oublie et bienvenue (creation d'entreprise, uniquement si
  `App__PublicUrl` est defini). Le gabarit d'invitation existe mais n'est pas utilise : l'admin cree
  les comptes avec un mot de passe, sans email (CLAUDE.md).

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
| `database.tf` | RDS PostgreSQL 16, `db.t4g.micro`, 20 Go gp3, prive, chiffre, sauvegardes 7 jours |
| `storage.tf` | Buckets frontend (OAC) et uploads (versioning, anciennes versions purgees a 30 jours) |
| `ecr.tf` | Depot `teamchat-api` (scan a l'envoi, 10 images conservees) |
| `ssm.tf` | Parametres `/teamchat/<env>/...` (nom du parametre = nom de la variable d'environnement) |
| `compute.tf` | Role IAM EC2 (moindre privilege), EC2 t3.micro AL2023 (IMDSv2, Docker, agent CodeDeploy, swap), IP elastique, log group `/teamchat/api` |
| `cdn.tf` | CloudFront : `/*` -> S3 (+ `spa-rewrite.js`), `/api/*` et `/hubs/*` -> EC2 |
| `ses.tf` | Identite d'expedition SES |

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
5. **Pipeline CI/CD** (CodePipeline, CodeBuild, CodeDeploy) : phase 3, `infra/cicd.tf`.

Notes :
- La chaine de connexion utilise `SSL Mode=Require;Trust Server Certificate=true` : le trafic vers RDS est chiffre,
  mais l'autorite de certification RDS n'est pas dans l'image `aspnet`.
- Le groupe de securite de l'EC2 consomme ~55 regles de quota a cause de la liste de prefixes CloudFront.

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

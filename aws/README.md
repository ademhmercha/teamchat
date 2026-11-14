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

## Ressources a creer (une region, ex. eu-west-1)

1. **AWS Budgets** : budget mensuel avec alerte email.
2. **S3** : `teamchat-frontend-<compte>` (prive) et `teamchat-uploads-<compte>` (prive).
3. **ECR** : depot `teamchat-api` (regle de cycle de vie : garder les 10 dernieres images).
4. **RDS PostgreSQL 16** : `db.t4g.micro`, 20 Go gp3, pas d'acces public,
   groupe de securite qui n'accepte le port 5432 que depuis le groupe de securite de l'EC2.
5. **SSM Parameter Store**, sous `/teamchat/prod/` (SecureString pour les secrets) :
   - `ConnectionStrings__Default` = `Host=<endpoint-rds>;Port=5432;Database=teamchat;Username=...;Password=...;SSL Mode=Require`
   - `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`
   - `Cors__AllowedOrigins` = `https://<domaine-cloudfront>`
   - `Storage__Provider` = `S3`, `Storage__S3__Bucket` = `teamchat-uploads-<compte>`,
     `Storage__S3__Region` = `eu-west-1`, `Storage__S3__ForcePathStyle` = `false`
     (ne pas definir `Storage__S3__AccessKey` : le role IAM de l'instance est utilise)
   - `Admin__SeedEmail`, `Admin__SeedPassword`, `Google__ClientId` (si utilise)
6. **Role IAM de l'EC2** `teamchat-ec2-role` :
   - `AmazonSSMManagedInstanceCore`, `AmazonEC2ContainerRegistryReadOnly`
   - `ssm:GetParametersByPath` / `ssm:GetParameters` sur `parameter/teamchat/prod*` (+ `kms:Decrypt`)
   - `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject` sur `teamchat-uploads-<compte>/*`
   - `s3:GetObject` sur le bucket d'artefacts CodePipeline (lecture des revisions CodeDeploy)
   - `logs:CreateLogGroup`, `logs:CreateLogStream`, `logs:PutLogEvents`
7. **EC2** Amazon Linux 2023, `t3.micro`, 20 Go gp3, role ci-dessus, tag `App=teamchat`.
   Groupe de securite : port 80 uniquement depuis la liste de prefixes geree
   `com.amazonaws.global.cloudfront.origin-facing`. Pas de SSH : Session Manager.
   Donnees utilisateur (premier demarrage) :
   ```bash
   #!/bin/bash
   dnf install -y docker ruby wget
   systemctl enable --now docker
   fallocate -l 1G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
   echo '/swapfile swap swap defaults 0 0' >> /etc/fstab
   REGION=$(ec2-metadata --availability-zone | sed 's/.*: //;s/.$//')
   cd /tmp && wget https://aws-codedeploy-$REGION.s3.$REGION.amazonaws.com/latest/install
   chmod +x install && ./install auto && systemctl enable --now codedeploy-agent
   ```
   Associer une **IP elastique** (adresse stable pour CloudFront).
8. **CloudFront** :
   - Origine 1 : bucket frontend, avec Origin Access Control.
   - Origine 2 : nom DNS public de l'EC2, HTTP seulement, port 80.
   - Comportement `/api/*` et `/hubs/*` -> origine EC2, toutes les methodes HTTP,
     politique de cache `CachingDisabled`, politique de requete d'origine `AllViewerExceptHostHeader`.
   - Comportement par defaut -> S3, `CachingOptimized`, redirection HTTP -> HTTPS,
     CloudFront Function `spa-rewrite.js` en *viewer request*.
   - Objet racine par defaut : `index.html`.
9. **CodeDeploy** : application `teamchat` (EC2/on-premises), groupe de deploiement
   `prod` cible le tag `App=teamchat`, configuration `CodeDeployDefault.AllAtOnce`.
10. **CodeBuild** :
    - `teamchat-build` : source CodePipeline, image `aws/codebuild/standard:7.0`,
      mode privilegie, buildspec `buildspec.yml`, variables `ECR_REPOSITORY`,
      `FRONTEND_BUCKET`, `CLOUDFRONT_DISTRIBUTION_ID`. Son role doit pouvoir pousser
      sur ECR, ecrire dans le bucket frontend et appeler `cloudfront:CreateInvalidation`.
    - `teamchat-ci` : source GitHub, webhook sur les evenements de pull request,
      buildspec `aws/buildspec-ci.yml`, mode privilegie.
11. **CodePipeline** (V2) : Source GitHub (connexion CodeConnections, branche `main`)
    -> Build `teamchat-build` -> Deploy CodeDeploy `teamchat` / `prod`.

## Migrer les donnees depuis Railway (optionnel)

```bash
pg_dump --no-owner --no-acl -Fc "<url-postgres-railway>" -f teamchat.dump
pg_restore --no-owner --no-acl -d "<url-postgres-rds>" teamchat.dump
```
(a lancer depuis l'EC2 via Session Manager, RDS n'etant pas public).
Les fichiers deja envoyes sur Railway (stockage local) sont a copier dans le bucket uploads
en conservant les memes cles.

## Couts indicatifs (eu-west-1)

EC2 t3.micro ~7,5 $/mois, RDS db.t4g.micro + 20 Go ~15 $/mois, IPv4 publique ~3,6 $/mois,
S3/CloudFront/ECR/CloudWatch ~1 $/mois, CodePipeline/CodeBuild quelques centimes.
Arreter l'EC2 et la base RDS quand le projet n'est pas utilise.

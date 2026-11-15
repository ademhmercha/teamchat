# Restaurer TeamChat (RDS, fichiers S3, state Terraform)

Région `eu-north-1`. Les commandes utilisent l'AWS CLI avec ton profil SSO (`aws sso login`). Remplace `<ACCOUNT_ID>`.

> **Fenêtre de restauration RDS = `rds_backup_retention_days`.** En Free plan elle vaut **1 jour** : on ne peut revenir
> qu'à un instant des dernières 24 h. Après passage au plan payant, mets `rds_backup_retention_days = 7` dans
> `infra/envs/prod.tfvars` (puis `terraform apply`). Avant une opération risquée, prends un snapshot manuel (ci-dessous) :
> il n'expire pas.

## 1. Base RDS

### 1.0 Snapshot manuel (avant une migration ou une manip risquée)

```bash
aws rds create-db-snapshot --db-instance-identifier teamchat-prod \
  --db-snapshot-identifier teamchat-prod-avant-$(date +%Y%m%d-%H%M)
aws rds wait db-snapshot-available --db-snapshot-identifier <identifiant-ci-dessus>
```

### 1.1 Restaurer à un instant donné (point-in-time)

La restauration crée **une nouvelle instance** (la production n'est pas modifiée).

```bash
# Fenêtre disponible (RestoreWindow) : instants entre EarliestTime et LatestTime
aws rds describe-db-instance-automated-backups --db-instance-identifier teamchat-prod \
  --query 'DBInstanceAutomatedBackups[0].RestoreWindow'

# Paramètres réseau de l'instance actuelle (à réutiliser)
aws rds describe-db-instances --db-instance-identifier teamchat-prod \
  --query 'DBInstances[0].[DBSubnetGroup.DBSubnetGroupName,VpcSecurityGroups[0].VpcSecurityGroupId,DBInstanceClass]' --output text

# Restauration (heure en UTC ; ou --use-latest-restorable-time)
aws rds restore-db-instance-to-point-in-time \
  --source-db-instance-identifier teamchat-prod \
  --target-db-instance-identifier teamchat-prod-restore \
  --restore-time 2026-11-15T09:30:00Z \
  --db-subnet-group-name <subnet-group> \
  --vpc-security-group-ids <sg-rds> \
  --db-instance-class db.t4g.micro \
  --no-publicly-accessible \
  --tags Key=Project,Value=teamchat Key=Environment,Value=prod Key=AutoSchedule,Value=false

aws rds wait db-instance-available --db-instance-identifier teamchat-prod-restore
```

L'instance restaurée garde le mot de passe maître de la source. `AutoSchedule=false` empêche le scheduler de
l'arrêter la nuit pendant qu'on travaille dessus.

### 1.2 Vérifier les données (RDS n'est pas public : passer par l'EC2)

```bash
aws ssm start-session --target <instance_id>          # terraform output instance_id
sudo dnf install -y postgresql16                      # une fois
psql "host=<endpoint-restore> dbname=teamchat user=teamchat_admin sslmode=require"   # mot de passe : voir SSM
```

Le mot de passe est dans le paramètre SSM `ConnectionStrings__Default` :
`aws ssm get-parameter --name /teamchat/prod/ConnectionStrings__Default --with-decryption --query Parameter.Value --output text`.

### 1.3 Basculer l'application sur l'instance restaurée

L'API lit sa chaîne de connexion au déploiement (`start.sh` régénère `/opt/teamchat/api.env` depuis SSM).

```bash
# 1. Pointer SSM vers la nouvelle instance : remplace "Host=<ancien>" par "Host=<endpoint-restore>"
aws ssm put-parameter --name /teamchat/prod/ConnectionStrings__Default --type SecureString --overwrite \
  --value 'Host=<endpoint-restore>;Port=5432;Database=teamchat;Username=teamchat_admin;Password=<mot-de-passe>;SSL Mode=Require;Trust Server Certificate=true'

# 2. Recharger la configuration : relancer le déploiement (CodePipeline > Release change) ou, sur l'EC2 :
sudo bash /opt/teamchat/deploy/scripts/start.sh
curl -fsS http://localhost/api/health
```

> Attention : `terraform apply` réécrit ce paramètre depuis `aws_db_instance.main` (l'ancienne instance). Soit tu
> remets l'ancienne chaîne avant, soit tu fais la **bascule définitive** ci-dessous.

### 1.4 Bascule définitive (garder l'identifiant `teamchat-prod` pour Terraform)

À faire hors heures de forte activité (quelques minutes d'indisponibilité). Le nom d'hôte RDS ne change pas
(`teamchat-prod.<hash>.eu-north-1.rds.amazonaws.com`), donc SSM reste valide.

```bash
# a) Renommer l'ancienne instance (libère l'identifiant) ; la restauration part de l'instance renommée
aws rds modify-db-instance --db-instance-identifier teamchat-prod --new-db-instance-identifier teamchat-prod-old --apply-immediately
aws rds wait db-instance-available --db-instance-identifier teamchat-prod-old

# b) Restaurer sous l'identifiant d'origine (mêmes options qu'en 1.1, --target-db-instance-identifier teamchat-prod,
#    --source-db-instance-identifier teamchat-prod-old, tag AutoSchedule=true)

# c) Terraform : réassocier la nouvelle instance
cd infra
terraform state rm aws_db_instance.main
terraform import aws_db_instance.main teamchat-prod
terraform plan -var-file=envs/prod.tfvars        # ne doit montrer ni remplacement ni changement inattendu

# d) Quand tout est validé : supprimer l'ancienne (garde un snapshot final)
aws rds modify-db-instance --db-instance-identifier teamchat-prod-old --no-deletion-protection --apply-immediately
aws rds delete-db-instance --db-instance-identifier teamchat-prod-old --final-db-snapshot-identifier teamchat-prod-old-final
```

### 1.5 Restaurer depuis un snapshot (manuel ou final)

```bash
aws rds describe-db-snapshots --db-instance-identifier teamchat-prod --query 'DBSnapshots[].[DBSnapshotIdentifier,SnapshotCreateTime]' --output text
aws rds restore-db-instance-from-db-snapshot --db-instance-identifier teamchat-prod-restore \
  --db-snapshot-identifier <snapshot> --db-subnet-group-name <subnet-group> --vpc-security-group-ids <sg-rds> \
  --db-instance-class db.t4g.micro --no-publicly-accessible
```

Le snapshot final (`teamchat-prod-final`) est créé à la suppression de l'instance tant que `db_skip_final_snapshot = false`.
Il survit à `terraform destroy` ; pour recréer ensuite un `teamchat-prod-final`, supprime-le d'abord
(`aws rds delete-db-snapshot ...`) si tu ne veux pas le conserver.

## 2. Récupérer un fichier supprimé ou écrasé dans S3 (bucket `uploads`)

Le bucket `teamchat-uploads-prod-<ACCOUNT_ID>` est **versionné** : une suppression pose un « marqueur de suppression »,
un écrasement garde l'ancienne version. Les anciennes versions sont purgées **30 jours** après avoir été remplacées.

La clé S3 d'une pièce jointe est son `StorageKey` en base (nom aléatoire, jamais le nom d'origine) :

```bash
# retrouver la clé (via psql, voir 1.2) : SELECT "StorageKey", "OriginalFileName" FROM "Attachments" WHERE "OriginalFileName" ILIKE '%rapport%';

BUCKET=teamchat-uploads-prod-<ACCOUNT_ID>
KEY=<StorageKey>

# Versions et marqueurs de suppression de cette clé
aws s3api list-object-versions --bucket $BUCKET --prefix $KEY \
  --query '{Versions:Versions[].[VersionId,IsLatest,LastModified],DeleteMarkers:DeleteMarkers[].[VersionId,IsLatest]}'
```

**Fichier supprimé** (un `DeleteMarkers` avec `IsLatest = true`) : supprimer le marqueur le « ressuscite ».

```bash
aws s3api delete-object --bucket $BUCKET --key $KEY --version-id <VersionId-du-marqueur>
```

**Fichier écrasé** : recopier l'ancienne version par-dessus (elle devient la version courante).

```bash
aws s3api copy-object --bucket $BUCKET --key $KEY --copy-source "$BUCKET/$KEY?versionId=<VersionId-ancienne>"
```

## 3. State Terraform (bucket `teamchat-tfstate-<ACCOUNT_ID>`, versionné)

```bash
BUCKET=teamchat-tfstate-<ACCOUNT_ID>
aws s3api list-object-versions --bucket $BUCKET --prefix teamchat/prod/terraform.tfstate \
  --query 'Versions[].[VersionId,LastModified,IsLatest]' --output text
aws s3api copy-object --bucket $BUCKET --key teamchat/prod/terraform.tfstate \
  --copy-source "$BUCKET/teamchat/prod/terraform.tfstate?versionId=<VersionId>"
```

Les anciennes versions du state sont gardées 90 jours. Un verrou resté bloqué (`terraform.tfstate.tflock`) se supprime avec
`terraform force-unlock <ID>`.

## 4. Journal d'audit (CloudTrail)

Les appels d'API de gestion sont dans le bucket `teamchat-cloudtrail-prod-<ACCOUNT_ID>` pendant 90 jours (console CloudTrail >
*Historique des événements* pour les 90 derniers jours, sans rien configurer). Utile pour répondre à « qui a supprimé / modifié quoi ».

## 5. Test de restauration

Une sauvegarde non testée n'est pas une sauvegarde : refais 1.1 à 1.2 une fois (puis supprime l'instance de test avec
`aws rds delete-db-instance --db-instance-identifier teamchat-prod-restore --skip-final-snapshot`).

# Revue de sécurité AWS (rôles IAM et reste à durcir)

État après la phase 4. Légende : ✅ déjà restreint, ⚠️ pourrait l'être davantage, ❌ à corriger côté compte.

## 1. Rôles IAM du projet

| Rôle | Ce qu'il peut faire | État | Ce qui pourrait encore être restreint |
|---|---|---|---|
| `teamchat-prod-ec2` (instance) | Lire ses secrets SSM, tirer l'image ECR, lire/écrire le bucket uploads, envoyer des emails SES, publier les métriques de l'agent | ⚠️ | • `ecr:GetAuthorizationToken` et `cloudwatch:PutMetricData` exigent `Resource: *` (limite d'API ; la seconde est bornée au namespace `CWAgent`).<br>• `ses:SendEmail` pourrait être borné par `ses:FromAddress` (condition).<br>• `s3:ListBucket` du bucket uploads pourrait être limité à un préfixe si les clés en avaient un.<br>• `AmazonSSMManagedInstanceCore` (politique AWS) est large mais nécessaire à Session Manager et State Manager. |
| `teamchat-prod-codebuild-build` | Pousser sur le seul dépôt ECR, `sync` du seul bucket frontend, invalider la seule distribution, lire le scan ECR, rapports de tests | ✅ | • Le bucket d'artefacts est ouvert en entier : le restreindre à `teamchat-prod/*` (préfixe des artefacts du pipeline) après avoir vérifié les clés réelles d'un build.<br>• `ecr:GetAuthorizationToken` : `Resource: *` imposé. |
| `teamchat-prod-codebuild-ci` (PR) | Journaux, lire la connexion GitHub, rapports de tests | ✅ | Aucun accès ECR, S3 ni CloudFront : rien à retirer. (Les builds de PR exécutent du code non relu : ne jamais lui donner d'accès aux ressources de production.) |
| `teamchat-prod-codepipeline` | Artefacts, démarrer `teamchat-build`, créer des déploiements CodeDeploy | ✅ | Le bucket d'artefacts pourrait être borné au préfixe du pipeline (comme ci-dessus). |
| `teamchat-prod-codedeploy` | Déploiements sur les instances taguées | ⚠️ | Utilise la politique AWS `AWSCodeDeployRole` (autoscaling, ELB, SNS… inutiles ici) : la remplacer par une politique en ligne limitée à `ec2:DescribeInstances`, `ec2:DescribeInstanceStatus`, `tag:GetResources`. |
| `teamchat-prod-scheduler` (Lambda) | Décrire l'EC2/RDS, démarrer/arrêter **seulement** ce qui porte `AutoSchedule=true`, publier sur SNS | ✅ | `ec2:DescribeInstances` : `Resource: *` imposé. Les actions start/stop pourraient viser l'ARN exact de l'instance et de la base plutôt que `instance/*` + tag. |
| `teamchat-prod-scheduler-invoke` | Invoquer cette seule Lambda | ✅ | — |

## 2. Hors Terraform

- ❌ **Clés d'accès longue durée** : un fichier `*_accessKeys.csv` a circulé sur ce poste. Désactive/supprime la clé
  (IAM > Utilisateurs > Identifiants de sécurité) et travaille avec `aws configure sso` (IAM Identity Center, session courte).
- ❌ **Utilisateur qui lance Terraform** : probablement `AdministratorAccess`. Crée un jeu de permissions dédié
  (S3 du state, IAM des rôles `teamchat-*`, EC2/RDS/CloudFront/…) puis retire l'administrateur du quotidien.
- ⚠️ **Compte racine** : MFA activée, aucune clé d'accès, jamais utilisé au quotidien.
- ⚠️ **SNS** `teamchat-alerts-prod` non chiffré (CloudWatch ne sait pas publier vers un sujet chiffré avec la clé gérée `aws/sns`) ;
  acceptable : le contenu est de la supervision, pas de donnée utilisateur.
- ⚠️ **Chiffrement** S3/EBS/RDS avec clés gérées par AWS (SSE-S3, `aws/ebs`, `aws/rds`) : suffisant ici, des clés KMS gérées
  par le client ajouteraient un contrôle d'accès et un coût (≈ 1 $/mois par clé).
- ⚠️ **Connexion RDS** `Trust Server Certificate=true` : chiffrée mais le certificat n'est pas validé. Étape suivante : ajouter le
  bundle de certificats RDS à l'image Docker et passer à `SSL Mode=VerifyFull`.
- ⚠️ **Origine EC2** : le groupe de sécurité n'accepte que CloudFront, mais n'importe quelle distribution CloudFront atteint
  l'origine. Étape suivante : un en-tête secret ajouté par CloudFront (`origin custom header`) et vérifié par l'API.

## 3. Ce que la phase 4 a ajouté

CloudTrail (gestion, multi-région, 90 jours, validation d'intégrité), blocage de l'accès public S3 au niveau du compte,
chiffrement EBS par défaut, AWS Budgets, scan ECR bloquant sur CRITICAL, alarmes CloudWatch.

**IAM Access Analyzer : à activer après passage au plan payant.** Une SCP du Free plan refuse `access-analyzer:TagResource`
(explicit deny) ; la ressource existe donc mais reste désactivée (`enable_access_analyzer = false`). Après le passage au plan
payant, mettre `enable_access_analyzer = true` dans `infra/envs/prod.tfvars`, puis `terraform apply`. En attendant, l'analyse
« ressources accessibles depuis l'extérieur » se fait à la main : blocage public S3 au niveau du compte (déjà actif) et revue des
politiques de ressources (buckets, SNS).

## 4. Étapes suivantes (non faites : coût)

- **GuardDuty** (détection de menaces, ≈ quelques $ par mois après 30 jours d'essai).
- **Security Hub** (agrégation et contrôles de conformité, facturé par vérification).
- **WAF** devant CloudFront (≈ 5 $/mois + règles) : à prévoir si l'application devient publique et ciblée.

variable "aws_region" {
  type    = string
  default = "eu-north-1"
}

variable "environment" {
  description = "Nom de l'environnement (prod). Sert aux tags, au chemin SSM /teamchat/<env>/ et aux noms."
  type        = string
  default     = "prod"
}

# ---- Base de donnees ----

variable "db_name" {
  type    = string
  default = "teamchat"
}

variable "db_username" {
  type    = string
  default = "teamchat_admin"
}

variable "db_instance_class" {
  type    = string
  default = "db.t4g.micro"
}

variable "rds_backup_retention_days" {
  description = "Retention des sauvegardes automatiques RDS (jours). Free plan : 1 ; passer a 7 apres le passage au plan payant."
  type        = number
  default     = 1

  validation {
    condition     = var.rds_backup_retention_days >= 0 && var.rds_backup_retention_days <= 35
    error_message = "rds_backup_retention_days doit etre compris entre 0 et 35."
  }
}

variable "db_deletion_protection" {
  description = "Empeche la suppression de la base (passer a false avant un terraform destroy)."
  type        = bool
  default     = true
}

variable "db_skip_final_snapshot" {
  description = "true = pas de sauvegarde finale a la suppression (a eviter en prod)."
  type        = bool
  default     = false
}

# ---- Serveur ----

variable "instance_type" {
  type    = string
  default = "t3.micro"
}

# ---- CI/CD ----

variable "github_repository" {
  description = "Depot GitHub (proprietaire/nom) surveille par le pipeline."
  type        = string
  default     = "ademhmercha/teamchat"
}

variable "github_branch" {
  type    = string
  default = "main"
}

variable "codebuild_compute_type" {
  description = "Taille des builds CodeBuild : BUILD_GENERAL1_SMALL (defaut) ou BUILD_GENERAL1_MEDIUM si les tests sont trop lents."
  type        = string
  default     = "BUILD_GENERAL1_SMALL"

  validation {
    condition     = contains(["BUILD_GENERAL1_SMALL", "BUILD_GENERAL1_MEDIUM", "BUILD_GENERAL1_LARGE"], var.codebuild_compute_type)
    error_message = "Valeurs possibles : BUILD_GENERAL1_SMALL, BUILD_GENERAL1_MEDIUM, BUILD_GENERAL1_LARGE."
  }
}

variable "enable_pr_checks" {
  description = "Cree le projet teamchat-ci et son webhook GitHub (verification des pull requests). A activer APRES avoir valide la connexion GitHub dans la console."
  type        = bool
  default     = false
}

# ---- Securite et couts ----

variable "budget_name" {
  description = "Nom du budget AWS Budgets (le nom exact de ton budget existant si tu l'importes)."
  type        = string
  default     = "teamchat-monthly"
}

variable "budget_limit_usd" {
  description = "Plafond mensuel en dollars (alertes a 80 % reel et 100 % prevu, envoyees a alert_email)."
  type        = string
  default     = "20"
}

variable "import_existing_budget" {
  description = "true = importe le budget existant (budget_name) au lieu de le creer. Sans effet si alert_email est vide."
  type        = bool
  default     = false
}

variable "enable_access_analyzer" {
  description = "Cree l'analyseur IAM Access Analyzer. Desactive : une SCP du Free plan refuse access-analyzer:TagResource. A activer apres passage au plan payant."
  type        = bool
  default     = false
}

variable "ecr_scan_fail_on_critical" {
  description = "Fait echouer le build si le scan ECR trouve une vulnerabilite CRITICAL (aws/scripts/check-ecr-scan.sh)."
  type        = bool
  default     = true
}

# ---- Arret nocturne (Lambda scheduler) ----

variable "scheduler_enabled" {
  description = "false = toutes les planifications sont desactivees (l'infra reste allumee en permanence)."
  type        = bool
  default     = true
}

variable "scheduler_timezone" {
  type    = string
  default = "Africa/Lagos"
}

variable "scheduler_start_hour" {
  description = "Heure de demarrage (0-23, dans scheduler_timezone)."
  type        = number
  default     = 8

  validation {
    condition     = var.scheduler_start_hour >= 0 && var.scheduler_start_hour <= 23 && floor(var.scheduler_start_hour) == var.scheduler_start_hour
    error_message = "scheduler_start_hour doit etre un entier entre 0 et 23."
  }
}

variable "scheduler_stop_hour" {
  description = "Heure d'arret (0-23, dans scheduler_timezone)."
  type        = number
  default     = 23

  validation {
    condition     = var.scheduler_stop_hour >= 0 && var.scheduler_stop_hour <= 23 && floor(var.scheduler_stop_hour) == var.scheduler_stop_hour
    error_message = "scheduler_stop_hour doit etre un entier entre 0 et 23."
  }
}

variable "scheduler_enforce_rate" {
  description = "Frequence du controle qui re-arrete RDS s'il a redemarre seul (RDS redemarre apres 7 jours d'arret)."
  type        = string
  default     = "rate(1 hour)"
}

# ---- Ressources us-east-1 (optionnelles, voir us_east_1.tf) ----

variable "enable_us_east_1_resources" {
  description = "Cree les ressources imposees en us-east-1 (certificat ACM, alarme CloudFront). Desactive : le compte peut bloquer cette region."
  type        = bool
  default     = false
}

variable "custom_domain" {
  description = "Domaine personnalise de l'application (ex. chat.exemple.com) : cree un certificat ACM si enable_us_east_1_resources = true."
  type        = string
  default     = ""
}

variable "alert_email" {
  description = "Adresse qui recoit l'alarme CloudFront 5xx (obligatoire si enable_us_east_1_resources = true)."
  type        = string
  default     = ""
}

# ---- Emails (SES) ----

variable "ses_from_address" {
  description = "Adresse d'expedition verifiee dans SES (Email__FromAddress). Une verification par email est envoyee a la creation."
  type        = string

  validation {
    condition     = can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.ses_from_address)) && !endswith(var.ses_from_address, "example.com")
    error_message = "Renseigne une vraie adresse email (pas @example.com), par exemple via TF_VAR_ses_from_address."
  }
}

# ---- Secrets fournis par toi : jamais dans un tfvars committe ----
# Passe-les par l'environnement (TF_VAR_admin_seed_password=...) ou un fichier *.auto.tfvars
# local (ignore par git). Laisse vide pour ne pas creer le parametre SSM correspondant.

variable "admin_seed_email" {
  description = "Admin__SeedEmail : email du compte SuperAdmin cree au premier demarrage."
  type        = string
  default     = ""
}

variable "admin_seed_password" {
  description = "Admin__SeedPassword : mot de passe du compte SuperAdmin initial."
  type        = string
  default     = ""
  sensitive   = true
}

variable "google_client_id" {
  description = "Google__ClientId (connexion Google). Vide = connexion Google desactivee."
  type        = string
  default     = ""
  sensitive   = true
}

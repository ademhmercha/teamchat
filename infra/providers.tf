provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "teamchat"
      Environment = var.environment
    }
  }
}

# us-east-1 : region imposee par AWS pour le certificat ACM de CloudFront et pour les metriques CloudFront
# (alarmes). Le compte (Free plan) refuse peut-etre cette region : aucune ressource n'y est creee tant que
# var.enable_us_east_1_resources vaut false (voir us_east_1.tf). Les "skip_*" evitent tout appel d'API
# (STS, metadonnees) au demarrage de ce provider.
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"

  skip_credentials_validation = true
  skip_requesting_account_id  = true
  skip_metadata_api_check     = true

  default_tags {
    tags = {
      Project     = "teamchat"
      Environment = var.environment
    }
  }
}

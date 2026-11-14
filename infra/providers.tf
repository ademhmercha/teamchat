provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "teamchat"
      Environment = var.environment
    }
  }
}

# Certaines ressources CloudFront (certificat ACM, WAF) doivent etre en us-east-1.
# Inutilise tant que le domaine personnalise n'est pas ajoute (phase 6).
provider "aws" {
  alias  = "us_east_1"
  region = "us-east-1"

  default_tags {
    tags = {
      Project     = "teamchat"
      Environment = var.environment
    }
  }
}

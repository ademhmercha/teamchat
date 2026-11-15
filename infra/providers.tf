provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "teamchat"
      Environment = var.environment
    }
  }
}

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

# Securite et gouvernance du compte. Volontairement absents (cout) : GuardDuty, Security Hub, WAF (voir aws/README.md).

locals {
  cloudtrail_bucket_name = "teamchat-cloudtrail-${local.env}-${local.account_id}"
  cloudtrail_name        = "teamchat-${local.env}"
  # ARN construit a la main : la policy du bucket doit exister AVANT le trail (evite un cycle).
  cloudtrail_arn = "arn:aws:cloudtrail:${var.aws_region}:${local.account_id}:trail/${local.cloudtrail_name}"
}

# ---------------- CloudTrail : journal d'audit des appels d'API (gestion uniquement) ----------------

resource "aws_s3_bucket" "cloudtrail" {
  bucket = local.cloudtrail_bucket_name
}

resource "aws_s3_bucket_public_access_block" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id

  rule {
    id     = "expire-after-90-days"
    status = "Enabled"

    filter {}

    expiration {
      days = 90
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

data "aws_iam_policy_document" "cloudtrail" {
  statement {
    sid       = "AWSCloudTrailAclCheck"
    actions   = ["s3:GetBucketAcl"]
    resources = [aws_s3_bucket.cloudtrail.arn]

    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = [local.cloudtrail_arn]
    }
  }

  statement {
    sid       = "AWSCloudTrailWrite"
    actions   = ["s3:PutObject"]
    resources = ["${aws_s3_bucket.cloudtrail.arn}/AWSLogs/${local.account_id}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "s3:x-amz-acl"
      values   = ["bucket-owner-full-control"]
    }

    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = [local.cloudtrail_arn]
    }
  }

  statement {
    sid       = "DenyInsecureTransport"
    effect    = "Deny"
    actions   = ["s3:*"]
    resources = [aws_s3_bucket.cloudtrail.arn, "${aws_s3_bucket.cloudtrail.arn}/*"]

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "cloudtrail" {
  bucket     = aws_s3_bucket.cloudtrail.id
  policy     = data.aws_iam_policy_document.cloudtrail.json
  depends_on = [aws_s3_bucket_public_access_block.cloudtrail]
}

# Un seul trail multi-region, evenements de GESTION uniquement (la premiere copie est gratuite) :
# pas d'evenements de donnees S3/Lambda (payants), pas de CloudWatch Logs (cout).
resource "aws_cloudtrail" "main" {
  name                          = local.cloudtrail_name
  s3_bucket_name                = aws_s3_bucket.cloudtrail.id
  is_multi_region_trail         = true
  include_global_service_events = true # IAM, CloudFront, Budgets...
  enable_log_file_validation    = true # detecte une modification des journaux

  event_selector {
    read_write_type           = "All"
    include_management_events = true
  }

  depends_on = [aws_s3_bucket_policy.cloudtrail]
}

# ---------------- IAM Access Analyzer : ressources accessibles depuis l'exterieur du compte ----------------

# Desactive par defaut : une SCP du Free plan refuse access-analyzer:TagResource (explicit deny).
# A activer apres passage au plan payant : enable_access_analyzer = true.
resource "aws_accessanalyzer_analyzer" "main" {
  count = var.enable_access_analyzer ? 1 : 0

  analyzer_name = "teamchat-${local.env}"
  type          = "ACCOUNT"
}

# ---------------- Protections au niveau du compte ----------------

# Aucun bucket du compte ne peut devenir public (s'ajoute au blocage par bucket).
resource "aws_s3_account_public_access_block" "main" {
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Tout nouveau volume EBS de cette region est chiffre par defaut.
resource "aws_ebs_encryption_by_default" "main" {
  enabled = true
}

# ---------------- AWS Budgets ----------------
# Si un budget du meme nom existe deja (cree a la main) : budget_name = son nom exact,
# import_existing_budget = true pour l'importer au lieu de le recreer, puis aligner budget_limit_usd sur son montant.

locals {
  budget_keys = var.alert_email != "" ? toset(["monthly"]) : toset([])
}

import {
  for_each = var.import_existing_budget ? local.budget_keys : toset([])
  to       = aws_budgets_budget.monthly[each.key]
  id       = "${local.account_id}:${var.budget_name}"
}

resource "aws_budgets_budget" "monthly" {
  for_each = local.budget_keys

  name         = var.budget_name
  budget_type  = "COST"
  limit_amount = var.budget_limit_usd
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.alert_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.alert_email]
  }
}

# CI/CD : GitHub (push main) -> CodePipeline V2 -> CodeBuild (teamchat-build) -> CodeDeploy (EC2).
# Verification des pull requests : CodeBuild teamchat-ci (webhook GitHub), voir var.enable_pr_checks.

locals {
  build_project_name = "teamchat-build"
  ci_project_name    = "teamchat-ci"
  pipeline_name      = "teamchat-${local.env}"

  github_clone_url = "https://github.com/${var.github_repository}.git"
}

# ---------------- Connexion GitHub ----------------
# Creee en etat PENDING : a valider une fois a la main dans la console AWS
# (Developer Tools > Settings > Connections > Update pending connection).

resource "aws_codestarconnections_connection" "github" {
  name          = "teamchat-github"
  provider_type = "GitHub"
}

# ---------------- Bucket d'artefacts CodePipeline ----------------

resource "aws_s3_bucket" "artifacts" {
  bucket        = local.artifacts_bucket_name
  force_destroy = true # artefacts jetables, regeneres a chaque build
}

resource "aws_s3_bucket_public_access_block" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    id     = "expire-artifacts"
    status = "Enabled"

    filter {}

    expiration {
      days = 30
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

data "aws_iam_policy_document" "artifacts" {
  statement {
    sid       = "DenyInsecureTransport"
    effect    = "Deny"
    actions   = ["s3:*"]
    resources = [aws_s3_bucket.artifacts.arn, "${aws_s3_bucket.artifacts.arn}/*"]

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

resource "aws_s3_bucket_policy" "artifacts" {
  bucket     = aws_s3_bucket.artifacts.id
  policy     = data.aws_iam_policy_document.artifacts.json
  depends_on = [aws_s3_bucket_public_access_block.artifacts]
}

# ---------------- Journaux CodeBuild ----------------

resource "aws_cloudwatch_log_group" "build" {
  name              = "/aws/codebuild/${local.build_project_name}"
  retention_in_days = 14
}

resource "aws_cloudwatch_log_group" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  name              = "/aws/codebuild/${local.ci_project_name}"
  retention_in_days = 14
}

# ---------------- CodeBuild : teamchat-build (pipeline main) ----------------

data "aws_iam_policy_document" "codebuild_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["codebuild.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "build" {
  name               = "teamchat-${local.env}-codebuild-build"
  assume_role_policy = data.aws_iam_policy_document.codebuild_assume.json
}

data "aws_iam_policy_document" "build" {
  statement {
    sid       = "Logs"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.build.arn}:*"]
  }

  # Cette action n'accepte pas de ressource ciblee.
  statement {
    sid       = "EcrAuthToken"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  statement {
    sid = "EcrPushApiImage"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:CompleteLayerUpload",
      "ecr:GetDownloadUrlForLayer",
      "ecr:InitiateLayerUpload",
      "ecr:PutImage",
      "ecr:UploadLayerPart",
    ]
    resources = [aws_ecr_repository.api.arn]
  }

  statement {
    sid       = "FrontendList"
    actions   = ["s3:ListBucket", "s3:GetBucketLocation"]
    resources = [aws_s3_bucket.frontend.arn]
  }

  # s3 sync --delete : lecture, ecriture et suppression sur le seul bucket frontend.
  statement {
    sid       = "FrontendObjects"
    actions   = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
    resources = ["${aws_s3_bucket.frontend.arn}/*"]
  }

  statement {
    sid       = "InvalidateCdn"
    actions   = ["cloudfront:CreateInvalidation"]
    resources = [aws_cloudfront_distribution.main.arn]
  }

  statement {
    sid       = "PipelineArtifacts"
    actions   = ["s3:GetObject", "s3:GetObjectVersion", "s3:PutObject", "s3:GetBucketVersioning", "s3:GetBucketLocation"]
    resources = [aws_s3_bucket.artifacts.arn, "${aws_s3_bucket.artifacts.arn}/*"]
  }

  # Lecture du resultat du scan a l'envoi (aws/scripts/check-ecr-scan.sh).
  statement {
    sid       = "EcrReadScan"
    actions   = ["ecr:DescribeImageScanFindings"]
    resources = [aws_ecr_repository.api.arn]
  }

  # Rapports de tests (section "reports" du buildspec).
  statement {
    sid = "TestReports"
    actions = [
      "codebuild:BatchPutCodeCoverages",
      "codebuild:BatchPutTestCases",
      "codebuild:CreateReport",
      "codebuild:CreateReportGroup",
      "codebuild:UpdateReport",
    ]
    resources = ["arn:aws:codebuild:${var.aws_region}:${local.account_id}:report-group/${local.build_project_name}-*"]
  }
}

resource "aws_iam_role_policy" "build" {
  name   = "teamchat-${local.env}-codebuild-build"
  role   = aws_iam_role.build.id
  policy = data.aws_iam_policy_document.build.json
}

resource "aws_codebuild_project" "build" {
  name          = local.build_project_name
  description   = "Tests, image Docker de l'API vers ECR, frontend vers S3"
  service_role  = aws_iam_role.build.arn
  build_timeout = 30

  source {
    type      = "CODEPIPELINE"
    buildspec = "buildspec.yml"
  }

  artifacts {
    type = "CODEPIPELINE"
  }

  environment {
    type            = "LINUX_CONTAINER"
    image           = "aws/codebuild/standard:7.0"
    compute_type    = var.codebuild_compute_type
    privileged_mode = true # Docker (build de l'image) et Testcontainers

    environment_variable {
      name  = "ECR_REPOSITORY"
      value = aws_ecr_repository.api.name
    }

    environment_variable {
      name  = "FRONTEND_BUCKET"
      value = aws_s3_bucket.frontend.bucket
    }

    environment_variable {
      name  = "CLOUDFRONT_DISTRIBUTION_ID"
      value = aws_cloudfront_distribution.main.id
    }

    environment_variable {
      name  = "ECR_SCAN_FAIL_ON_CRITICAL"
      value = tostring(var.ecr_scan_fail_on_critical)
    }
  }

  cache {
    type  = "LOCAL"
    modes = ["LOCAL_SOURCE_CACHE", "LOCAL_DOCKER_LAYER_CACHE", "LOCAL_CUSTOM_CACHE"]
  }

  logs_config {
    cloudwatch_logs {
      group_name = aws_cloudwatch_log_group.build.name
    }
  }
}

# ---------------- CodeBuild : teamchat-ci (pull requests) ----------------
# Necessite la connexion GitHub validee : cree seulement si var.enable_pr_checks = true.

resource "aws_iam_role" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  name               = "teamchat-${local.env}-codebuild-ci"
  assume_role_policy = data.aws_iam_policy_document.codebuild_assume.json
}

data "aws_iam_policy_document" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  statement {
    sid       = "Logs"
    actions   = ["logs:CreateLogStream", "logs:PutLogEvents"]
    resources = ["${aws_cloudwatch_log_group.ci[0].arn}:*"]
  }

  # Lecture du depot GitHub via la connexion (jeton d'acces temporaire).
  statement {
    sid = "GitHubConnection"
    actions = [
      "codeconnections:GetConnection",
      "codeconnections:GetConnectionToken",
      "codestar-connections:GetConnection",
      "codestar-connections:GetConnectionToken",
    ]
    resources = [aws_codestarconnections_connection.github.arn]
  }

  statement {
    sid = "TestReports"
    actions = [
      "codebuild:BatchPutCodeCoverages",
      "codebuild:BatchPutTestCases",
      "codebuild:CreateReport",
      "codebuild:CreateReportGroup",
      "codebuild:UpdateReport",
    ]
    resources = ["arn:aws:codebuild:${var.aws_region}:${local.account_id}:report-group/${local.ci_project_name}-*"]
  }
}

resource "aws_iam_role_policy" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  name   = "teamchat-${local.env}-codebuild-ci"
  role   = aws_iam_role.ci[0].id
  policy = data.aws_iam_policy_document.ci[0].json
}

# Identifiant GitHub de CodeBuild (compte entier, une seule fois par type de serveur).
resource "aws_codebuild_source_credential" "github" {
  count = var.enable_pr_checks ? 1 : 0

  auth_type   = "CODECONNECTIONS"
  server_type = "GITHUB"
  token       = aws_codestarconnections_connection.github.arn
}

resource "aws_codebuild_project" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  name          = local.ci_project_name
  description   = "Verification des pull requests (build et tests, aucun deploiement)"
  service_role  = aws_iam_role.ci[0].arn
  build_timeout = 30

  source {
    type                = "GITHUB"
    location            = local.github_clone_url
    buildspec           = "aws/buildspec-ci.yml"
    git_clone_depth     = 1
    report_build_status = true
  }

  artifacts {
    type = "NO_ARTIFACTS"
  }

  environment {
    type            = "LINUX_CONTAINER"
    image           = "aws/codebuild/standard:7.0"
    compute_type    = var.codebuild_compute_type
    privileged_mode = true
  }

  cache {
    type  = "LOCAL"
    modes = ["LOCAL_SOURCE_CACHE", "LOCAL_DOCKER_LAYER_CACHE", "LOCAL_CUSTOM_CACHE"]
  }

  logs_config {
    cloudwatch_logs {
      group_name = aws_cloudwatch_log_group.ci[0].name
    }
  }

  depends_on = [aws_codebuild_source_credential.github]
}

resource "aws_codebuild_webhook" "ci" {
  count = var.enable_pr_checks ? 1 : 0

  project_name = aws_codebuild_project.ci[0].name
  build_type   = "BUILD"

  filter_group {
    filter {
      type    = "EVENT"
      pattern = "PULL_REQUEST_CREATED, PULL_REQUEST_UPDATED, PULL_REQUEST_REOPENED"
    }
  }
}

# ---------------- CodeDeploy ----------------

data "aws_iam_policy_document" "codedeploy_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["codedeploy.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "codedeploy" {
  name               = "teamchat-${local.env}-codedeploy"
  assume_role_policy = data.aws_iam_policy_document.codedeploy_assume.json
}

resource "aws_iam_role_policy_attachment" "codedeploy" {
  role       = aws_iam_role.codedeploy.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSCodeDeployRole"
}

resource "aws_codedeploy_app" "main" {
  name             = "teamchat"
  compute_platform = "Server"
}

resource "aws_codedeploy_deployment_group" "main" {
  app_name               = aws_codedeploy_app.main.name
  deployment_group_name  = local.env
  service_role_arn       = aws_iam_role.codedeploy.arn
  deployment_config_name = "CodeDeployDefault.AllAtOnce"

  # Cible l'instance par son tag (voir compute.tf).
  ec2_tag_filter {
    key   = "App"
    type  = "KEY_AND_VALUE"
    value = "teamchat"
  }

  auto_rollback_configuration {
    enabled = true
    events  = ["DEPLOYMENT_FAILURE"]
  }
}

# ---------------- CodePipeline V2 ----------------

data "aws_iam_policy_document" "pipeline_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["codepipeline.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "pipeline" {
  name               = "teamchat-${local.env}-codepipeline"
  assume_role_policy = data.aws_iam_policy_document.pipeline_assume.json
}

data "aws_iam_policy_document" "pipeline" {
  statement {
    sid = "Artifacts"
    actions = [
      "s3:GetObject",
      "s3:GetObjectVersion",
      "s3:PutObject",
      "s3:GetBucketVersioning",
      "s3:GetBucketLocation",
    ]
    resources = [aws_s3_bucket.artifacts.arn, "${aws_s3_bucket.artifacts.arn}/*"]
  }

  statement {
    sid = "GitHubConnection"
    actions = [
      "codeconnections:UseConnection",
      "codestar-connections:UseConnection",
    ]
    resources = [aws_codestarconnections_connection.github.arn]
  }

  statement {
    sid       = "StartBuild"
    actions   = ["codebuild:BatchGetBuilds", "codebuild:StartBuild"]
    resources = [aws_codebuild_project.build.arn]
  }

  statement {
    sid = "Deploy"
    actions = [
      "codedeploy:CreateDeployment",
      "codedeploy:GetApplication",
      "codedeploy:GetApplicationRevision",
      "codedeploy:GetDeployment",
      "codedeploy:RegisterApplicationRevision",
    ]
    resources = [
      aws_codedeploy_app.main.arn,
      aws_codedeploy_deployment_group.main.arn,
    ]
  }

  statement {
    sid       = "DeployConfig"
    actions   = ["codedeploy:GetDeploymentConfig"]
    resources = ["arn:aws:codedeploy:${var.aws_region}:${local.account_id}:deploymentconfig:CodeDeployDefault.AllAtOnce"]
  }
}

resource "aws_iam_role_policy" "pipeline" {
  name   = "teamchat-${local.env}-codepipeline"
  role   = aws_iam_role.pipeline.id
  policy = data.aws_iam_policy_document.pipeline.json
}

resource "aws_codepipeline" "main" {
  name          = local.pipeline_name
  role_arn      = aws_iam_role.pipeline.arn
  pipeline_type = "V2"

  artifact_store {
    type     = "S3"
    location = aws_s3_bucket.artifacts.bucket
  }

  # Declenche uniquement par un push sur la branche principale (aucun filtre de chemins : tout fichier compte).
  # source_action_name doit etre exactement le nom de l'action Source ci-dessous.
  # Le webhook GitHub n'est enregistre que si la connexion est deja AVAILABLE au moment ou le pipeline est
  # cree/mis a jour : apres validation de la connexion, un "terraform apply" qui modifie le pipeline le re-enregistre.
  trigger {
    provider_type = "CodeStarSourceConnection"

    git_configuration {
      source_action_name = "Source"

      push {
        branches {
          includes = [var.github_branch]
        }
      }
    }
  }

  stage {
    name = "Source"

    action {
      name             = "Source"
      category         = "Source"
      owner            = "AWS"
      provider         = "CodeStarSourceConnection"
      version          = "1"
      output_artifacts = ["source_output"]

      configuration = {
        ConnectionArn        = aws_codestarconnections_connection.github.arn
        FullRepositoryId     = var.github_repository
        BranchName           = var.github_branch
        OutputArtifactFormat = "CODE_ZIP"
        # "false" = CodePipeline ne demarre PAS le pipeline sur un nouveau commit (doc AWS, action
        # CodeStarSourceConnection) : doit rester true, le bloc trigger ci-dessus ne fait que filtrer (branche main).
        DetectChanges = "true"
      }
    }
  }

  stage {
    name = "Build"

    action {
      name             = "Build"
      category         = "Build"
      owner            = "AWS"
      provider         = "CodeBuild"
      version          = "1"
      input_artifacts  = ["source_output"]
      output_artifacts = ["build_output"]

      configuration = {
        ProjectName = aws_codebuild_project.build.name
      }
    }
  }

  stage {
    name = "Deploy"

    action {
      name            = "Deploy"
      category        = "Deploy"
      owner           = "AWS"
      provider        = "CodeDeploy"
      version         = "1"
      input_artifacts = ["build_output"]

      configuration = {
        ApplicationName     = aws_codedeploy_app.main.name
        DeploymentGroupName = aws_codedeploy_deployment_group.main.deployment_group_name
      }
    }
  }
}

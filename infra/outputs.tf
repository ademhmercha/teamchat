output "cloudfront_url" {
  description = "URL publique de l'application."
  value       = local.cloudfront_url
}

output "cloudfront_distribution_id" {
  description = "CLOUDFRONT_DISTRIBUTION_ID pour CodeBuild."
  value       = aws_cloudfront_distribution.main.id
}

output "rds_endpoint" {
  value = aws_db_instance.main.endpoint
}

output "frontend_bucket" {
  description = "FRONTEND_BUCKET pour CodeBuild."
  value       = aws_s3_bucket.frontend.bucket
}

output "uploads_bucket" {
  value = aws_s3_bucket.uploads.bucket
}

output "ecr_repository_url" {
  value = aws_ecr_repository.api.repository_url
}

output "ecr_repository_name" {
  description = "ECR_REPOSITORY pour CodeBuild."
  value       = aws_ecr_repository.api.name
}

output "instance_id" {
  description = "Connexion : aws ssm start-session --target <id>"
  value       = aws_instance.api.id
}

output "api_public_dns" {
  value = aws_eip.api.public_dns
}

output "github_connection_arn" {
  description = "A VALIDER A LA MAIN : console AWS > Developer Tools > Settings > Connections > Update pending connection. Tant qu'elle est PENDING, le pipeline echoue a l'etape Source."
  value       = aws_codestarconnections_connection.github.arn
}

output "pipeline_name" {
  value = aws_codepipeline.main.name
}

output "codedeploy_app" {
  value = aws_codedeploy_app.main.name
}

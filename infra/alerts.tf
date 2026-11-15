# Sujet SNS des alertes d'exploitation (scheduler, puis alarmes CloudWatch en phase 4.1).
# Pas de chiffrement KMS : CloudWatch ne peut pas publier vers un sujet chiffre avec la cle geree aws/sns.
resource "aws_sns_topic" "alerts" {
  name = "teamchat-alerts-${local.env}"
}

# L'abonnement email doit etre confirme en cliquant sur le lien recu.
resource "aws_sns_topic_subscription" "alerts_email" {
  count = var.alert_email != "" ? 1 : 0

  topic_arn = aws_sns_topic.alerts.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

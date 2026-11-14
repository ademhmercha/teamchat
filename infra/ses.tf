# Identite d'expedition SES (adresse email pour l'instant ; domaine en phase 6).
# A la creation, AWS envoie un email de verification a cette adresse : clique sur le lien.
# Le compte SES est en "sandbox" tant que tu n'as pas demande l'acces production
# (console SES > Account dashboard > Request production access).
resource "aws_sesv2_email_identity" "sender" {
  email_identity = var.ses_from_address
}

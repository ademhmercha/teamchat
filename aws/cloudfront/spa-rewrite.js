// CloudFront Function (evenement "viewer request") a associer UNIQUEMENT au
// comportement par defaut (S3 / frontend), pas a /api/* ni /hubs/*.
// Les routes Angular (/login, /conversations/...) renvoient index.html.
// On n'utilise pas les "pages d'erreur personnalisees" de CloudFront car elles
// s'appliqueraient aussi aux 404 de l'API.
function handler(event) {
  var request = event.request;
  var uri = request.uri;
  var lastSegment = uri.substring(uri.lastIndexOf('/') + 1);
  if (lastSegment.indexOf('.') === -1) {
    request.uri = '/index.html';
  }
  return request;
}

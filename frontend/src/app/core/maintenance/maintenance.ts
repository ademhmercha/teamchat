/**
 * Logique pure (sans Angular) de la redirection vers la page de veille /maintenance.html.
 *
 * La nuit, l'EC2 et RDS sont arretes (scheduler) : CloudFront renvoie 502/503/504 pour /api/* et /hubs/*,
 * alors que le frontend (S3) reste disponible. On redirige alors vers une page statique qui sonde
 * /api/health et revient a l'URL d'origine (?from=) des que l'API repond.
 */

export const MAINTENANCE_PATH = '/maintenance.html';

/** Statuts qui signifient "serveur indisponible". Jamais 401/403/404/500 : ce sont des reponses de l'API. */
export const MAINTENANCE_STATUSES: ReadonlySet<number> = new Set([502, 503, 504]);

/** Anti-boucle : pas de nouvelle redirection si une a deja eu lieu dans cette fenetre. */
export const REDIRECT_GUARD_KEY = 'tc_maintenance_redirect_at';
export const REDIRECT_GUARD_WINDOW_MS = 30_000;

/**
 * Vrai si l'URL de la requete cible NOTRE API (/api/*) ou la negociation SignalR (/hubs/*).
 * `apiBaseUrl` vaut '' en production (meme origine) : seuls les chemins relatifs comptent alors.
 */
export function isApiOrHubUrl(url: string, apiBaseUrl: string): boolean {
  let path: string;
  if (apiBaseUrl !== '' && url.startsWith(apiBaseUrl)) {
    path = url.slice(apiBaseUrl.length);
  } else if (url.startsWith('/') && !url.startsWith('//')) {
    path = url;
  } else {
    return false; // URL externe
  }

  const withoutQuery = path.split(/[?#]/, 1)[0];
  return withoutQuery.startsWith('/api/') || withoutQuery.startsWith('/hubs/');
}

/**
 * Vrai pour 502/503/504, et pour une erreur reseau (status 0) quand le navigateur se croit en ligne
 * (hors ligne, c'est la page /offline qui s'applique).
 */
export function isMaintenanceFailure(status: number, online: boolean): boolean {
  return MAINTENANCE_STATUSES.has(status) || (status === 0 && online);
}

/**
 * Erreur du client SignalR : echec de negociation d'un 502/503/504 ("... Status code '502'")
 * ou erreur reseau pendant la negociation ("Failed to fetch").
 */
export function isHubMaintenanceError(error: unknown, online: boolean): boolean {
  const message = error instanceof Error ? error.message : typeof error === 'string' ? error : '';
  const status = /Status code '?(\d{3})'?/i.exec(message);
  if (status) {
    return MAINTENANCE_STATUSES.has(Number(status[1]));
  }
  return online && /negotiation|Failed to fetch/i.test(message);
}

/** Pas de boucle : jamais depuis la page de veille elle-meme, ni deux fois de suite en moins de 30 s. */
export function canRedirect(pathname: string, lastRedirectAt: number | null, now: number): boolean {
  if (pathname === MAINTENANCE_PATH) {
    return false;
  }
  return lastRedirectAt === null || now - lastRedirectAt >= REDIRECT_GUARD_WINDOW_MS;
}

export function buildMaintenanceUrl(from: string): string {
  return `${MAINTENANCE_PATH}?from=${encodeURIComponent(from)}`;
}

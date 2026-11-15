
export const MAINTENANCE_PATH = '/maintenance.html';

export const MAINTENANCE_STATUSES: ReadonlySet<number> = new Set([502, 503, 504]);

export const REDIRECT_GUARD_KEY = 'tc_maintenance_redirect_at';
export const REDIRECT_GUARD_WINDOW_MS = 30_000;

export function isApiOrHubUrl(url: string, apiBaseUrl: string): boolean {
  let path: string;
  if (apiBaseUrl !== '' && url.startsWith(apiBaseUrl)) {
    path = url.slice(apiBaseUrl.length);
  } else if (url.startsWith('/') && !url.startsWith('//')) {
    path = url;
  } else {
    return false;
  }

  const withoutQuery = path.split(/[?#]/, 1)[0];
  return withoutQuery.startsWith('/api/') || withoutQuery.startsWith('/hubs/');
}

export function isMaintenanceFailure(status: number, online: boolean): boolean {
  return MAINTENANCE_STATUSES.has(status) || (status === 0 && online);
}

export function isHubMaintenanceError(error: unknown, online: boolean): boolean {
  const message = error instanceof Error ? error.message : typeof error === 'string' ? error : '';
  const status = /Status code '?(\d{3})'?/i.exec(message);
  if (status) {
    return MAINTENANCE_STATUSES.has(Number(status[1]));
  }
  return online && /negotiation|Failed to fetch/i.test(message);
}

export function canRedirect(pathname: string, lastRedirectAt: number | null, now: number): boolean {
  if (pathname === MAINTENANCE_PATH) {
    return false;
  }
  return lastRedirectAt === null || now - lastRedirectAt >= REDIRECT_GUARD_WINDOW_MS;
}

export function buildMaintenanceUrl(from: string): string {
  return `${MAINTENANCE_PATH}?from=${encodeURIComponent(from)}`;
}

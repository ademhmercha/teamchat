import {
  REDIRECT_GUARD_WINDOW_MS,
  buildMaintenanceUrl,
  canRedirect,
  isApiOrHubUrl,
  isHubMaintenanceError,
  isMaintenanceFailure,
} from './maintenance';

describe('maintenance (logique pure)', () => {
  describe('isMaintenanceFailure', () => {
    it.each([502, 503, 504])('redirige sur %i', (status) => {
      expect(isMaintenanceFailure(status, true)).toBe(true);
      expect(isMaintenanceFailure(status, false)).toBe(true);
    });

    it.each([200, 400, 401, 403, 404, 409, 429, 500])('ne redirige jamais sur %i', (status) => {
      expect(isMaintenanceFailure(status, true)).toBe(false);
    });

    it('traite une erreur reseau (0) comme une veille seulement si le navigateur est en ligne', () => {
      expect(isMaintenanceFailure(0, true)).toBe(true);
      expect(isMaintenanceFailure(0, false)).toBe(false); // hors ligne : page /offline
    });
  });

  describe('isApiOrHubUrl', () => {
    it('accepte /api/* et /hubs/* en meme origine (apiBaseUrl vide)', () => {
      expect(isApiOrHubUrl('/api/users?search=a', '')).toBe(true);
      expect(isApiOrHubUrl('/hubs/chat/negotiate?negotiateVersion=1', '')).toBe(true);
    });

    it('accepte les URL sous apiBaseUrl', () => {
      expect(isApiOrHubUrl('http://localhost:5285/api/users', 'http://localhost:5285')).toBe(true);
    });

    it('refuse le reste : autre chemin, URL externe, URL protocole-relative', () => {
      expect(isApiOrHubUrl('/assets/logo.png', '')).toBe(false);
      expect(isApiOrHubUrl('/apiary/x', '')).toBe(false);
      expect(isApiOrHubUrl('https://lh3.googleusercontent.com/api/x', '')).toBe(false);
      expect(isApiOrHubUrl('//evil.example/api/x', '')).toBe(false);
      expect(isApiOrHubUrl('https://other.example/api/x', 'http://localhost:5285')).toBe(false);
    });
  });

  describe('isHubMaintenanceError', () => {
    it.each(['502', '503', '504'])('reconnait un echec de negociation en %s', (status) => {
      const error = new Error(`Failed to complete negotiation with the server: Error: Bad Gateway: Status code '${status}'`);
      expect(isHubMaintenanceError(error, true)).toBe(true);
    });

    it('ignore les autres statuts (401, 404, 500)', () => {
      for (const status of ['401', '404', '500']) {
        const error = new Error(`Failed to complete negotiation with the server: Error: x: Status code '${status}'`);
        expect(isHubMaintenanceError(error, true)).toBe(false);
      }
    });

    it('traite un echec reseau de negociation comme une veille seulement en ligne', () => {
      const error = new Error('Failed to complete negotiation with the server: TypeError: Failed to fetch');
      expect(isHubMaintenanceError(error, true)).toBe(true);
      expect(isHubMaintenanceError(error, false)).toBe(false);
    });

    it('ignore une erreur sans rapport', () => {
      expect(isHubMaintenanceError(new Error('Invocation canceled'), true)).toBe(false);
      expect(isHubMaintenanceError(undefined, true)).toBe(false);
    });
  });

  describe('canRedirect (anti-boucle)', () => {
    it('jamais depuis la page de veille', () => {
      expect(canRedirect('/maintenance.html', null, 1_000)).toBe(false);
    });

    it('autorise la premiere redirection', () => {
      expect(canRedirect('/app/conversations', null, 1_000)).toBe(true);
    });

    it('refuse une deuxieme redirection dans la fenetre de garde, puis la reautorise', () => {
      const first = 10_000;
      expect(canRedirect('/app', first, first + REDIRECT_GUARD_WINDOW_MS - 1)).toBe(false);
      expect(canRedirect('/app', first, first + REDIRECT_GUARD_WINDOW_MS)).toBe(true);
    });
  });

  it('buildMaintenanceUrl encode l URL d origine', () => {
    expect(buildMaintenanceUrl('/app/conversations?id=1&x=2#fin')).toBe(
      '/maintenance.html?from=%2Fapp%2Fconversations%3Fid%3D1%26x%3D2%23fin',
    );
  });
});

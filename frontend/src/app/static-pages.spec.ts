import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const read = (relative: string): string => readFileSync(resolve(process.cwd(), relative), 'utf-8');
const readRepo = (relative: string): string => read(`../${relative}`);

describe('pages statiques legales', () => {
  const privacy = read('public/privacy.html');
  const terms = read('public/terms.html');

  it.each([
    ['privacy.html', privacy],
    ['terms.html', terms],
  ])('%s : document HTML francais, autonome et indexable', (_name, html) => {
    expect(html).toContain('<html lang="fr">');
    expect(html).toContain('<meta charset="utf-8" />');
    expect(html).not.toContain('noindex');
    expect(html).not.toMatch(/<script/i);
    expect(html).not.toMatch(/(?:src|href)="https?:\/\//i);
    expect(html).toContain('href="mailto:ademhmerchaaa@gmail.com"');
    expect(html).toMatch(/Dernière mise à jour : \d{1,2} \p{L}+ \d{4}/u);
  });

  it('privacy.html decrit les donnees, la finalite, l hebergement, la conservation et les droits', () => {
    for (const expected of [
      'adresse email',
      'photo',
      'messages texte',
      'fichiers',
      'messages vocaux',
      'journaux',
      'eu-north-1',
      'Stockholm',
      'ni revendues',
      'publicité',
      'Durée de conservation',
      '14 jours',
      'suppression',
      'export',
    ]) {
      expect(privacy, expected).toContain(expected);
    }
  });

  it('terms.html couvre usage de demonstration, absence de garantie, usage acceptable et contact', () => {
    for (const expected of ['projet de portfolio', 'Aucune garantie', 'Usage acceptable', 'spam', 'Contact'.toLowerCase()]) {
      expect(terms.toLowerCase(), expected).toContain(expected.toLowerCase());
    }
  });

  it('les deux pages se renvoient l une a l autre', () => {
    expect(privacy).toContain('href="/terms.html"');
    expect(terms).toContain('href="/privacy.html"');
  });
});

describe('liens dans le pied de page', () => {
  it.each(['src/app/features/landing/landing.html', 'src/app/features/login/login.html'])(
    '%s : liens classiques (pas routerLink) vers /privacy.html et /terms.html',
    (file) => {
      const html = read(file);
      expect(html).toMatch(/<a href="\/privacy\.html"[^>]*>\s*Confidentialité\s*<\/a>/);
      expect(html).toMatch(/<a href="\/terms\.html"[^>]*>\s*Conditions\s*<\/a>/);
      expect(html).not.toMatch(/routerLink="\/(?:privacy|terms)/);
    },
  );
});

describe('service par CloudFront / S3 / service worker', () => {
  const pages = ['/privacy.html', '/terms.html', '/maintenance.html'];

  it('la fonction CloudFront spa-rewrite laisse passer ces fichiers (extension) et reecrit les routes Angular', () => {
    const source = readRepo('aws/cloudfront/spa-rewrite.js');
    const handler = new Function(`${source}; return handler;`)() as (event: {
      request: { uri: string };
    }) => { uri: string };
    const rewrite = (uri: string) => handler({ request: { uri } }).uri;

    for (const page of pages) {
      expect(rewrite(page), page).toBe(page);
    }
    expect(rewrite('/app/conversations/123')).toBe('/index.html');
    expect(rewrite('/login')).toBe('/index.html');
  });

  it('ngsw-config.json exclut ces pages de navigationUrls (le service worker ne sert pas index.html)', () => {
    const config = JSON.parse(read('ngsw-config.json')) as { navigationUrls: string[] };
    for (const page of pages) {
      expect(config.navigationUrls, page).toContain(`!${page}`);
    }
    expect(config.navigationUrls).toContain('!/**/*.*');
  });

  it('buildspec.yml exclut ces pages du cache long et les publie en no-cache', () => {
    const buildspec = readRepo('buildspec.yml');
    const sync = buildspec.slice(buildspec.indexOf('aws s3 sync'), buildspec.indexOf('for f in'));
    const noCacheLoop = buildspec.slice(buildspec.indexOf('for f in'), buildspec.indexOf('aws s3 cp'));

    for (const file of ['privacy.html', 'terms.html', 'maintenance.html']) {
      expect(sync, `sync --exclude ${file}`).toContain(`--exclude "${file}"`);
      expect(noCacheLoop, `boucle no-cache ${file}`).toContain(file);
    }
  });
});

import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { REDIRECT_GUARD_KEY } from './maintenance';
import { MAINTENANCE_NAVIGATE, MaintenanceService } from './maintenance.service';

describe('MaintenanceService', () => {
  let navigate: ReturnType<typeof vi.fn>;
  let service: MaintenanceService;

  beforeEach(() => {
    sessionStorage.clear();
    history.pushState({}, '', '/app/conversations?id=1#fin');
    navigate = vi.fn();
    TestBed.configureTestingModule({ providers: [{ provide: MAINTENANCE_NAVIGATE, useValue: navigate }] });
    service = TestBed.inject(MaintenanceService);
  });

  afterEach(() => {
    sessionStorage.clear();
    history.pushState({}, '', '/');
  });

  it('redirige vers la page de veille en passant l URL courante dans ?from=', () => {
    expect(service.redirect()).toBe(true);
    expect(navigate).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith('/maintenance.html?from=%2Fapp%2Fconversations%3Fid%3D1%23fin');
  });

  it('ne redirige pas deux fois de suite (anti-boucle)', () => {
    expect(service.redirect()).toBe(true);
    expect(service.redirect()).toBe(false);
    expect(navigate).toHaveBeenCalledTimes(1);
  });

  it('redirige a nouveau une fois la fenetre de garde ecoulee', () => {
    sessionStorage.setItem(REDIRECT_GUARD_KEY, String(Date.now() - 31_000));
    expect(service.redirect()).toBe(true);
  });

  it('ne fait rien quand on est deja sur /maintenance.html', () => {
    history.pushState({}, '', '/maintenance.html?from=%2Fapp');
    expect(service.redirect()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('handleHubError : redirige sur un 503 de negociation, pas sur une erreur sans rapport', () => {
    expect(service.handleHubError(new Error("Failed to complete negotiation: Status code '404'"))).toBe(false);
    expect(navigate).not.toHaveBeenCalled();

    expect(service.handleHubError(new Error("Failed to complete negotiation: Status code '503'"))).toBe(true);
    expect(navigate).toHaveBeenCalledTimes(1);
  });
});

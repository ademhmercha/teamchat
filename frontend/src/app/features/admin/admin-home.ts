import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  CategoryScale,
  Chart,
  Filler,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
} from 'chart.js';
import { AdminStatsService } from '../../core/api/admin-stats.service';
import { AdminStats } from '../../core/models/admin.models';
import { IconComponent } from '../../shared/icon/icon';

Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Filler, Tooltip);

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink, IconComponent],
  template: `
    <div class="admin-home">
      <header class="page-header">
        <div>
          <h1>Tableau de bord</h1>
          <p>Vue d'ensemble de l'activité de votre espace.</p>
        </div>
      </header>

      @if (error(); as message) {
        <div class="alert" role="alert">{{ message }}</div>
      }

      @if (loading()) {
        <div class="state" role="status">Chargement des statistiques…</div>
      } @else if (stats(); as data) {
        <section class="metrics">
          <div class="metric">
            <span class="metric-icon"><app-icon name="users" [size]="20" /></span>
            <span class="metric-value">{{ data.activeUsers }}</span>
            <span class="metric-label">Utilisateurs actifs</span>
          </div>
          <div class="metric">
            <span class="metric-icon"><app-icon name="user" [size]="20" /></span>
            <span class="metric-value">{{ data.totalUsers }}</span>
            <span class="metric-label">Comptes au total</span>
          </div>
          <div class="metric">
            <span class="metric-icon"><app-icon name="conversations" [size]="20" /></span>
            <span class="metric-value">{{ data.activeGroups }}</span>
            <span class="metric-label">Groupes actifs</span>
          </div>
          <div class="metric">
            <span class="metric-icon"><app-icon name="send" [size]="20" /></span>
            <span class="metric-value">{{ data.messagesLast7Days }}</span>
            <span class="metric-label">Messages (7 jours)</span>
          </div>
        </section>

        <section class="card">
          <h2>Messages par jour</h2>
          <div class="chart-wrapper">
            <canvas #messageChart></canvas>
          </div>
        </section>

        <section class="card">
          <h2>Groupes les plus actifs</h2>
          @if (data.topGroups.length === 0) {
            <div class="state">Aucune activité de groupe sur la période.</div>
          } @else {
            <ul class="group-list">
              @for (group of data.topGroups; track group.conversationId) {
                <li class="group-row">
                  <span class="group-avatar">
                    <app-icon name="conversations" [size]="18" />
                  </span>
                  <span class="group-name">{{ group.name }}</span>
                  <span class="group-tag">{{ group.memberCount }} membre(s)</span>
                  <span class="group-count">{{ group.messageCount }} msg</span>
                </li>
              }
            </ul>
          }
        </section>
      }

      <section class="cards">
        <a class="card-link" routerLink="/admin/users">
          <span class="card-link-icon"><app-icon name="users" [size]="22" /></span>
          <div class="card-link-body">
            <span class="card-title">Utilisateurs</span>
            <span class="card-desc">Créer des comptes, changer les rôles, activer ou désactiver.</span>
          </div>
          <span class="card-chevron"><app-icon name="chevron-right" [size]="18" /></span>
        </a>
        <a class="card-link" routerLink="/admin/groups">
          <span class="card-link-icon"><app-icon name="conversations" [size]="22" /></span>
          <div class="card-link-body">
            <span class="card-title">Groupes</span>
            <span class="card-desc">Créer des groupes, gérer les membres et archiver.</span>
          </div>
          <span class="card-chevron"><app-icon name="chevron-right" [size]="18" /></span>
        </a>
      </section>
    </div>
  `,
  styles: `
    .admin-home {
      width: 100%;
      height: 100%;
      overflow-y: auto;
      padding: 1.75rem 2rem;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      color: var(--tc-text);
    }

    .page-header {
      h1 {
        margin: 0;
        font-size: 1.5rem;
        letter-spacing: -0.01em;
      }

      p {
        margin: 0.3rem 0 0;
        font-size: 0.87rem;
        color: var(--tc-text-muted);
      }
    }

    .metrics {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 0.9rem;
    }

    .metric {
      display: grid;
      grid-template-columns: auto 1fr;
      grid-template-rows: auto auto;
      column-gap: 0.85rem;
      row-gap: 0.1rem;
      align-items: center;
      padding: 1rem 1.1rem;
      border: 1px solid var(--tc-border);
      border-radius: var(--tc-radius-lg);
      background: var(--tc-surface);
      box-shadow: var(--tc-shadow-sm);
      transition: box-shadow var(--tc-slow);

      &:hover {
        box-shadow: var(--tc-shadow);
      }
    }

    .metric-icon {
      grid-row: 1 / span 2;
      display: grid;
      place-items: center;
      width: 42px;
      height: 42px;
      border-radius: 12px;
      background: var(--tc-gradient-soft);
      color: var(--tc-primary);
    }

    .metric-value {
      font-size: 1.6rem;
      font-weight: 700;
      line-height: 1.1;
      color: var(--tc-text);
    }

    .metric-label {
      font-size: 0.8rem;
      color: var(--tc-text-muted);
    }

    .card {
      padding: 1.1rem 1.2rem;
      border: 1px solid var(--tc-border);
      border-radius: var(--tc-radius-lg);
      background: var(--tc-surface);
      box-shadow: var(--tc-shadow-sm);

      h2 {
        margin: 0 0 0.85rem;
        font-size: 0.98rem;
        font-weight: 650;
      }
    }

    .chart-wrapper {
      height: 240px;
    }

    .group-list {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .group-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.6rem 0.4rem;
      border-radius: 10px;
      transition: background 180ms var(--tc-ease);

      &:hover {
        background: var(--tc-surface-2);
      }

      & + .group-row {
        border-top: 1px solid var(--tc-border);
      }
    }

    .group-avatar {
      display: grid;
      place-items: center;
      width: 34px;
      height: 34px;
      border-radius: 10px;
      background: var(--tc-primary-soft);
      color: var(--tc-primary);
      flex-shrink: 0;
    }

    .group-name {
      flex: 1;
      font-weight: 600;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .group-tag {
      font-size: 0.78rem;
      color: var(--tc-text-muted);
    }

    .group-count {
      font-size: 0.8rem;
      font-weight: 650;
      color: var(--tc-primary);
      background: var(--tc-primary-soft);
      padding: 0.2rem 0.6rem;
      border-radius: 999px;
      white-space: nowrap;
    }

    .state {
      padding: 1.5rem;
      text-align: center;
      color: var(--tc-text-muted);
    }

    .alert {
      padding: 0.65rem 0.8rem;
      border-radius: 10px;
      background: var(--tc-danger-soft);
      color: var(--tc-danger);
      font-size: 0.85rem;
    }

    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 0.9rem;
    }

    .card-link {
      display: flex;
      align-items: center;
      gap: 0.9rem;
      padding: 1rem 1.1rem;
      border: 1px solid var(--tc-border);
      border-radius: var(--tc-radius-lg);
      text-decoration: none;
      color: inherit;
      background: var(--tc-surface);
      box-shadow: var(--tc-shadow-sm);
      transition: border-color var(--tc-slow), box-shadow var(--tc-slow);

      &:hover {
        border-color: var(--tc-primary);
        box-shadow: var(--tc-shadow);

        .card-chevron {
          color: var(--tc-primary);
          transform: translateX(2px);
        }
      }
    }

    .card-link-icon {
      display: grid;
      place-items: center;
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: var(--tc-gradient-soft);
      color: var(--tc-primary);
      flex-shrink: 0;
    }

    .card-link-body {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      flex: 1;
      min-width: 0;
    }

    .card-title {
      font-weight: 650;
    }

    .card-desc {
      font-size: 0.83rem;
      color: var(--tc-text-muted);
    }

    .card-chevron {
      color: var(--tc-text-muted);
      transition: color 200ms var(--tc-ease), transform 200ms var(--tc-ease);
    }
  `,
})
export class AdminHomeComponent {
  private readonly statsApi = inject(AdminStatsService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly stats = signal<AdminStats | null>(null);

  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('messageChart');
  private chart: Chart | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.chart?.destroy());

    effect(() => {
      const data = this.stats();
      const canvas = this.canvasRef()?.nativeElement;
      if (data && canvas) {
        this.renderChart(canvas, data);
      }
    });

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.statsApi.getStats().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.readErrorMessage(error));
        this.loading.set(false);
      },
    });
  }

  private renderChart(canvas: HTMLCanvasElement, data: AdminStats): void {
    this.chart?.destroy();
    const styles = getComputedStyle(document.documentElement);
    const accentRgb = styles.getPropertyValue('--tc-accent-rgb').trim() || '37, 99, 235';
    const gridColor = styles.getPropertyValue('--tc-border').trim() || '#e2e8f0';
    const tickColor = styles.getPropertyValue('--tc-text-muted').trim() || '#6b7688';
    this.chart = new Chart(canvas, {
      type: 'line',
      data: {
        labels: data.messagesPerDay.map((day) => this.formatDay(day.date)),
        datasets: [
          {
            label: 'Messages',
            data: data.messagesPerDay.map((day) => day.count),
            borderColor: `rgb(${accentRgb})`,
            backgroundColor: `rgba(${accentRgb}, 0.15)`,
            fill: true,
            tension: 0.3,
            pointRadius: 3,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
        },
        scales: {
          x: {
            grid: { color: gridColor, display: false },
            ticks: { color: tickColor },
          },
          y: {
            beginAtZero: true,
            ticks: { precision: 0, color: tickColor },
            grid: { color: gridColor },
          },
        },
      },
    });
  }

  private formatDay(date: string): string {
    const parsed = new Date(date);
    if (Number.isNaN(parsed.getTime())) {
      return date;
    }
    return parsed.toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit' });
  }

  private readErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Serveur injoignable.';
    }

    const problem = error.error as { detail?: string; title?: string } | null;
    return problem?.detail ?? problem?.title ?? 'Erreur.';
  }
}

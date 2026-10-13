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

Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Filler, Tooltip);

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink],
  template: `
    <div class="admin-home">
      <h1>Tableau de bord</h1>

      @if (error(); as message) {
        <div class="alert" role="alert">{{ message }}</div>
      }

      @if (loading()) {
        <div class="state">Chargement des statistiques…</div>
      } @else if (stats(); as data) {
        <section class="metrics">
          <div class="metric">
            <span class="metric-value">{{ data.activeUsers }}</span>
            <span class="metric-label">Utilisateurs actifs</span>
          </div>
          <div class="metric">
            <span class="metric-value">{{ data.totalUsers }}</span>
            <span class="metric-label">Comptes au total</span>
          </div>
          <div class="metric">
            <span class="metric-value">{{ data.activeGroups }}</span>
            <span class="metric-label">Groupes actifs</span>
          </div>
          <div class="metric">
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
          <span class="card-title">Utilisateurs</span>
          <span class="card-desc">Créer des comptes, changer les rôles, activer ou désactiver.</span>
        </a>
        <a class="card-link" routerLink="/admin/groups">
          <span class="card-title">Groupes</span>
          <span class="card-desc">Créer des groupes, gérer les membres et archiver.</span>
        </a>
      </section>
    </div>
  `,
  styles: `
    .admin-home {
      max-width: 880px;
      margin: 0 auto;
      display: flex;
      flex-direction: column;
      gap: 1.25rem;
      color: #2c2f42;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .metrics {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 0.9rem;
    }

    .metric {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      padding: 1rem 1.1rem;
      border: 1px solid #e6e7f1;
      border-radius: 12px;
      background: #fff;
    }

    .metric-value {
      font-size: 1.7rem;
      font-weight: 700;
      color: #5b4bd6;
    }

    .metric-label {
      font-size: 0.82rem;
      color: #7a8095;
    }

    .card {
      padding: 1rem 1.1rem;
      border: 1px solid #e6e7f1;
      border-radius: 14px;
      background: #fff;

      h2 {
        margin: 0 0 0.75rem;
        font-size: 1rem;
      }
    }

    .chart-wrapper {
      height: 240px;
    }

    .group-list {
      list-style: none;
      margin: 0;
      padding: 0;

      li + li {
        border-top: 1px solid #f0f1f7;
      }
    }

    .group-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.55rem 0;
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
      color: #7a8095;
    }

    .group-count {
      font-size: 0.82rem;
      font-weight: 600;
      color: #5b4bd6;
    }

    .state {
      padding: 1.5rem;
      text-align: center;
      color: #7a8095;
    }

    .alert {
      padding: 0.6rem 0.75rem;
      border-radius: 9px;
      background: #fdecea;
      color: #b3261e;
      font-size: 0.85rem;
    }

    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 0.9rem;
    }

    .card-link {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      padding: 1rem 1.1rem;
      border: 1px solid #e6e7f1;
      border-radius: 12px;
      text-decoration: none;
      color: inherit;
      background: #fff;

      &:hover {
        border-color: #c9c2f2;
        background: #f8f7ff;
      }
    }

    .card-title {
      font-weight: 600;
    }

    .card-desc {
      font-size: 0.85rem;
      color: #7a8095;
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
    this.chart = new Chart(canvas, {
      type: 'line',
      data: {
        labels: data.messagesPerDay.map((day) => this.formatDay(day.date)),
        datasets: [
          {
            label: 'Messages',
            data: data.messagesPerDay.map((day) => day.count),
            borderColor: '#5b4bd6',
            backgroundColor: 'rgba(91, 75, 214, 0.15)',
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
          y: { beginAtZero: true, ticks: { precision: 0 } },
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

import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-admin-home',
  imports: [RouterLink],
  template: `
    <div class="admin-home">
      <h1>Tableau de bord</h1>
      <p>
        Espace reserve aux administrateurs. Gerez les groupes de discussion ci-dessous ;
        les statistiques et la gestion des utilisateurs arriveront dans les prochains sprints.
      </p>
      <a class="card-link" routerLink="/admin/groups">
        <span class="card-title">Groupes</span>
        <span class="card-desc">Creer des groupes, gerer les membres et archiver.</span>
      </a>
    </div>
  `,
  styles: `
    .admin-home {
      max-width: 880px;
      margin: 0 auto;
      color: #2c2f42;
    }

    h1 {
      margin: 0 0 0.5rem;
      font-size: 1.5rem;
    }

    p {
      color: #7a8095;
    }

    .card-link {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-top: 1rem;
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
export class AdminHomeComponent {}

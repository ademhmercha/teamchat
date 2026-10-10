import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-home',
  template: `
    <div class="admin-home">
      <h1>Tableau de bord</h1>
      <p>
        Espace reserve aux administrateurs. Les statistiques et la gestion des utilisateurs
        arriveront dans les prochains sprints.
      </p>
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
  `,
})
export class AdminHomeComponent {}

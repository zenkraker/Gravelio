import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './core/auth';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <mat-toolbar>
      <a class="brand" routerLink="/" i18n>Gravelio</a>
      <nav aria-label="Main" i18n-aria-label>
        <a
          mat-button
          routerLink="/activities"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >Activities</a
        >
        <a mat-button routerLink="/gear" routerLinkActive="active" ariaCurrentWhenActive="page" i18n
          >Gear</a
        >
        <a
          mat-button
          routerLink="/weight"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >Weight</a
        >
        <a
          mat-button
          routerLink="/steps"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >Steps</a
        >
        <a
          mat-button
          routerLink="/people"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >People</a
        >
        @if (auth.isAdmin()) {
          <a
            mat-button
            routerLink="/users"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            i18n
            >Users</a
          >
        }
      </nav>
      <span class="spacer"></span>
      <a mat-button routerLink="/profile" routerLinkActive="active" ariaCurrentWhenActive="page">
        <mat-icon aria-hidden="true">person</mat-icon>{{ auth.user()?.name }}
      </a>
      <button mat-button type="button" (click)="auth.logout()" i18n>Sign out</button>
    </mat-toolbar>
    <main><router-outlet /></main>
  `,
  styles: `
    .brand {
      font: var(--mat-sys-title-large);
      color: var(--mat-sys-primary);
      text-decoration: none;
      margin-right: 16px;
    }
    mat-toolbar {
      background: var(--mat-sys-surface-container);
      color: var(--mat-sys-on-surface);
    }
    .spacer {
      flex: 1;
    }
    .active {
      background: var(--mat-sys-secondary-container);
      border-radius: var(--mat-sys-corner-full);
    }
    main {
      max-width: 1100px;
      margin: 0 auto;
      padding: 16px;
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
}

import { Avatar } from './components/avatar';
import { Component, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthService } from './core/auth';
import { Notifications } from './core/notifications';

@Component({
  selector: 'app-shell',
  imports: [
    Avatar,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatBadgeModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
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
          routerLink="/stats"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >Statistics</a
        >
        <a
          mat-button
          routerLink="/goals"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >Goals</a
        >
        <button mat-button type="button" [matMenuTriggerFor]="healthMenu" i18n>
          Health<mat-icon aria-hidden="true">expand_more</mat-icon>
        </button>
        <mat-menu #healthMenu="matMenu">
          <a mat-menu-item routerLink="/weight" i18n>Weight</a>
          <a mat-menu-item routerLink="/steps" i18n>Steps</a>
          <a mat-menu-item routerLink="/sleep" i18n>Sleep</a>
          <a mat-menu-item routerLink="/water" i18n>Water</a>
          <a mat-menu-item routerLink="/targets" i18n>Targets</a>
          <a mat-menu-item routerLink="/fasting" i18n>Fasting</a>
          <a mat-menu-item routerLink="/poop" i18n>Bowel movements</a>
        </mat-menu>
        <a
          mat-button
          routerLink="/people"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
          i18n
          >People</a
        >
        <a
          mat-button
          routerLink="/notifications"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
        >
          <span class="cdk-visually-hidden" i18n>Notifications</span>
          <mat-icon
            aria-hidden="true"
            [matBadge]="notifications.count() || null"
            matBadgeColor="warn"
            matBadgeSize="small"
            >notifications</mat-icon
          >
        </a>
        @if (auth.isAdmin()) {
          <a
            mat-button
            routerLink="/users"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            i18n
            >Users</a
          >
          <a
            mat-button
            routerLink="/sso-providers"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            i18n
            >Sign-in providers</a
          >
          <a
            mat-button
            routerLink="/server-settings"
            routerLinkActive="active"
            ariaCurrentWhenActive="page"
            i18n
            >Server settings</a
          >
        }
      </nav>
      <span class="spacer"></span>
      <a mat-button routerLink="/profile" routerLinkActive="active" ariaCurrentWhenActive="page">
        @if (auth.user(); as me) {
          <app-avatar [userId]="me.id" [name]="me.name" [hasPhoto]="me.has_photo" [size]="24" />
          {{ me.name }}
        }
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
      /* Admins have more links than fit on a narrow screen: let the bar grow a row instead of overlapping. */
      height: auto;
      min-height: 64px;
      flex-wrap: wrap;
      padding-block: 4px;
    }
    nav {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
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
  protected readonly notifications = inject(Notifications);

  constructor() {
    // New notifications arrive while the user is elsewhere, so refresh the badge after each navigation.
    inject(Router)
      .events.pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.notifications.refresh());
  }
}

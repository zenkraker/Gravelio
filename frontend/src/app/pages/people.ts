import { Avatar } from '../components/avatar';
import { HttpClient, httpResource } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Follow, Paged, Person, problemMessage } from '../core/api';
import { OUTLINE_FIELDS } from '../core/outline-fields';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-people',
  providers: [OUTLINE_FIELDS],
  imports: [
    Avatar,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatTableModule,
  ],
  template: `
    <h1 i18n>People</h1>

    <mat-card appearance="outlined">
      <mat-card-content>
        <h2 i18n>Follow requests</h2>
        @if (requests.error()) {
          <p class="error" role="alert" i18n>Could not load your follow requests.</p>
        }
        <table
          mat-table
          [dataSource]="requests.value()?.items ?? []"
          aria-label="Follow requests"
          i18n-aria-label
        >
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef i18n>Name</th>
            <td mat-cell *matCellDef="let r">
              <a [routerLink]="['/people', r.follower.id]">{{ r.follower.name }}</a>
            </td>
          </ng-container>
          <ng-container matColumnDef="username">
            <th mat-header-cell *matHeaderCellDef i18n>Username</th>
            <td mat-cell *matCellDef="let r">{{ r.follower.username }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef>
              <span class="cdk-visually-hidden" i18n>Actions</span>
            </th>
            <td mat-cell *matCellDef="let r">
              <button
                mat-flat-button
                type="button"
                (click)="accept(r)"
                [attr.aria-label]="acceptLabel(r)"
                i18n
              >
                Accept
              </button>
              <button
                mat-button
                type="button"
                (click)="reject(r)"
                [attr.aria-label]="rejectLabel(r)"
                i18n
              >
                Reject
              </button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="requestColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: requestColumns"></tr>
          @if (!requests.isLoading() && !(requests.value()?.total ?? 0)) {
            <tr class="mat-mdc-row" *matNoDataRow>
              <td class="mat-mdc-cell" [attr.colspan]="requestColumns.length" i18n>
                No pending requests.
              </td>
            </tr>
          }
        </table>
      </mat-card-content>
    </mat-card>

    <h2 i18n>Find people</h2>
    <mat-form-field>
      <mat-label i18n>Search by name or username</mat-label>
      <input matInput type="search" #search [value]="query()" (input)="setQuery(search.value)" />
    </mat-form-field>
    @if (people.error()) {
      <p class="error" role="alert" i18n>Could not load people.</p>
    }
    <table mat-table [dataSource]="people.value()?.items ?? []" aria-label="People" i18n-aria-label>
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef i18n>Name</th>
        <td mat-cell *matCellDef="let p">
          <app-avatar [userId]="p.id" [name]="p.name" [hasPhoto]="p.has_photo" [size]="24" />
          <a [routerLink]="['/people', p.id]">{{ p.name }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="username">
        <th mat-header-cell *matHeaderCellDef i18n>Username</th>
        <td mat-cell *matCellDef="let p">{{ p.username }}</td>
      </ng-container>
      <ng-container matColumnDef="city">
        <th mat-header-cell *matHeaderCellDef i18n>City</th>
        <td mat-cell *matCellDef="let p">{{ p.city ?? '' }}</td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns"></tr>
    </table>
    <mat-paginator
      [length]="people.value()?.total ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex() - 1"
      [hidePageSize]="true"
      (page)="onPage($event)"
    />
  `,
  styles: `
    mat-card {
      margin-bottom: 24px;
    }
    table {
      width: 100%;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class People {
  private http = inject(HttpClient);
  private snack = inject(MatSnackBar);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageIndex = signal(1);
  protected readonly query = signal('');
  protected readonly columns = ['name', 'username', 'city'];
  protected readonly requestColumns = ['name', 'username', 'actions'];

  protected readonly people = httpResource<Paged<Person>>(() => ({
    url: '/api/v1/people',
    params: {
      page: this.pageIndex(),
      page_size: PAGE_SIZE,
      ...(this.query().trim() ? { q: this.query().trim() } : {}),
    },
  }));
  protected readonly requests = httpResource<Paged<Follow>>(() => ({
    url: '/api/v1/follow-requests',
    params: { page_size: 100 },
  }));

  protected setQuery(value: string): void {
    this.query.set(value);
    this.pageIndex.set(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex + 1);
  }

  protected acceptLabel(f: Follow): string {
    return $localize`Accept request from ${f.follower.name}`;
  }

  protected rejectLabel(f: Follow): string {
    return $localize`Reject request from ${f.follower.name}`;
  }

  protected async accept(f: Follow): Promise<void> {
    try {
      await firstValueFrom(this.http.post(`/api/v1/follow-requests/${f.follower_id}/accept`, null));
      this.requests.reload();
      this.snack.open($localize`Request accepted`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }

  protected async reject(f: Follow): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(`/api/v1/follow-requests/${f.follower_id}`));
      this.requests.reload();
      this.snack.open($localize`Request rejected`, undefined, { duration: 4000 });
    } catch (err) {
      this.snack.open(problemMessage(err), $localize`Dismiss`, { duration: 8000 });
    }
  }
}

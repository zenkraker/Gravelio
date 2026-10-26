import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { problemMessage } from '../core/api';
import { AuthService } from '../core/auth';
import { Avatar } from './avatar';

const SIDE = 256;

/** Change or remove the profile picture. The picture is cropped to a square and re-encoded here, which also drops its metadata. */
@Component({
  selector: 'app-profile-photo',
  imports: [Avatar, MatButtonModule],
  template: `
    @if (auth.user(); as me) {
      <section aria-labelledby="photo-title">
        <h2 id="photo-title" i18n>Profile picture</h2>
        <app-avatar
          [userId]="me.id"
          [name]="me.name"
          [hasPhoto]="me.has_photo"
          [size]="96"
          [version]="version()"
        />
        <div class="actions">
          <input
            #file
            class="cdk-visually-hidden"
            type="file"
            accept="image/*"
            aria-label="Picture file"
            i18n-aria-label
            tabindex="-1"
            (change)="choose(file)"
          />
          <button mat-flat-button type="button" [disabled]="busy()" (click)="file.click()" i18n>
            Change picture
          </button>
          @if (me.has_photo) {
            <button mat-button type="button" [disabled]="busy()" (click)="remove()" i18n>
              Remove picture
            </button>
          }
        </div>
        @if (error()) {
          <p class="error" role="alert">{{ error() }}</p>
        }
      </section>
    }
  `,
  styles: `
    .actions {
      display: flex;
      gap: 8px;
      margin-top: 12px;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class ProfilePhoto {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly snack = inject(MatSnackBar);
  private readonly document = inject(DOCUMENT);

  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly version = signal(0);

  protected async choose(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    await this.run(async () => {
      const body = new FormData();
      body.append('file', await this.squareJpeg(file), 'me.jpg');
      await firstValueFrom(this.http.put('/api/v1/users/me/photo', body));
      this.snack.open($localize`Picture saved`, undefined, { duration: 4000 });
    });
  }

  protected async remove(): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http.delete('/api/v1/users/me/photo'));
      this.snack.open($localize`Picture removed`, undefined, { duration: 4000 });
    });
  }

  private async run(work: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await work();
      await this.auth.loadUser();
      this.version.update((v) => v + 1);
    } catch (err) {
      this.error.set(err instanceof Error && !('error' in err) ? err.message : problemMessage(err));
    } finally {
      this.busy.set(false);
    }
  }

  private async squareJpeg(file: File): Promise<Blob> {
    let bitmap: ImageBitmap;
    try {
      bitmap = await createImageBitmap(file);
    } catch {
      throw new Error($localize`That file is not a picture this browser can open.`);
    }
    const side = Math.min(bitmap.width, bitmap.height);
    const canvas = this.document.createElement('canvas');
    canvas.width = canvas.height = SIDE;
    canvas
      .getContext('2d')!
      .drawImage(
        bitmap,
        (bitmap.width - side) / 2,
        (bitmap.height - side) / 2,
        side,
        side,
        0,
        0,
        SIDE,
        SIDE,
      );
    bitmap.close();
    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob(
        (blob) =>
          blob ? resolve(blob) : reject(new Error($localize`Could not prepare the picture.`)),
        'image/jpeg',
        0.85,
      ),
    );
  }
}

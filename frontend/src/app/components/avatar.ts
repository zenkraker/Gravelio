import { NgOptimizedImage } from '@angular/common';
import { Component, computed, input } from '@angular/core';

/** A person's picture, or their initial when they have none. Always decorative: the name is shown next to it. */
@Component({
  selector: 'app-avatar',
  imports: [NgOptimizedImage],
  template: `
    @if (hasPhoto()) {
      <img [ngSrc]="src()" [width]="size()" [height]="size()" alt="" />
    } @else {
      <span
        aria-hidden="true"
        [style.width.px]="size()"
        [style.height.px]="size()"
        [style.font-size.px]="size() / 2"
        >{{ initial() }}</span
      >
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      vertical-align: middle;
    }
    img,
    span {
      border-radius: var(--mat-sys-corner-full);
      object-fit: cover;
    }
    span {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
  `,
})
export class Avatar {
  readonly userId = input.required<number>();
  readonly name = input.required<string>();
  readonly hasPhoto = input(false);
  readonly size = input(32);
  /** Change it after a new picture is uploaded, so the browser asks for the new one. */
  readonly version = input(0);

  protected readonly src = computed(
    () => `/api/v1/users/${this.userId()}/photo?v=${this.version()}`,
  );
  protected readonly initial = computed(() => this.name().trim().charAt(0).toUpperCase());
}

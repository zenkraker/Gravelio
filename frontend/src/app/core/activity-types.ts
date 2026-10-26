import { httpResource } from '@angular/common/http';
import { Service, computed } from '@angular/core';

@Service()
export class ActivityTypes {
  private readonly types = httpResource<Record<string, string>>(() => '/api/v1/activities/types');

  /** Every type, by name, for a picker. */
  readonly options = computed(() =>
    Object.entries(this.types.value() ?? {})
      .map(([id, name]) => ({ id: Number(id), name }))
      .sort((a, b) => a.name.localeCompare(b.name)),
  );

  name(id: number): string {
    return this.types.value()?.[id] ?? '';
  }
}

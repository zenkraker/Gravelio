import { httpResource } from '@angular/common/http';
import { Service } from '@angular/core';

@Service()
export class ActivityTypes {
  private readonly types = httpResource<Record<string, string>>(() => '/api/v1/activities/types');

  name(id: number): string {
    return this.types.value()?.[id] ?? '';
  }
}

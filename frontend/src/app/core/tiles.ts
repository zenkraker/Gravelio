import { httpResource } from '@angular/common/http';
import { Service, computed } from '@angular/core';
import { PublicSettings } from './api';

/** Where map tiles come from: the server's setting, which defaults to the public OpenStreetMap server. */
@Service()
export class MapTiles {
  private readonly settings = httpResource<PublicSettings>(() => '/api/v1/server-settings/public');

  /** Null until the setting has loaded, so a map is not drawn from the wrong server first. */
  readonly config = computed(() => {
    const s = this.settings.value();
    return s ? { url: s.tile_url, attribution: s.tile_attribution } : null;
  });
}

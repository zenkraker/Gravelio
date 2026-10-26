import { Component, ElementRef, effect, inject, input, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { MapTiles } from '../core/tiles';
import { themeColor } from '../core/theme';

@Component({
  selector: 'app-route-map',
  template: '<div #map role="region" aria-label="Route map" i18n-aria-label></div>',
  styles: ':host { display: block } div { height: 360px; border-radius: 12px; }',
})
export class RouteMap {
  readonly points = input.required<[number, number][]>();
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('map');
  private readonly tiles = inject(MapTiles);

  constructor() {
    effect((onCleanup) => {
      const config = this.tiles.config();
      if (!config) return;
      const map = L.map(this.host().nativeElement);
      L.tileLayer(config.url, { maxZoom: 19, attribution: config.attribution }).addTo(map);
      const line = L.polyline(this.points(), {
        color: themeColor(this.host().nativeElement, 'primary'),
        weight: 4,
      }).addTo(map);
      map.fitBounds(line.getBounds(), { padding: [16, 16] });
      onCleanup(() => map.remove());
    });
  }
}

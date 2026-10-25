import { Component, ElementRef, effect, input, viewChild } from '@angular/core';
import * as L from 'leaflet';
import { themeColor } from '../core/theme';

// shortcut: public OpenStreetMap tiles, so every view tells OSM the viewed area; make the tile server configurable when self-hosters ask.
const TILES = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';

@Component({
  selector: 'app-route-map',
  template: '<div #map role="region" aria-label="Route map" i18n-aria-label></div>',
  styles: ':host { display: block } div { height: 360px; border-radius: 12px; }',
})
export class RouteMap {
  readonly points = input.required<[number, number][]>();
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('map');

  constructor() {
    effect((onCleanup) => {
      const map = L.map(this.host().nativeElement);
      L.tileLayer(TILES, { maxZoom: 19, attribution: '© OpenStreetMap contributors' }).addTo(map);
      const line = L.polyline(this.points(), {
        color: themeColor(this.host().nativeElement, 'primary'),
        weight: 4,
      }).addTo(map);
      map.fitBounds(line.getBounds(), { padding: [16, 16] });
      onCleanup(() => map.remove());
    });
  }
}

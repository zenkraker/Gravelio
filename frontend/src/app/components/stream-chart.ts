import { Component, ElementRef, effect, input, viewChild } from '@angular/core';
import { Chart } from 'chart.js/auto';
import { formatDuration } from '../core/format';

export interface ChartPoint {
  x: number; // seconds since the start
  y: number;
}

@Component({
  selector: 'app-stream-chart',
  template: '<canvas #canvas role="img" [attr.aria-label]="label()"></canvas>',
  styles: ':host { display: block; height: 220px }',
})
export class StreamChart {
  readonly label = input.required<string>();
  readonly points = input.required<ChartPoint[]>();
  /** Formats y ticks; plain numbers by default. */
  readonly format = input<(value: number) => string>((v) => String(Math.round(v * 10) / 10));
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  constructor() {
    effect((onCleanup) => {
      const format = this.format();
      const chart = new Chart(this.canvas().nativeElement, {
        type: 'line',
        data: {
          datasets: [
            {
              label: this.label(),
              data: this.points(),
              pointRadius: 0,
              borderWidth: 2,
              borderColor: '#0b57d0',
            },
          ],
        },
        options: {
          parsing: false,
          animation: false,
          maintainAspectRatio: false,
          plugins: { legend: { display: false }, title: { display: true, text: this.label() } },
          scales: {
            x: { type: 'linear', ticks: { callback: (v) => formatDuration(Number(v)) } },
            y: { ticks: { precision: 0, callback: (v) => format(Number(v)) } },
          },
        },
      });
      onCleanup(() => chart.destroy());
    });
  }
}

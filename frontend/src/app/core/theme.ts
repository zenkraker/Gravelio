/**
 * Resolves a Material system colour token (e.g. 'primary') to a concrete rgb() string for
 * canvas and Leaflet, which cannot read light-dark() CSS variables.
 * shortcut: read once when drawn, so a live OS theme switch shows after the next redraw.
 */
export function themeColor(host: HTMLElement, token: string): string {
  const probe = document.createElement('span');
  probe.style.color = `var(--mat-sys-${token})`;
  host.append(probe);
  const color = getComputedStyle(probe).color;
  probe.remove();
  return color;
}

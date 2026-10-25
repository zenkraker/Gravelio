import { GEAR_TYPES, componentLabel, formatMoney, gearTypeLabel } from './gear';

describe('gear', () => {
  it('labels every gear type and falls back to the id for an unknown one', () => {
    for (const t of GEAR_TYPES) expect(gearTypeLabel(t.id)).toBe(t.label);
    expect(gearTypeLabel(99)).toBe('99');
  });

  it('labels known component types and shows an unknown one as sent', () => {
    expect(componentLabel('wing')).toBe('Wing');
    expect(componentLabel('not_a_component')).toBe('not_a_component');
  });

  it('formats money in the user currency and shows a dash for none', () => {
    expect(formatMoney(null, 'euro')).toBe('—');
    // The browser locale decides the decimal separator and symbol placement, so assert both halves separately.
    expect(formatMoney(12.5, 'dollar')).toMatch(/12[.,]50/);
    expect(formatMoney(12.5, 'dollar')).toMatch(/(USD|\$)/);
    expect(formatMoney(12.5, 'pound')).toContain('£');
  });
});

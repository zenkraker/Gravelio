import {
  displayToKg,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatElevation,
  formatPace,
  formatSpeed,
  kgToDisplay,
} from './format';

describe('format', () => {
  it('formats distance in both unit systems', () => {
    expect(formatDistance(3618, 'metric')).toBe('3.62 km');
    expect(formatDistance(1609.344, 'imperial')).toBe('1.00 mi');
  });

  it('formats durations with and without hours', () => {
    expect(formatDuration(59)).toBe('0:59');
    expect(formatDuration(3725)).toBe('1:02:05');
  });

  it('converts stored seconds-per-metre to pace per km or mile', () => {
    expect(formatPace(0.3, 'metric')).toBe('5:00 /km');
    expect(formatPace(0.3, 'imperial')).toBe('8:03 /mi');
    expect(formatPace(0, 'metric')).toBe('—');
  });

  it('converts stored metres-per-second to speed', () => {
    expect(formatSpeed(10, 'metric')).toBe('36.0 km/h');
    expect(formatSpeed(null, 'metric')).toBe('—');
  });

  it('formats elevation, treating missing data differently from zero', () => {
    expect(formatElevation(0, 'metric')).toBe('0 m');
    expect(formatElevation(100, 'imperial')).toBe('328 ft');
    expect(formatElevation(null, 'metric')).toBe('—');
  });

  it('shows instants in the activity timezone and a dash when hidden', () => {
    const lisbonSummer = formatDateTime('2026-07-01T12:00:00Z', 'Europe/Lisbon');
    const utc = formatDateTime('2026-07-01T12:00:00Z', null);
    expect(lisbonSummer).not.toBe(utc);
    expect(formatDateTime(null, null)).toBe('—');
  });

  it('converts body weight both ways', () => {
    expect(kgToDisplay(80, 'metric')).toBe(80);
    expect(kgToDisplay(80, 'imperial')).toBe(176.4);
    expect(displayToKg(176.4, 'imperial')).toBeCloseTo(80, 1);
  });
});

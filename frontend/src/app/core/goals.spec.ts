import { displayToTarget, formatGoalAmount, targetToDisplay } from './goals';

describe('goals', () => {
  it('converts a typed target to the stored unit and back', () => {
    expect(displayToTarget('distance', 10, 'metric')).toBe(10_000);
    expect(targetToDisplay('distance', 10_000, 'metric')).toBe(10);
    expect(displayToTarget('duration', 90, 'metric')).toBe(5_400);
    expect(targetToDisplay('duration', 5_400, 'metric')).toBe(90);
    expect(displayToTarget('elevation', 1000, 'imperial')).toBe(305);
    expect(displayToTarget('activities', 3, 'metric')).toBe(3);
  });

  it('never stores a target below one', () => {
    expect(displayToTarget('distance', 0, 'metric')).toBe(1);
  });

  it('shows amounts in the user units', () => {
    expect(formatGoalAmount('distance', 12_500, 'metric')).toBe('12.5 km');
    expect(formatGoalAmount('duration', 3_725, 'metric')).toBe('1:02:05');
    expect(formatGoalAmount('calories', 850.4, 'metric')).toBe('850 kcal');
    expect(formatGoalAmount('activities', 3, 'metric')).toBe('3');
  });
});

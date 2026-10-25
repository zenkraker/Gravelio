import { User } from './api';

export const GEAR_TYPES = [
  { id: 1, label: $localize`Bike` },
  { id: 2, label: $localize`Shoes` },
  { id: 3, label: $localize`Wetsuit` },
  { id: 4, label: $localize`Racquet` },
  { id: 5, label: $localize`Ski` },
  { id: 6, label: $localize`Snowboard` },
  { id: 7, label: $localize`Windsurf` },
  { id: 8, label: $localize`Water sports board` },
] as const;

export function gearTypeLabel(id: number): string {
  return GEAR_TYPES.find((t) => t.id === id)?.label ?? String(id);
}

/** Display names for the component types the API allows; an id with no entry falls back to its raw id. */
const COMPONENT_LABELS: Record<string, string> = {
  back_break_oil: $localize`Rear brake oil`,
  back_break_pads: $localize`Rear brake pads`,
  back_break_rotor: $localize`Rear brake rotor`,
  back_tire: $localize`Rear tire`,
  back_tube: $localize`Rear tube`,
  back_tubeless_rim_tape: $localize`Rear tubeless rim tape`,
  back_tubeless_sealant: $localize`Rear tubeless sealant`,
  back_wheel: $localize`Rear wheel`,
  back_wheel_valve: $localize`Rear wheel valve`,
  basegrip: $localize`Basegrip`,
  board: $localize`Board`,
  boom: $localize`Boom`,
  bottle_cage: $localize`Bottle cage`,
  bottom_bracket: $localize`Bottom bracket`,
  bumpers: $localize`Bumpers`,
  cassette: $localize`Cassette`,
  chain: $localize`Chain`,
  cleats: $localize`Cleats`,
  computer_mount: $localize`Computer mount`,
  crank_left_power_meter: $localize`Crank left power meter`,
  crank_right_power_meter: $localize`Crank right power meter`,
  crankset: $localize`Crankset`,
  crankset_power_meter: $localize`Crankset power meter`,
  fin: $localize`Fin`,
  footpad: $localize`Footpad`,
  footstraps: $localize`Footstraps`,
  fork: $localize`Fork`,
  frame: $localize`Frame`,
  front_break_oil: $localize`Front brake oil`,
  front_break_pads: $localize`Front brake pads`,
  front_break_rotor: $localize`Front brake rotor`,
  front_derailleur: $localize`Front derailleur`,
  front_foil: $localize`Front foil`,
  front_shifter: $localize`Front shifter`,
  front_tire: $localize`Front tire`,
  front_tube: $localize`Front tube`,
  front_tubeless_rim_tape: $localize`Front tubeless rim tape`,
  front_tubeless_sealant: $localize`Front tubeless sealant`,
  front_wheel: $localize`Front wheel`,
  front_wheel_valve: $localize`Front wheel valve`,
  fuselage: $localize`Fuselage`,
  grips: $localize`Grips`,
  grommets: $localize`Grommets`,
  handlebar: $localize`Handlebar`,
  handlebar_tape: $localize`Handlebar tape`,
  harness_lines: $localize`Harness lines`,
  headset: $localize`Headset`,
  helmet: $localize`Helmet`,
  impact_vest: $localize`Impact vest`,
  insoles: $localize`Insoles`,
  laces: $localize`Laces`,
  lifeguard_vest: $localize`Lifeguard vest`,
  mast: $localize`Mast`,
  mast_base: $localize`Mast base`,
  mast_extension: $localize`Mast extension`,
  mast_universal_joint: $localize`Mast universal joint`,
  overgrip: $localize`Overgrip`,
  pedals: $localize`Pedals`,
  pedals_left_power_meter: $localize`Pedals left power meter`,
  pedals_power_meter: $localize`Pedals power meter`,
  pedals_right_power_meter: $localize`Pedals right power meter`,
  rear_derailleur: $localize`Rear derailleur`,
  rear_shifter: $localize`Rear shifter`,
  rigging_lines: $localize`Rigging lines`,
  saddle: $localize`Saddle`,
  sail: $localize`Sail`,
  seatpost: $localize`Seatpost`,
  stabilizer: $localize`Stabilizer`,
  stem: $localize`Stem`,
  strings: $localize`Strings`,
  wing: $localize`Wing`,
};

export function componentLabel(type: string): string {
  return COMPONENT_LABELS[type] ?? type;
}

const CURRENCY_CODES: Record<User['currency'], string> = {
  euro: 'EUR',
  dollar: 'USD',
  pound: 'GBP',
};

export function formatMoney(value: number | null, currency: User['currency']): string {
  if (value == null) return '—';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: CURRENCY_CODES[currency],
  }).format(value);
}

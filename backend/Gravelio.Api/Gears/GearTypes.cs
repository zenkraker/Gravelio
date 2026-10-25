namespace Gravelio.Api.Gears;

public static class GearTypes
{
    public const int Bike = 1, Shoes = 2, Wetsuit = 3, Racquet = 4, Ski = 5, Snowboard = 6, Windsurf = 7, WaterSportsBoard = 8;

    public static bool IsValid(int type) => type is >= Bike and <= WaterSportsBoard;

    /// Component types each gear type may carry (ported from Endurain); other gear types have none.
    static readonly Dictionary<int, HashSet<string>> ComponentTypes = new()
    {
        [1] = ["back_break_oil", "back_break_pads", "back_break_rotor", "back_tire", "back_tube", "back_tubeless_sealant", "back_tubeless_rim_tape", "back_wheel", "back_wheel_valve", "bottom_bracket", "bottle_cage", "cassette", "chain", "computer_mount", "crank_left_power_meter", "crank_right_power_meter", "crankset", "crankset_power_meter", "fork", "frame", "front_break_oil", "front_break_pads", "front_break_rotor", "front_derailleur", "front_shifter", "front_tire", "front_tube", "front_tubeless_sealant", "front_tubeless_rim_tape", "front_wheel", "front_wheel_valve", "grips", "handlebar", "handlebar_tape", "headset", "pedals", "pedals_left_power_meter", "pedals_power_meter", "pedals_right_power_meter", "rear_derailleur", "rear_shifter", "saddle", "seatpost", "stem"],
        [2] = ["cleats", "insoles", "laces"],
        [4] = ["basegrip", "bumpers", "grommets", "overgrip", "strings"],
        [7] = ["sail", "board", "mast", "boom", "mast_extension", "mast_base", "mast_universal_joint", "fin", "footstraps", "harness_lines", "rigging_lines", "footpad", "impact_vest", "lifeguard_vest", "helmet", "wing", "front_foil", "stabilizer", "fuselage"],
    };

    public static IReadOnlyCollection<string> ComponentTypesFor(int gearType) =>
        ComponentTypes.TryGetValue(gearType, out var types) ? types : [];

    public static bool AllowsComponent(int gearType, string componentType) =>
        ComponentTypes.TryGetValue(gearType, out var types) && types.Contains(componentType);

    /// The activity types that can have a default gear, and the gear type each takes (the same 15 slots Endurain offers).
    public static readonly IReadOnlyDictionary<int, int> DefaultSlots = new Dictionary<int, int>
    {
        [1] = 2,
        [2] = 2,
        [3] = 2,
        [11] = 2,
        [12] = 2,
        [4] = 1,
        [5] = 1,
        [6] = 1,
        [7] = 1,
        [9] = 3,
        [21] = 4,
        [15] = 5,
        [16] = 5,
        [17] = 6,
        [30] = 7,
    };
}

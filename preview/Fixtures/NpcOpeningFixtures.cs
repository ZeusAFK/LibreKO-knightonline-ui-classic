using LibreKO;

/// <summary>NPC opening fixtures (Inn Hostess, Blacksmith).</summary>
public static partial class PreviewFixtures
{
    /// <summary>
    /// The Inn Hostess's reply (warehouse sub 0x10) as the running game receives it: the client builds its
    /// choice dialogue and the plugin's network handler marks it as the inn's.
    /// </summary>
    public static void OnWarehouseNpc(World world)
    {
        Call(world, "OnWarehouseOpen");
        NativeNpcOpening.Opened(world, NativeNpcOpening.Inn);
    }

    /// <summary>A repairing merchant's reply (Blacksmith, Sundries): the client's trade/repair choice, marked as the smith's.</summary>
    public static void OnRepairNpc(World world, int sellingGroup)
    {
        Call(world, "OnRepairOpen", sellingGroup);
        NativeNpcOpening.Opened(world, NativeNpcOpening.Smith);
    }
}

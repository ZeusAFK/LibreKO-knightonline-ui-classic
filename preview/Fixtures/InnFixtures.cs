using LibreKO;

/// <summary>Inn Hostess fixtures.</summary>
public static partial class PreviewFixtures
{
    /// <summary>
    /// The Inn Hostess's reply (warehouse sub 0x10) as the running game receives it: the client builds its
    /// choice dialogue and the plugin's network handler marks it as the inn's.
    /// </summary>
    public static void OnWarehouseNpc(World world)
    {
        Call(world, "OnWarehouseOpen");
        NativeInn.Opened(world);
    }
}

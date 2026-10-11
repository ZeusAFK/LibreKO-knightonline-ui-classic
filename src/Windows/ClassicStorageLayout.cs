using Godot;

namespace KnightOnlineUiClassic.Windows;

public static class ClassicStorageLayout
{
    /// <summary>
    /// VIP storage shows two stored rows. Its upper part closes under them instead of keeping the space of rows
    /// 3-4: the PIN actions take the place of the status band and the expiry takes the stored-coins row, so the
    /// window is <see cref="VipCut"/> pixels shorter and keeps the warehouse rhythm (grid, 28-pixel band, rows
    /// 12 pixels apart, separator).
    /// </summary>
    public const float VipCut = 58;
    public static float Cut(bool vip) => vip ? VipCut : 0;
    public static Vector2 Size(bool vip) => new(366, 553 - Cut(vip));
    public static readonly Rect2 Title = new(14, 16, 338, 24);
    /// <summary>Status line in the band under the stored cells.</summary>
    public static Rect2 Status(bool vip) => new(14, vip ? 154 : 253, 336, 18);
    public static readonly Rect2 StoredCaption = new(14, 280, 178, 20);
    public static Rect2 PageUp(bool vip) => new(318, vip ? 61 : 98, 40, 17);
    public static Rect2 PageDown(bool vip) => new(318, vip ? 120 : 181, 40, 17);
    public static Rect2 Page(bool vip) => new(318, vip ? 83 : 132, 40, 32);
    public static readonly Rect2 StoredGold = new(231, 280, 119, 20);
    /// <summary>VIP PIN actions: two equal buttons spanning the stored-coins row width, 28 pixels under the cells.</summary>
    public static readonly Rect2 ChangePin = new(13, 177, 165, 27), ClearPin = new(185, 177, 165, 27);
    /// <summary>VIP expiry on the stored-coins row: the caption and the value on the original field.</summary>
    public static readonly Rect2 ExpiryCaption = new(14, 280 - VipCut, 178, 20), Expiry = new(231, 280 - VipCut, 119, 20);
    /// <summary>The VIP key plaque beside the expiry field, where the coin sits beside the stored coins.</summary>
    public static readonly Rect2 Key = new(204, 220, 24, 24);
    /// <summary>The stored-coins field and the section separator in the warehouse art.</summary>
    public const float FieldTop = 274, Separator = 309;
    public static Rect2 Weight(bool vip) => new(14, 319 - Cut(vip), 181, 20);
    public static Rect2 Gold(bool vip) => new(231, 319 - Cut(vip), 119, 20);
    public static Rect2 Coin(bool vip) => new(206, 316 - Cut(vip), 22, 25);
    public static Rect2 Stored(int index) => new(13 + index % 6 * 49, 52 + index / 6 * 49, 45, 45);
    public static Rect2 Bag(int index, bool vip = false) => new(13 + index % 7 * 49, 349 - Cut(vip) + index / 7 * 49, 45, 45);
}

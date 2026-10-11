using Godot;

namespace KnightOnlineUiClassic.Layout;

public static class ClassicPetBarLayout
{
    public static readonly Vector2 Size = new(384, 40);
    public static readonly Rect2 Grip = new(0, 2, 28, 36);
    public static readonly Rect2 Attack = new(30, 4, 32, 32);
    public static readonly Rect2 Page = new(358, 4, 24, 32);
    public static Rect2 Skill(int index) => new(70 + index * 36, 4, 32, 32);
}

/// <summary>
/// The familiar plate as one Classic party member slot (co_party_us): health and mana bars, then satisfaction
/// in the party art's green and experience in the status bar's experience fill, with the name and level below.
/// </summary>
public static class ClassicPetPlateLayout
{
    public static readonly Vector2 Size = new(131, 50);
    public static readonly Rect2 Hp = new(8, 1, 108, 12);
    public static readonly Rect2 Mp = new(8, 11, 108, 7);
    public static readonly Rect2 Satisfaction = new(8, 19, 108, 7);
    public static readonly Rect2 Exp = new(8, 27, 108, 6);
    public static readonly Rect2 Name = new(9, 34, 74, 15);
    public static readonly Rect2 Level = new(84, 34, 32, 15);
}

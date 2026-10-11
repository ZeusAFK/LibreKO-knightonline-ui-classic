using Godot;
using LibreKO;
namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Geometry of the Power-Up Store drawn on the original Power Up Store art (the <c>re_powerupstore</c> UIF of a
/// 2.x Knight Online client). The art is bundled in <c>assets/myko</c> (<see cref="LocalAssets"/>); the rectangles below
/// are the UIF's own (1024×765 stage) and name what each native control takes over.
/// </summary>
public static class ClassicStoreMykoLayout
{
    public const string LayoutName = "re_powerupstore";
    /// <summary>The local UIF, or null when the art is not installed.</summary>
    public static LayoutNode? Art => Plugin.Kit.Local.Layout(LayoutName);
    public static readonly Vector2 Stage = new(1024, 765);
    /// <summary>The stage shrinks uniformly on screens that cannot hold it, keeping this margin.</summary>
    public const float ScreenMargin = 16;
    public static float Scale(Vector2 screen) => Mathf.Min(1, Mathf.Min((screen.X - 2 * ScreenMargin) / Stage.X, (screen.Y - 2 * ScreenMargin) / Stage.Y));

    // Touch: plates behind a native touch target (checkout, Clear all, gift, recharge, pager) grow to a 40 px hit
    // target with stretched art; the CLOSE plate and the card buttons keep the art size, because the card itself
    // opens the native details, whose buttons are touch-sized.
    public const float TouchHeight = 40;
    private static readonly Dictionary<string, Rect2> TouchPlates = new()
    {
        ["btn_buyall"] = new(839, 654, 72, 40), ["btn_clearall"] = new(912, 654, 72, 40), ["btn_refund"] = new(837, 146, 155, 40),
        ["btn_reseller"] = new(843, 47, 170, 40), ["btn_previous"] = new(251, 696, 36, 36), ["btn_next"] = new(394, 696, 36, 36),
    };
    /// <summary>Where an original button plate sits: its UIF rectangle, or the larger touch target.</summary>
    public static Rect2 Plate(LayoutNode art) => Platform.TouchUi && TouchPlates.TryGetValue(art.Id, out var touch) ? touch : new Rect2(art.Position, art.SizeVec);

    // Window chrome: the ornamental strip (dragging) and the stone CLOSE plate.
    public static readonly Rect2 Drag = new(0, 0, 880, 38);
    public const string Close = "btn_close";
    /// <summary>Art pieces without a native feature behind them: the TL balance plate (690,694).</summary>
    public static readonly Rect2[] OmittedArt = { new(690, 694, 149, 38) };

    // Header row: native search field, sort list and catalogue title.
    public const string Search = "edit_search";
    public static readonly Rect2 SearchField = new(26, 116, 244, 34);
    public static readonly Rect2 Sort = new(276, 116, 160, 34);
    public static readonly Rect2 GridTitle = new(444, 118, 219, 30);
    /// <summary>Native recharge (Get more cash) on the original "Buy TL Balance" plate.</summary>
    public const string Recharge = "btn_reseller";
    public const string RechargeCaption = "Get Knight Cash";

    // Category tabs (btn_tab1..5): one row over the item well; more than five categories share the row.
    public static Rect2 TabRow => Platform.TouchUi ? new(31, 151, 632, 36) : new(31, 157, 632, 30);
    public const int TabWidth = 126, TabFont = 12;
    public static readonly Color TabInk = new("c6b58c"), TabSelectedInk = new("fff1c0");
    public static Rect2 Tab(int index, int count)
    {
        float width = count <= 5 ? TabWidth : Mathf.Floor(TabRow.Size.X / count);
        var row = TabRow;
        return new Rect2(row.Position.X + index * width, row.Position.Y, width, row.Size.Y);
    }

    // Item well: 3×3 cards of 196×153 (items1..3 × item1..3), turned page by page.
    public const int Columns = 3, Rows = 3;
    public static readonly Vector2 Card = new(196, 153);
    public const int CardGapX = 8, CardGapY = 13;
    public static readonly Rect2 Grid = new(47, 207, Columns * 196 + (Columns - 1) * CardGapX, Rows * 153 + (Rows - 1) * CardGapY);
    public static float PageHeight => Rows * (Card.Y + CardGapY);
    public const string Previous = "btn_previous", Next = "btn_next";
    public static readonly Rect2 PageText = new(296, 706, 90, 16);

    // Card parts, relative to the card (item1 children).
    public static readonly Rect2 CardName = new(4, 8, 189, 23);
    public static readonly Rect2 CardIconCell = new(22, 34, 53, 53);
    public static readonly Rect2 CardIcon = new(27, 39, 43, 43);
    public static readonly Rect2 CardPriceCaption = new(80, 42, 48, 15);
    public static readonly Rect2 CardPrice = new(118, 42, 70, 15);
    /// <summary>Card values end inside the card border.</summary>
    public const float CardPriceEnd = 188;
    /// <summary>The second value line ("Piece :" in the original) shows the native in-cart and sale-time chips in a row.</summary>
    public static readonly Vector2 CardChips = new(80, 63);
    public const float CardChipGap = 3; public const int CardChipFont = 11, CardCaptionFont = 11, CardOldPriceFont = 10;
    /// <summary>The featured star sits on the top-left corner of the icon cell, at three quarters of its size.</summary>
    public static readonly Vector2 CardBadge = new(13, 27);
    public const float CardBadgeScale = 0.75f;
    public static readonly Rect2 CardBuy = new(24, 92, 155, 23), CardAdd = new(24, 119, 155, 23);
    public const string CardBuyArt = "btn_purchase", CardAddArt = "btn_add_basket";
    public static readonly Color CardNameInk = new("c6b58c"), CardPriceInk = new("ff8000"), CardButtonInk = new("c6b58c");

    // My Basket: header tab, native lines and messages, totals, checkout and balance.
    public static readonly Rect2 BasketTitle = new(722, 156, 96, 29);
    public static Rect2 Basket => Platform.TouchUi ? new(719, 194, 246, 456) : new(719, 194, 246, 462);
    public static readonly Rect2 BasketRowArtTop = new(512, 487, 238, 24), BasketRowArtMiddle = new(15, 247, 238, 8), BasketRowArtBottom = new(512, 520, 238, 22);
    public static readonly Rect2 BasketIconCell = new(707, 78, 53, 53);
    public const int BasketRowHeight = 56;
    public const string BasketRow = "purchase_list1", BasketRemove = "btn_cancel";
    /// <summary>Cart steppers on the quantity buttons of the original purchase confirmation.</summary>
    public const string Confirm = "shopping_confirm", StepDown = "btn_down", StepUp = "btn_up";
    public static float StepperSide => Platform.TouchUi ? 36 : 24;
    public static float RemoveSide => Platform.TouchUi ? 36 : 22;
    public static float StepperCount => Platform.TouchUi ? 24 : 34;
    /// <summary>Touch rows drop the icon cell so the larger steppers and X fit the original row width.</summary>
    public static bool BasketIcons => !Platform.TouchUi;
    public static readonly Rect2 Total = new(698, 664, 138, 27);
    public const string Checkout = "btn_buyall", ClearCart = "btn_clearall";
    public static readonly Color CheckoutInk = new("00ff00"), ClearInk = new("ff4040");
    /// <summary>Native gift (send the basket to a friend or clan member) on the original "Refund Item" plate.</summary>
    public const string Gift = "btn_refund";
    public static readonly Rect2 Cash = new(866, 702, 113, 20);
}

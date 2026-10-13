using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

public partial class ChatWindow : Control
{
    private static readonly Vector2 DefaultSize = new(620, 190);
    public static float DefaultHeight => DefaultSize.Y;
    private static readonly Vector2 Minimum = new(620, 140);
    private static readonly (string Name, string Prefix)[] Channels =
    {
        ("All", ""), ("Private", "@"), ("Shout", "!"), ("Party", "#"),
        ("Clan", "$"), ("Noah", "|"), ("Nation", "%"), ("Alliance", "&"),
    };
    /// <summary>
    /// Every chat category, as the client's <c>ChatCategory.All</c>. It still holds the chat room bit, although room
    /// lines stay in the room window since ff78eef.
    /// </summary>
    public const int AllCategories = (int)LibreKO.Domain.ChatCategory.All;
    private const int FilterRevision = 2;
    /// <summary>The Noah Knights tab shows only for Noah Knights, and hides Clan and Alliance for them (the client's rule).</summary>
    public static bool Applies(string prefix, bool noahKnight) => prefix switch { "|" => noahKnight, "$" or "&" => !noahKnight, _ => true };
    /// <summary>The client's chat prefixes (<c>ChatPrefixes</c>, ff78eef dropped the chat room's <c>\</c>) and commands.</summary>
    private const string PrefixChars = "@!#$%&~|/+";
    private readonly PluginGame _game = Plugin.Kit.Game;
    private readonly LogText _log;
    private readonly ClassicScrollRail _rail;
    private readonly Panel _background;
    private readonly Panel _title;
    private readonly Panel _footer;
    private readonly LineEdit _input;
    private readonly List<Panel> _tabs = new();
    private readonly Control _surface = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ClassicChatTransparency _transparency;
    private HudLayout? _layout;
    private string _prefix = "";
    private readonly LibreKO.Domain.ChatInputHistory _history=new();
    private readonly LibreKO.Domain.ChatLinkDraft _links=new();
    private string _previousText="";
    private ClassicChatPanel? _settingsPanel,_nearbyPanel;
    private int _filter;
    private bool _timestamps;
    private string _colors="";
    private int _fontSize;
    private bool _appendQueued;
    private bool _noahKnight;


    public ChatWindow()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        Size = DefaultSize;
        CustomMinimumSize = Minimum;
        AddChild(_surface);
        _surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _background = ClassicChatControls.Backdrop();
        _surface.AddChild(_background);
        _filter=LoadFilter();
        _timestamps=Plugin.Kit.Context.Settings.GetBool("chat.timestamps",false);
        _fontSize=Plugin.Kit.Context.Settings.GetInt("chat.font_size",13);
        if(_fontSize is not (12 or 13 or 15)) _fontSize=13;
        _colors=Plugin.Kit.Context.Settings.Get("chat.colors");
        _log = new LogText(_fontSize, Colors.White, Plugin.Kit.ChatFont);
        _log.EnableLinks();
        _log.MetaClicked+=meta=> {if(meta.StartsWith("p:")) _game.Chat.PlayerMenu(meta[2..],GetGlobalMousePosition());};
        _log.MetaHoverStarted+=meta=> {if(meta.StartsWith("i:") && int.TryParse(meta[2..],out int id)) _game.Chat.ShowLinkTooltip(id);};
        _log.MetaHoverEnded+=()=>_game.Chat.HideLinkTooltip();
        _surface.AddChild(_log);
        _rail = new ClassicScrollRail(_log);
        _surface.AddChild(_rail);
        _title = ClassicChatControls.Title("Chat");
        _surface.AddChild(_title);
        _footer = ClassicChatControls.Frame();
        _surface.AddChild(_footer);
        _surface.MoveChild(_footer, 1);
        var group = new ButtonGroup();
        foreach (var (name, prefix) in Channels)
        {
            var tab = ClassicChatControls.Tab(name);
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.ButtonPressed = prefix.Length == 0;
            tab.Pressed += () => Select(prefix);
            tab.TooltipText="Send to "+name+"; right-click for chat settings";
            tab.GuiInput+=ev=> {if(ev is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Right}) {OpenSettings();tab.AcceptEvent();}};
            var frame = ClassicChatControls.TabFrame(tab);
            _tabs.Add(frame);
            _surface.AddChild(frame);
        }
        _input = new LineEdit { MaxLength = 128, KeepEditingOnTextSubmit = true, PlaceholderText = "Press Enter to chat" };
        ClassicChatControls.Input(_input, originalChat: true);
        _surface.AddChild(_input);
        _input.FocusEntered += () => _game.Chat.SetTyping(true);
        _input.FocusExited += () => _game.Chat.SetTyping(false);
        _input.TextSubmitted += Submit;
        _input.TextChanged+=text=> {_links.TextChanged(_previousText,text);_previousText=text;ApplyInputLimit();};
        _input.GuiInput+=ev=> {
            if(ev is not InputEventKey {Pressed:true} key || key.CtrlPressed || key.AltPressed || key.MetaPressed) return;
            string? text=key.Keycode==Key.Up ? _history.Older(_input.Text) : key.Keycode==Key.Down ? _history.Newer() : null;
            if(text==null) return;
            _links.Clear();_input.MaxLength=128;_input.Text=text;_previousText=text;_input.CaretColumn=text.Length;_input.AcceptEvent();
        };
        ClassicChatControls.SnapToPixels(this);
        Resized += Arrange;
        Arrange();
        _transparency = new ClassicChatTransparency(_surface, _title, "chat.transparency_step");
    }

    public override void _EnterTree()
    {
        _game.Chat.LineAdded += Append;
        NativeGame.Source.ItemLinkRequested+=InsertItem;
        _game.Chat.InputRequested += FocusInput;
        NativeGame.Source.ChannelRequested += FocusChannel;
        _game.BecameAvailable += Reload;
        _game.Character.Changed += RefreshNoah;
        _layout ??= NativeLayout.Attach(this, "classic_chat", _title, DefaultPosition,
            resizable: true, defaultSize: DefaultSize, minimumSize: Minimum, resizeCorner: HudLayout.Corner.TopRight,
            backgroundOpacityChanged: _ => _transparency.Cycle(),legacyResizeGrip:true, resizeSnapPeerId:"classic_log");
        _layout.Locked=Plugin.Kit.Context.Settings.GetBool("chat.locked",false);
        Reload();
    }

    public override void _ExitTree()
    {
        _game.Chat.LineAdded -= Append;
        NativeGame.Source.ItemLinkRequested-=InsertItem;
        _game.Chat.HideLinkTooltip();
        _settingsPanel?.QueueFree();_nearbyPanel?.QueueFree();
        _game.Chat.InputRequested -= FocusInput;
        NativeGame.Source.ChannelRequested -= FocusChannel;
        _game.BecameAvailable -= Reload;
        _game.Character.Changed -= RefreshNoah;
    }

    /// <summary>
    /// The saved category filter. Filters saved before the Noah Knights and chat room categories existed (masked
    /// with 1023) gain both where they show General, as the client's own chat preferences do (rev 2). No room line
    /// reaches the chat since ff78eef, so the chat room bit only keeps the filter equal to the client's.
    /// </summary>
    private static int LoadFilter()
    {
        var settings = Plugin.Kit.Context.Settings;
        int filter = settings.GetInt("chat.filter", AllCategories);
        if (settings.GetInt("chat.filter_rev", 1) < FilterRevision)
        {
            if ((filter & (int)LibreKO.Domain.ChatCategory.General) != 0)
                filter |= (int)(LibreKO.Domain.ChatCategory.NoahKnights | LibreKO.Domain.ChatCategory.ChatRoom);
            settings.SetInt("chat.filter", filter & AllCategories);
            settings.SetInt("chat.filter_rev", FilterRevision);
        }
        return filter & AllCategories;
    }

    private void RefreshNoah()
    {
        bool noah = _game.Available && _game.Character.NoahKnight;
        if (noah == _noahKnight) return;
        _noahKnight = noah;
        if (!Applies(_prefix, noah)) FocusTab("");
        Arrange();
    }

    private void FocusTab(string prefix)
    {
        _prefix = prefix;
        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].GetChildren().OfType<BaseButton>().FirstOrDefault() is { } button)
                button.SetPressedNoSignal(Channels[i].Prefix == prefix);
    }

    private void Arrange()
    {
        float footer = Mathf.Round(Size.Y) - 24;
        _background.Size = new Vector2(Size.X, footer);
        _title.Position = new Vector2(0, footer);
        _title.Size = new Vector2(64, 24);
        _footer.Position = new Vector2(0, footer);
        _footer.Size = new Vector2(DefaultSize.X, 24);
        int shown = Channels.Count(c => Applies(c.Prefix, _noahKnight));
        float width = (DefaultSize.X - 64) / shown;
        for (int i = 0, slot = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Visible = Applies(Channels[i].Prefix, _noahKnight);
            if (!_tabs[i].Visible) continue;
            float left = Mathf.Round(64 + slot * width);
            float right = Mathf.Round(64 + (slot + 1) * width);
            _tabs[i].Position = new Vector2(left, footer);
            _tabs[i].Size = new Vector2(right - left, 24);
            slot++;
        }
        _log.Position = new Vector2(24, 4);
        _log.Size = new Vector2(Size.X - 44, footer - 32);
        _rail.Position = new Vector2(3, 4);
        _rail.Size = new Vector2(18, footer - 32);
        _input.Position = new Vector2(3, footer - 24);
        _input.Size = new Vector2(Size.X - 6, 22);
    }

    private Vector2 DefaultPosition() => new(4, GetViewport().GetVisibleRect().Size.Y - Taskbar.BarHeight - Size.Y - 4);
    private void Reload()
    {
        RefreshNoah();
        _log.Set(_game.Chat.ReadHistory(_filter,_timestamps,_colors));
    }
    // The client publishes a line before it stores it, so the structured history is read once the
    // dispatch has finished; several lines in one frame refresh the log once.
    private void Append(string bbcode)
    {
        if(_appendQueued) return;
        _appendQueued=true;
        Callable.From(()=> {_appendQueued=false;if(IsInstanceValid(this)) _log.SetPreservingScroll(_game.Chat.ReadHistory(_filter,_timestamps,_colors));}).CallDeferred();
    }
    private void ApplyInputLimit()
    {
        int limit=128-_links.WireExtra;
        if(_input.MaxLength==limit) return;
        int caret=_input.CaretColumn;
        _input.MaxLength=limit;
        _input.CaretColumn=caret;
    }

    private void InsertItem(int id,string name)
    {
        if(!_input.HasFocus()) return;
        var result=_links.Insert(_input.Text,_input.CaretColumn,id,name);
        if(result.Text.Length+_links.WireExtra>128) {_links.Clear();return;}
        _input.MaxLength=128;_input.Text=result.Text;_previousText=result.Text;_input.CaretColumn=result.Caret;ApplyInputLimit();
    }


    private void Select(string prefix)
    {
        _prefix = prefix;
        if (prefix != "@") return;
        FocusInput();
        _input.Text = _game.Target.Has && _game.Target.IsPlayer ? "@" + _game.Target.Name + " " : "@";
        _input.CaretColumn = _input.Text.Length;
    }

    private void FocusInput()
    {
        _input.GrabFocus();
        _input.CaretColumn = _input.Text.Length;
        // A prefill reaches the client's hidden input right after this request.
        Callable.From(TakePrefill).CallDeferred();
    }

    private void TakePrefill()
    {
        if (!IsInstanceValid(this) || NativeChat.TakeTyped() is not { Length: > 0 } text) return;
        _links.Clear(); _input.MaxLength = 128; _input.Text = text; _previousText = text; _input.CaretColumn = text.Length; ApplyInputLimit();
        _input.GrabFocus();
    }

    private void FocusChannel(string prefix)
    {
        FocusTab(prefix);
        FocusInput();
    }

    private void Submit(string text)
    {
        string historyText=text;
        string wire=_links.ToWire(text);
        _links.Clear();_input.MaxLength=128;_previousText="";
        _input.Clear();
        _input.ReleaseFocus();
        text = wire.Trim();
        if (text.Length == 0) return;
        _history.Push(PrefixChars.Contains(historyText[0]) || _prefix.Length==0 ? historyText : _prefix+historyText);
        _game.Chat.Send(PrefixChars.Contains(text[0]) || _prefix.Length == 0 ? text : _prefix + text);
    }

    public override void _Input(InputEvent ev)
    {
        if(ev is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Escape})
        {
            var panel=_nearbyPanel?.IsVisibleInTree()==true ? _nearbyPanel : _settingsPanel?.IsVisibleInTree()==true ? _settingsPanel : null;
            if(panel!=null) {panel.Visible=false;GetViewport().SetInputAsHandled();return;}
        }
        if (!_input.HasFocus() || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _links.Clear();_input.MaxLength=128;_previousText="";
        _input.Clear();
        _input.ReleaseFocus();
        GetViewport().SetInputAsHandled();
    }
}

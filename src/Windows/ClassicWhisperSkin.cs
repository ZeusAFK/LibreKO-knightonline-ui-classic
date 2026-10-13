using System.Text.RegularExpressions;
using Godot;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using LibreKO;

namespace KnightOnlineUiClassic.Windows;

public static class ClassicWhisperSkin
{
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach(var child in root.GetChildren())
        {
            yield return child;
            foreach(var nested in Descendants(child)) yield return nested;
        }
    }

    public static void Apply(HudWindow window)
    {
        var kit=Plugin.Kit;
        // The original client uses this common PM artwork for both nations.
        var open=kit.Layout("co_whisper_open_us");
        var closed=kit.Layout("co_whisper_close_us");
        // Every new Classic conversation opens at the same upper-right origin.
        // Existing conversations retain their manually dragged position.
        Vector2 StartPosition() => new(Mathf.Max(0, window.GetViewportRect().Size.X-open.W-12),96);
        window.DockTo(StartPosition);
        window.Position=StartPosition();
        var header=window.Header!;
        var nativeGrip=Descendants(header).OfType<HBoxContainer>().First();
        string name=Descendants(header).OfType<Label>().First(label=>label.Text!="◆").Text;
        var nativeClose=Descendants(header).OfType<Button>().Last();
        // The client's header actions (player menu, size and transparency, whisper block) sit before its
        // minimize and close buttons; they move onto the original title bar with their callbacks.
        var actions=Descendants(header).OfType<Button>().Where(b=>b.Icon!=null && b!=nativeClose && b.TooltipText is not ("Minimize" or "Restore")).ToArray();
        // fa3cfc3: the conversation runs on the room's thread and composer (voice notes, reactions, history).
        // Older clients and the HUD fixtures keep a scroll, an input and a "Send" button of their own.
        var parts=NativeWhispers.Parts(window);
        var scroll=parts?.Thread ?? Descendants(window.Body).OfType<ScrollContainer>().First();
        var input=parts?.Input ?? Descendants(window.Body).OfType<LineEdit>().First();
        var nativeSend=parts?.Send ?? Descendants(window.Body).OfType<Button>().FirstOrDefault(b=>b.Text=="Send" || b.TooltipText=="Send")
            ?? Descendants(window.Body).OfType<Button>().First();

        var surface=new Control { Name="classic_whisper",MouseFilter=Control.MouseFilterEnum.Ignore };
        var expanded=new LayoutView(kit,open);
        var compact=new LayoutView(kit,closed) { Visible=false };
        surface.AddChild(expanded);
        surface.AddChild(compact);
        foreach(var native in window.GetChildren().OfType<MarginContainer>()) native.Visible=false;
        window.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        window.TextureFilter=CanvasItem.TextureFilterEnum.Nearest;
        window.AddChild(surface);

        foreach(var view in new[]{expanded,compact})
        {
            var title=view.Get<Label>("exit_id")!;
            title.Text=name;
            title.AddThemeFontOverride("font",kit.Bold);
            title.AddThemeFontSizeOverride("font_size",UiKit.FontSize(open.Find("exit_id")!));
            title.HorizontalAlignment=HorizontalAlignment.Left;
            title.VerticalAlignment=VerticalAlignment.Center;
            title.AutowrapMode=TextServer.AutowrapMode.Off;
            title.ClipText=true;
            title.TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis;
            title.Position=new Vector2(view==expanded ? 8:6,2);
            title.Size=new Vector2(view==expanded ? 216-actions.Length*ClassicWhisperLayout.ActionPitch:94,20);
            title.AddThemeColorOverride("font_outline_color",Colors.Black);
            title.AddThemeConstantOverride("outline_size",1);
            var grip=view.Get("btn_bar")!;
            grip.MouseDefaultCursorShape=Control.CursorShape.Move;
            grip.GuiInput+=ev=> {
                if(ev is not InputEventMouseButton { ButtonIndex:MouseButton.Left } press) return;
                nativeGrip.EmitSignal(Control.SignalName.GuiInput,press);
                grip.AcceptEvent();
            };
        }
        expanded.OnPressed("btn_hide",()=>window.SetMinimized(true));
        compact.OnPressed("btn_open",()=>window.SetMinimized(false));
        expanded.OnPressed("btn_close",()=>nativeClose.EmitSignal(BaseButton.SignalName.Pressed));
        // The original send cell runs the client's Send; the composer hides Send while a voice note records.
        expanded.OnPressed("btn_chat",()=>{ if(nativeSend.Visible) nativeSend.EmitSignal(BaseButton.SignalName.Pressed); });
        expanded.Get("btn_hide")!.TooltipText="Minimize";
        compact.Get("btn_open")!.TooltipText="Restore";
        expanded.Get("btn_close")!.TooltipText="Close";
        expanded.Get("btn_chat")!.TooltipText="Send (Enter)";

        // Reserve separate restore and close cells in the original compact frame.
        compact.Get("btn_open")!.Position=new Vector2(104,3);
        var compactBar=(TextureButton)compact.Get("btn_bar")!;
        compactBar.Size=new Vector2(101,15);
        AtlasTexture? HeaderCrop(Texture2D? texture) => texture is AtlasTexture atlas ? new AtlasTexture {
            Atlas=atlas.Atlas,Region=new Rect2(atlas.Region.Position,new Vector2(101,15)),FilterClip=true,
        }:null;
        compactBar.TextureNormal=HeaderCrop(compactBar.TextureNormal);
        compactBar.TextureHover=HeaderCrop(compactBar.TextureHover);
        compactBar.TexturePressed=HeaderCrop(compactBar.TexturePressed);
        compactBar.TextureDisabled=HeaderCrop(compactBar.TextureDisabled);
        var closeSource=(TextureButton)expanded.Get("btn_close")!;
        var compactClose=new TextureButton {
            Name="btn_close",Position=new Vector2(124,3),Size=new Vector2(19,15),
            IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.Scale,
            TextureNormal=closeSource.TextureNormal,TextureHover=closeSource.TextureHover,
            TexturePressed=closeSource.TexturePressed,TextureDisabled=closeSource.TextureDisabled,
            FocusMode=Control.FocusModeEnum.None,TooltipText="Close",
        };
        compact.AddChild(compactClose);
        compactClose.Pressed+=()=>nativeClose.EmitSignal(BaseButton.SignalName.Pressed);

        for(int i=0;i<actions.Length;i++)
        {
            var action=actions[i];
            action.Reparent(expanded,keepGlobalTransform:false);
            action.Name="whisper_action_"+i;
            action.CustomMinimumSize=Vector2.Zero;
            action.Position=ClassicWhisperLayout.Action(i,actions.Length).Position;
            action.Size=ClassicWhisperLayout.Action(i,actions.Length).Size;
            action.AddThemeConstantOverride("icon_max_width",12);
            action.ExpandIcon=false;
            TitleIcon(action);
        }
        NativeWhispers.StylePopups(window,Popup);
        // fa3cfc3: the composer opens its emoticon picker as its own child, outside the whisper layer.
        if(parts!=null)
            parts.Composer.ChildEnteredTree+=node=> {
                if(node is PopupPanel popup) Callable.From(()=> { if(Plugin.Active && GodotObject.IsInstanceValid(popup)) Popup(popup); }).CallDeferred();
            };
        var attention=new ClassicWhisperAttention(compactBar,compact.Get<Label>("exit_id")!);
        surface.AddChild(attention);
        window.AttentionStyler=attention.SetUnread;

        var logArea=open.Find("exit_chat")!;
        expanded.Get("exit_chat")!.Visible=false;
        scroll.Reparent(expanded,keepGlobalTransform:false);
        scroll.CustomMinimumSize=Vector2.Zero;
        scroll.Position=logArea.Position;
        scroll.Size=logArea.SizeVec;
        scroll.HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode=ScrollContainer.ScrollMode.ShowNever;
        scroll.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        foreach(var log in Descendants(scroll).OfType<VBoxContainer>()) log.AddThemeConstantOverride("separation",0);
        if(parts!=null) ClassicWhisperThread.Attach(parts,logArea.W);

        var edit=open.Find("edit_chat")!;
        var template=expanded.Get<LineEdit>("edit_chat")!;
        foreach(var background in template.GetChildren().OfType<TextureRect>().ToArray())
            background.Reparent(input,keepGlobalTransform:false);
        template.Visible=false;
        // fa3cfc3: the composer hides its field (the input) while a voice note records, so the field moves along.
        Control editHost=(Control?)parts?.Field ?? input;
        editHost.Reparent(expanded,keepGlobalTransform:false);
        editHost.CustomMinimumSize=Vector2.Zero;
        input.CustomMinimumSize=Vector2.Zero;
        editHost.Position=edit.Position;
        input.PlaceholderText="";
        var textStyle=edit.Strings.First();
        void StyleInput()
        {
            if(!Plugin.Active || !GodotObject.IsInstanceValid(input)) return;
            // The client's emoticon button sits inside the right end of the input; the text stops before it.
            foreach(var state in new[]{"normal","focus","read_only"})
                input.AddThemeStyleboxOverride(state,new StyleBoxEmpty { ContentMarginTop=textStyle.Y-edit.Y,ContentMarginRight=ClassicWhisperLayout.EmoticonRoom });
        }
        StyleInput();
        // The composer widens the input's own style for the emoticon button whenever the input enters the tree.
        input.TreeEntered+=StyleInput;
        input.AddThemeFontOverride("font",kit.ChatStrong);
        input.AddThemeFontSizeOverride("font_size",13);
        input.AddThemeColorOverride("font_color",textStyle.Color);
        input.AddThemeColorOverride("caret_color",textStyle.Color);
        input.AddThemeColorOverride("font_outline_color",Colors.Black);
        input.AddThemeConstantOverride("outline_size",1);
        // The client's 20 px emoticon button overflows the original edit art; it sits on the band of the send cell.
        foreach(var face in input.GetChildren().OfType<TextureButton>())
        {
            face.Name="whisper_emoticons";
            face.CustomMinimumSize=new Vector2(ClassicWhisperLayout.EmoticonSize,ClassicWhisperLayout.EmoticonSize);
            face.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            face.OffsetLeft=-ClassicWhisperLayout.EmoticonInset-ClassicWhisperLayout.EmoticonSize;
            face.OffsetRight=-ClassicWhisperLayout.EmoticonInset;
            face.OffsetTop=ClassicWhisperLayout.Record.Position.Y-edit.Y;
            face.OffsetBottom=face.OffsetTop+ClassicWhisperLayout.EmoticonSize;
        }
        editHost.Size=edit.SizeVec;
        var classicSend=(TextureButton)expanded.Get("btn_chat")!;
        if(parts!=null) Composer(expanded,parts,textStyle.Color);
        // The client sizes its thread from the whisper size option; the original frame keeps its log area.
        NativeWindows.Sync(window,()=> {
            if(scroll.CustomMinimumSize!=Vector2.Zero) scroll.CustomMinimumSize=Vector2.Zero;
            if(scroll.Size!=logArea.SizeVec) scroll.Size=logArea.SizeVec;
            // The original send art has no disabled image; while a voice note records the cell takes the Classic
            // cells' disabled tint.
            bool off=!nativeSend.Visible;
            if(classicSend.Disabled!=off) { classicSend.Disabled=off; classicSend.SelfModulate=off ? ClassicWhisperLayout.DisabledTint:Colors.White; }
        });

        BindScroll(expanded,open.Find("scroll")!,scroll.GetVScrollBar(),kit.ChatStrong.GetHeight(13));
        void Arrange(bool minimized)
        {
            if(minimized && input.HasFocus()) input.ReleaseFocus();
            expanded.Visible=!minimized;
            compact.Visible=minimized;
            var size=minimized ? closed.SizeVec:open.SizeVec;
            surface.CustomMinimumSize=size;
            surface.Size=size;
            window.CustomMinimumSize=size;
            window.Size=size;
            window.CallDeferred(Control.MethodName.ResetSize);
        }
        window.MinimizedChanged+=Arrange;
        Arrange(window.Minimized);
    }

    /// <summary>
    /// The composer's voice-note parts in the original frame: the record button in a cell mirroring the send
    /// cell, and the recording strip (dot, clock, level, cancel) over the edit band. The composer still shows
    /// and hides them, swaps the record icon to "send the note" and runs every callback.
    /// </summary>
    private static void Composer(LayoutView view,WhisperParts parts,Color ink)
    {
        var record=parts.Record;
        record.Reparent(view,keepGlobalTransform:false);
        record.Name="whisper_record";
        ClassicChatRoomSkin.Cell(record);
        record.CustomMinimumSize=Vector2.Zero;
        record.AddThemeConstantOverride("icon_max_width",(int)ClassicWhisperLayout.Record.Size.X-3);
        record.Position=ClassicWhisperLayout.Record.Position;
        record.Size=ClassicWhisperLayout.Record.Size;

        var strip=parts.Strip;
        strip.Reparent(view,keepGlobalTransform:false);
        strip.Name="whisper_record_strip";
        strip.AddThemeConstantOverride("separation",ClassicWhisperLayout.StripGap);
        var labels=strip.GetChildren().OfType<Label>().ToArray();
        for(int i=0;i<labels.Length;i++)
        {
            var label=labels[i];
            label.AddThemeFontOverride("font",Plugin.Kit.ChatStrong);
            label.AddThemeFontSizeOverride("font_size",ClassicWhisperLayout.LineSize);
            label.AddThemeColorOverride("font_outline_color",Colors.Black);
            label.AddThemeConstantOverride("outline_size",1);
            label.VerticalAlignment=VerticalAlignment.Center;
            // The recording dot keeps the client's red; the clock reads in the edit field's ink.
            if(i>0) { label.AddThemeColorOverride("font_color",ink); label.Name="whisper_record_clock"; }
        }
        foreach(var level in strip.GetChildren().OfType<ProgressBar>()) { ClassicChatRoomSkin.Meter(level); level.Name="whisper_record_level"; }
        foreach(var cancel in strip.GetChildren().OfType<Button>())
        {
            ClassicChatRoomSkin.Cell(cancel);
            cancel.Name="whisper_record_cancel";
            cancel.CustomMinimumSize=new Vector2(ClassicWhisperLayout.StripCancel,ClassicWhisperLayout.StripCancel);
            cancel.AddThemeConstantOverride("icon_max_width",(int)ClassicWhisperLayout.StripCancel-4);
            cancel.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
        }
        strip.Position=ClassicWhisperLayout.Strip.Position;
        strip.Size=ClassicWhisperLayout.Strip.Size;
    }

    /// <summary>A title-bar icon on the original bar: no plate, a light hover, the title's cyan ink (unless the client tints it).</summary>
    public static void TitleIcon(Button action)
    {
        action.IconAlignment=HorizontalAlignment.Center;
        action.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
        var hover=new StyleBoxFlat { BgColor=new Color(1,1,1,.16f) };
        foreach(var state in new[]{"hover","pressed","hover_pressed"}) action.AddThemeStyleboxOverride(state,hover);
        action.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
        // Header icons keep the original cyan of the title; the block toggle keeps the client's red or green state.
        if(!action.HasThemeColorOverride("icon_normal_color")) action.AddThemeColorOverride("icon_normal_color",ClassicWhisperThread.ActionInk);
        action.AddThemeColorOverride("icon_hover_color",Colors.White);
    }

    private static void BindScroll(LayoutView view,LayoutNode source,VScrollBar range,float lineHeight)
    {
        var arrows=source.Children.Where(n=>n.IsButton).OrderBy(n=>n.Y).ToArray();
        var up=(BaseButton)view.ControlOf(arrows[0])!;
        var down=(BaseButton)view.ControlOf(arrows[1])!;
        up.Pressed+=()=>range.Value-=lineHeight;
        down.Pressed+=()=>range.Value+=lineHeight;
        var trackSource=source.Children.First(n=>n.Type=="trackbar");
        view.ControlOf(trackSource)!.Visible=false;
        var track=new ClassicScrollTrack(range,trackSource,thumbHeight:trackSource.Images.First(n=>n.Tag==1).H,
            emptyAtEnd:false,scrollStep:lineHeight) {
            Position=trackSource.Position-source.Position,Size=trackSource.SizeVec,
        };
        view.ControlOf(source)!.AddChild(track);
        void Refresh()
        {
            up.Disabled=range.Value<=range.MinValue;
            down.Disabled=range.Value>=Math.Max(range.MinValue,range.MaxValue-range.Page);
            track.QueueRedraw();
        }
        range.Changed+=Refresh;
        range.ValueChanged+=_=>Refresh();
        Refresh();
    }

    /// <summary>The client's size and transparency popup and its emoticon picker on the Classic menu panel.</summary>
    public static void Popup(PopupPanel popup)
    {
        // The emoticon picker is the client's shared type (room composer, whisper composer, reactions): one Classic look.
        if(ClassicReactionStyle.Owns(popup)) { ClassicReactionStyle.Apply(popup); return; }
        if(popup.HasMeta("classic_popup")) return;
        popup.SetMeta("classic_popup",true);
        var panel=ClassicDesign.InputBox(); panel.BgColor=new Color(0,0,0,.94f); panel.SetContentMarginAll(6);
        popup.AddThemeStyleboxOverride("panel",panel);
        ClassicSocialStyle.Apply(popup);
    }

    public static Control Line(string name,bool mine,bool notice,string text)
    {
        var source=Plugin.Kit.Layout("co_whisper_open_us").Find("exit_chat")!;
        string ink=notice ? "ffff00":mine ? "ffffff":"80ffff";
        // Notices carry web links (3277df5): the client's linkify and its link check open them in the browser.
        if(notice && LibreKO.Domain.WhisperLinks.Linkify(text).Contains("[url="))
        {
            var linked=new RichTextLabel {
                BbcodeEnabled=true,FitContent=true,ScrollActive=false,Text=$"[color=#{ink}]{LibreKO.Domain.WhisperLinks.Linkify(text)}[/color]",
                AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
                CustomMinimumSize=new Vector2(source.W,0),MouseFilter=Control.MouseFilterEnum.Pass,Name="whisper_notice_link",
            };
            linked.AddThemeFontOverride("normal_font",Plugin.Kit.ChatStrong);
            linked.AddThemeFontSizeOverride("normal_font_size",13);
            linked.AddThemeColorOverride("font_outline_color",Colors.Black);
            linked.AddThemeConstantOverride("outline_size",1);
            linked.MetaClicked+=meta=> {string link=meta.AsString();if(LibreKO.Domain.WhisperLinks.IsWebLink(link)) OS.ShellOpen(link);};
            return linked;
        }
        // Item links and emoticons render as in the client's chat; other lines keep the plain Classic label.
        string rendered=NativeWhispers.Render(text,ink);
        if(rendered.Contains("[url") || rendered.Contains("[img"))
        {
            var rich=new RichTextLabel {
                BbcodeEnabled=true,FitContent=true,ScrollActive=false,MetaUnderlined=false,Text=rendered,
                AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
                CustomMinimumSize=new Vector2(source.W,0),MouseFilter=Control.MouseFilterEnum.Pass,
                HorizontalAlignment=mine && !notice ? HorizontalAlignment.Right:HorizontalAlignment.Left,
            };
            rich.AddThemeFontOverride("normal_font",Plugin.Kit.ChatStrong);
            rich.AddThemeFontSizeOverride("normal_font_size",13);
            rich.AddThemeColorOverride("font_outline_color",Colors.Black);
            rich.AddThemeConstantOverride("outline_size",1);
            rich.MetaClicked+=meta=>NativeWhispers.ShowLink(meta);
            rich.MetaHoverEnded+=_=>NativeChat.HideLinkTooltip();
            return rich;
        }
        var label=new Label {
            Text=NativeWhispers.Plain(text),
            HorizontalAlignment=mine && !notice ? HorizontalAlignment.Right:HorizontalAlignment.Left,
            AutowrapMode=TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            CustomMinimumSize=new Vector2(source.W,0),MouseFilter=Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font",Plugin.Kit.ChatStrong);
        label.AddThemeFontSizeOverride("font_size",13);
        // Keep the original cyan private-channel color for incoming messages.
        label.AddThemeColorOverride("font_color",new Color(ink));
        label.AddThemeColorOverride("font_outline_color",Colors.Black);
        label.AddThemeConstantOverride("outline_size",1);
        label.AddThemeColorOverride("font_shadow_color",Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x",1);
        label.AddThemeConstantOverride("shadow_offset_y",1);
        return label;
    }
}

/// <summary>
/// The client's whisper thread (fa3cfc3: the room's thread view) in the retail one-line look of the original
/// PM window, restyled in place because the thread keeps its rows for reaction and voice-note refreshes. Own
/// lines are white and right-aligned, the partner's cyan, notices yellow, without bubbles or time stamps (the
/// time is the line's tooltip). The react button is a title-bar icon, reactions sit on a small chip with the
/// gold line, a voice note is a play cell, its waveform and its clock on one line, and day markers are chips.
/// </summary>
public static class ClassicWhisperThread
{
    public const string RowMeta = "classic_whisper_row";
    public static Color Mine => new("ffffff");
    public static Color Partner => new("80ffff");
    public static Color Notice => new("ffff00");
    public static Color ActionInk => new("00ffff");

    /// <summary>Restyles the rows the thread shows now and every row it adds later (history, new lines, a size change).</summary>
    public static void Attach(WhisperParts parts,float width)
    {
        var thread=parts.Thread;
        if(thread.GetChildCount()>0 && thread.GetChild(0) is MarginContainer margin)
            foreach(var side in new[]{"left","top","right","bottom"}) margin.AddThemeConstantOverride("margin_"+side,0);
        if(parts.Log.GetParent() is VBoxContainer content) content.AddThemeConstantOverride("separation",0);
        parts.Log.AddThemeConstantOverride("separation",0);
        parts.Older.Name="whisper_older";
        ClassicSocialStyle.Button(parts.Older);
        var pending=new List<Control>();
        void Flush()
        {
            if(!Plugin.Active || !GodotObject.IsInstanceValid(thread)) { pending.Clear(); return; }
            var posts=NativeWhispers.Posts(thread);
            foreach(var row in pending)
                if(GodotObject.IsInstanceValid(row) && !row.IsQueuedForDeletion()) Row(row,posts,width);
            pending.Clear();
        }
        pending.AddRange(parts.Log.GetChildren().OfType<Control>());
        Flush();
        // A row arrives complete; it is restyled once the client has finished adding the batch.
        parts.Log.ChildEnteredTree+=node=> {
            if(node is not Control row) return;
            if(pending.Count==0) Callable.From(Flush).CallDeferred();
            pending.Add(row);
        };
    }

    private static void Row(Control row,Dictionary<Node,LibreKO.Domain.RoomPost> posts,float width)
    {
        if(row.HasMeta(RowMeta) || row.GetChildCount()==0) return;
        switch(row)
        {
            case CenterContainer notice: NoticeRow(notice,width); break;
            case MarginContainer day when day.GetChild(0) is CenterContainer: DayRow(day); break;
            case MarginContainer post when post.GetChild(0) is HBoxContainer line: PostRow(post,line,posts.GetValueOrDefault(post),width); break;
        }
    }

    private static void PostRow(MarginContainer row,HBoxContainer line,LibreKO.Domain.RoomPost? post,float width)
    {
        bool mine=line.Alignment==BoxContainer.AlignmentMode.End;
        var ink=mine ? Mine:Partner;
        var stack=line.GetChildren().OfType<VBoxContainer>().FirstOrDefault();
        var bubble=stack?.GetChildren().OfType<PanelContainer>().FirstOrDefault();
        if(stack==null || bubble==null || bubble.GetChildCount()==0 || bubble.GetChild(0) is not VBoxContainer column) return;
        // Group starts keep no extra gap: the retail window shows one line under the other.
        row.AddThemeConstantOverride("margin_top",0);
        line.AddThemeConstantOverride("separation",(int)ClassicWhisperLayout.LineGap);
        stack.AddThemeConstantOverride("separation",0);
        column.AddThemeConstantOverride("separation",0);
        bubble.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
        bubble.Name=mine ? "whisper_line_mine":"whisper_line_partner";
        foreach(var slot in line.GetChildren().OfType<CenterContainer>()) ReactSlot(slot);
        foreach(var holder in stack.GetChildren().OfType<MarginContainer>()) Pill(holder,mine);
        if(column.GetChildren().OfType<VBoxContainer>().FirstOrDefault() is { } note)
        {
            row.SetMeta(RowMeta,"note");
            Note(note,ink);
            return;
        }
        row.SetMeta(RowMeta,"line");
        // The time stamp (and a sender name, which whispers do not show) leave the line.
        foreach(var label in column.GetChildren().OfType<Label>()) label.Visible=false;
        // The line keeps the client's side and fits its text, so the react button stays beside it; a line wider than
        // the log wraps at the log width (the client wraps at its bubble width).
        float room=width-ClassicWhisperLayout.ReactCell-ClassicWhisperLayout.LineGap;
        foreach(var text in column.GetChildren().OfType<RichTextLabel>())
        {
            text.Name="whisper_line_text";
            Font(text);
            text.HorizontalAlignment=mine ? HorizontalAlignment.Right:HorizontalAlignment.Left;
            if(post!=null)
            {
                text.Text=NativeWhispers.Render(post.Text,ink.ToHtml(false));
                text.TooltipText=post.Time.ToString("HH:mm");
            }
            string plain=post!=null ? NativeWhispers.Plain(post.Text):text.GetParsedText();
            bool wraps=Plugin.Kit.ChatStrong.GetStringSize(plain,HorizontalAlignment.Left,-1,ClassicWhisperLayout.LineSize).X+2>room;
            text.AutowrapMode=wraps ? TextServer.AutowrapMode.WordSmart:TextServer.AutowrapMode.Off;
            text.CustomMinimumSize=wraps ? new Vector2(room,0):Vector2.Zero;
        }
    }

    /// <summary>A voice note on one line: the play cell, the waveform in the line's ink and the clock.</summary>
    private static void Note(VBoxContainer note,Color ink)
    {
        var rows=note.GetChildren().OfType<HBoxContainer>().ToArray();
        if(rows.Length<2) return;
        var (line,foot)=(rows[0],rows[1]);
        note.CustomMinimumSize=new Vector2(ClassicWhisperLayout.NoteWidth,0);
        note.AddThemeConstantOverride("separation",0);
        line.AddThemeConstantOverride("separation",ClassicWhisperLayout.NoteGap);
        foreach(var play in line.GetChildren().OfType<Button>())
        {
            ClassicChatRoomSkin.Cell(play);
            play.Name="whisper_note_play";
            play.CustomMinimumSize=new Vector2(ClassicWhisperLayout.PlayCell,ClassicWhisperLayout.PlayCell);
            play.AddThemeConstantOverride("icon_max_width",(int)ClassicWhisperLayout.PlayIcon);
            play.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
        }
        foreach(var wave in line.GetChildren().OfType<VoiceWaveformView>())
        {
            wave.Name="whisper_note_wave";
            wave.Played=ink;
            wave.Rest=new Color(ink,.35f);
            wave.Dot=ink;
            wave.CustomMinimumSize=new Vector2(0,ClassicWhisperLayout.WaveHeight);
            wave.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
            wave.QueueRedraw();
        }
        // The clock joins the line; the foot keeps only a spacer and the time stamp, which the line drops.
        if(foot.GetChildren().OfType<Label>().FirstOrDefault(l=>l.SizeFlagsHorizontal.HasFlag(Control.SizeFlags.Expand)) is { } clock)
        {
            clock.Reparent(line,keepGlobalTransform:false);
            clock.Name="whisper_note_clock";
            clock.SizeFlagsHorizontal=Control.SizeFlags.ShrinkEnd;
            clock.SizeFlagsVertical=Control.SizeFlags.ShrinkCenter;
            Font(clock,ink,ClassicWhisperLayout.SmallSize);
        }
        foot.Visible=false;
    }

    /// <summary>The react button shows on the hovered line like a title-bar icon, in a cell the height of a line.</summary>
    private static void ReactSlot(CenterContainer slot)
    {
        slot.CustomMinimumSize=new Vector2(ClassicWhisperLayout.ReactCell,ClassicWhisperLayout.LinePitch);
        slot.SizeFlagsVertical=Control.SizeFlags.ShrinkBegin;
        foreach(var react in slot.GetChildren().OfType<Button>())
        {
            react.Name="whisper_react";
            react.CustomMinimumSize=new Vector2(ClassicWhisperLayout.ReactCell,ClassicWhisperLayout.ReactCell);
            react.AddThemeConstantOverride("icon_max_width",(int)ClassicWhisperLayout.ReactIcon);
            // The client's react button is flat, which draws no state box; the title-bar hover needs it drawn.
            react.Flat=false;
            ClassicWhisperSkin.TitleIcon(react);
            react.AddThemeColorOverride("icon_normal_color",ActionInk);
            react.AddThemeColorOverride("icon_pressed_color",Colors.White);
        }
    }

    /// <summary>The reactions under a line on a small Classic chip, on the line's side.</summary>
    private static void Pill(MarginContainer holder,bool mine)
    {
        holder.AddThemeConstantOverride("margin_top",0);
        holder.AddThemeConstantOverride("margin_left",0);
        if(holder.GetChildCount()==0 || holder.GetChild(0) is not HBoxContainer align) return;
        align.Alignment=mine ? BoxContainer.AlignmentMode.End:BoxContainer.AlignmentMode.Begin;
        foreach(var pill in align.GetChildren().OfType<PanelContainer>())
        {
            pill.Name="whisper_reactions";
            pill.AddThemeStyleboxOverride("panel",Chip(false));
            // The client swaps the pill's style on hover; the Classic chip follows (connected after the client's).
            pill.MouseEntered+=()=> { if(Plugin.Active && GodotObject.IsInstanceValid(pill)) pill.AddThemeStyleboxOverride("panel",Chip(true)); };
            pill.MouseExited+=()=> { if(Plugin.Active && GodotObject.IsInstanceValid(pill)) pill.AddThemeStyleboxOverride("panel",Chip(false)); };
            foreach(var count in pill.GetChildren().OfType<HBoxContainer>().SelectMany(l=>l.GetChildren().OfType<Label>()))
            {
                count.Name="whisper_reaction_count";
                ClassicSocialStyle.Font(count,true,ClassicWhisperLayout.SmallSize);
                count.AddThemeColorOverride("font_color",ClassicDesign.Text);
            }
        }
    }

    private static void DayRow(MarginContainer row)
    {
        row.SetMeta(RowMeta,"day");
        row.AddThemeConstantOverride("margin_top",ClassicWhisperLayout.DayChipMargin);
        row.AddThemeConstantOverride("margin_bottom",ClassicWhisperLayout.DayChipMargin);
        foreach(var chip in ClassicSocialStyle.Descendants(row).OfType<PanelContainer>())
        {
            chip.Name="whisper_day";
            chip.AddThemeStyleboxOverride("panel",Chip(false));
            chip.CustomMinimumSize=new Vector2(0,ClassicWhisperLayout.DayChipHeight);
            foreach(var label in chip.GetChildren().OfType<Label>())
            {
                ClassicSocialStyle.Font(label,true,ClassicWhisperLayout.SmallSize);
                label.AddThemeColorOverride("font_color",ClassicDesign.Muted);
            }
        }
    }

    /// <summary>A notice is a yellow line like the retail window's system lines; web links stay clickable.</summary>
    private static void NoticeRow(CenterContainer row,float width)
    {
        row.SetMeta(RowMeta,"notice");
        foreach(var spaced in row.GetChildren().OfType<MarginContainer>())
        {
            spaced.AddThemeConstantOverride("margin_top",0);
            spaced.AddThemeConstantOverride("margin_bottom",0);
        }
        foreach(var chip in ClassicSocialStyle.Descendants(row).OfType<PanelContainer>())
        {
            chip.Name="whisper_notice";
            chip.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
            foreach(var label in chip.GetChildren().OfType<Label>())
            {
                Font(label,Notice,ClassicWhisperLayout.LineSize);
                label.AddThemeColorOverride("font_shadow_color",Colors.Black);
                label.AddThemeConstantOverride("shadow_offset_x",1);
                label.AddThemeConstantOverride("shadow_offset_y",1);
                label.HorizontalAlignment=HorizontalAlignment.Left;
                label.AutowrapMode=TextServer.AutowrapMode.WordSmart;
                label.CustomMinimumSize=new Vector2(width,0);
            }
            foreach(var rich in chip.GetChildren().OfType<RichTextLabel>())
            {
                Font(rich);
                var linked=Regex.Match(rich.Text,@"^\[center\]\[color=#[0-9a-fA-F]+\](.*)\[/color\]\[/center\]$",RegexOptions.Singleline);
                if(linked.Success) rich.Text=$"[color=#{Notice.ToHtml(false)}]{linked.Groups[1].Value}[/color]";
                rich.HorizontalAlignment=HorizontalAlignment.Left;
                rich.AutowrapMode=TextServer.AutowrapMode.WordSmart;
                rich.CustomMinimumSize=new Vector2(width,0);
            }
        }
    }

    public static StyleBoxFlat Chip(bool hover)
    {
        var box=new StyleBoxFlat { BgColor=hover ? new Color("2a2418"):new Color(0,0,0,.85f),BorderColor=hover ? ClassicDarkFrame.Heading:ClassicDarkFrame.Line };
        box.SetBorderWidthAll(1);
        box.ContentMarginLeft=3;
        box.ContentMarginRight=4;
        box.ContentMarginTop=box.ContentMarginBottom=0;
        return box;
    }

    private static void Font(RichTextLabel text)
    {
        text.AddThemeFontOverride("normal_font",Plugin.Kit.ChatStrong);
        text.AddThemeFontSizeOverride("normal_font_size",ClassicWhisperLayout.LineSize);
        text.AddThemeColorOverride("font_outline_color",Colors.Black);
        text.AddThemeConstantOverride("outline_size",1);
        text.AddThemeColorOverride("font_shadow_color",Colors.Black);
        text.AddThemeConstantOverride("shadow_offset_x",1);
        text.AddThemeConstantOverride("shadow_offset_y",1);
    }

    private static void Font(Label label,Color ink,int size)
    {
        label.AddThemeFontOverride("font",Plugin.Kit.ChatStrong);
        label.AddThemeFontSizeOverride("font_size",size);
        label.AddThemeColorOverride("font_color",ink);
        label.AddThemeColorOverride("font_outline_color",Colors.Black);
        label.AddThemeConstantOverride("outline_size",1);
    }
}

public partial class ClassicWhisperAttention : Node
{
    private readonly TextureButton _bar;
    private readonly Label _title;
    private readonly Texture2D? _normal;
    private readonly Texture2D? _highlight;
    private bool _unread;
    private bool _lit;
    private double _elapsed;

    public ClassicWhisperAttention(TextureButton bar,Label title)
    {
        _bar=bar;
        _title=title;
        _normal=bar.TextureNormal;
        _highlight=bar.TextureHover ?? _normal;
    }

    public override void _Ready() => SetProcess(_unread);

    public void SetUnread(bool unread)
    {
        if(_unread==unread) return;
        _unread=unread;
        _elapsed=0;
        _lit=unread;
        SetProcess(unread);
        Refresh();
    }

    public override void _Process(double delta)
    {
        _elapsed+=delta;
        if(_elapsed<0.5) return;
        _elapsed%=0.5;
        _lit=!_lit;
        Refresh();
    }

    private void Refresh()
    {
        _bar.TextureNormal=_lit ? _highlight:_normal;
        _title.AddThemeColorOverride("font_color",new Color(_lit ? "ffff00":"00ffff"));
    }
}

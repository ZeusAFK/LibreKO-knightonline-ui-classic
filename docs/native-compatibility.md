# Native compatibility

The Classic UI runs on the unmodified [ZeusAFK/LibreKO](https://github.com/ZeusAFK/LibreKO) client. Everything the
former fork added to the client for this plugin (native control names, state metas, character/chat/HUD bridges,
Classic-only behaviour and the Classic preview fixtures) is recreated in the plugin's compatibility layer under
`src/Native`. Client and server correctness fixes that cannot live in a plugin are proposed upstream as 23 separate
branches. The plugin does not require them. On bare upstream it runs with upstream's existing bugs. Where a fix
adds something the plugin can use, the plugin detects it at runtime.

This page covers:

1. how the layer works;
2. what works on bare upstream and what each fix branch adds;
3. every client member the plugin reaches by reflection, as a checklist for upstream renames;
4. known differences from the former fork build;
5. optional client hooks that would remove reflection;
6. how to verify the plugin after an upstream update.

## 1. How the compatibility layer works

- **Validated reflection (`Native.cs`).** Reflection on non-public client members exists only in `src/Native`, and
  nearly all of it goes through `Native`: `Get`, `TryGet`, `Set`, `Call`, `TryCall`, `Subscribe`, `Unsubscribe`,
  `Has`, `HasMethod` and `ClientType`. Lookups walk base types and are cached. When a member is missing, `Native`
  writes `native member unavailable: <Type>.<member>` once per member, adds it to `Native.MissingMembers` and
  raises `Native.MemberMissing`. Then it returns a default value instead of throwing. A member that exists but no
  longer has the type `Get`/`TryGet` read it as (a container that became another container type, a field that
  changed width) is reported the same way, once, as `native member type changed: <Type>.<member> is <Actual>, read
  as <Requested>`; a null value of a compatible type is not a mismatch. `Has`, `HasMethod` and
  `TryCall` do not report, so they are used for feature detection. Two cases run outside `Native`:
  - The anvil binds the client's private `OnUpgradeOpen` with `Delegate.CreateDelegate`.
  - Nested records such as `World+BagCompanion` and `World+VipWhPending` are created with `Activator`.
- **Window preparers (`NativeWindows`).** Each window group registers preparers per window id
  (`NativeWindows.Prepare`) or per pattern (`PrepareMatching`, for example the per-player whisper windows).
  - `NativeSetup.Register` runs from `Plugin.Initialize` before any skin registers. Each group adds its own
    `Register*` partial method in its own `Native*.cs` or `Familiar*.cs` file.
  - Preparers and skins both defer to the end of the frame that built the window, so the preparer always runs first.
  - A preparer names the native controls a skin looks for. It mirrors native state into the metas the skin reads.
    It builds the additions the skin expects (embedded bags, confirmation layers, extra buttons). Last, it sets the
    skin's gate meta, for example `classic_mail_controls`.
  - If a required member is missing, the preparer returns without setting the gate. The skin then leaves the window
    as the client built it. The NPC service skins (`ClassicServiceSkin`) apply the generic Classic frame
    (`ClassicSkin.Apply`) instead. A rename upstream therefore costs one window its Classic composition. It does
    not break the game.
- **`NativeWindows.Sync(window, action)`** adds a `native_sync` node. The node runs the action every frame while the
  window is visible. It uses the lowest process priority, so it runs before the skin draws. It mirrors busy, pending,
  selected and page state, and repairs text the client rewrites.
- **`NativeWindows.OnDescendantAdded(root, action)`** listens to `SceneTree.NodeAdded`. It describes rows, cards and
  cells the client adds later (mail rows, store cards, trade rows, recipe rows, merchant signs, whisper rows). It runs
  before the skin's deferred styling.
- **HUD and layout.** `NativeHud` hides or extends HUD elements that have no public `HudPart` (the mail,
  achievements, attendance and Power-Up Store icons, and the familiar bar). It also carries the whisper styler.
  `NativeLayout` recreates three things the fork had: the legacy resize grip, chat/Info seam snapping and
  `content_open_anchor`. `NativeUi` provides the Classic count badge (`CountBadge`), ungrouped money digits
  (`GroupDigits`, through `PlainDigits`) and the per-window attention styler.
- **Game services (`NativeGame.cs`).** `INativeGame` holds the services the skins need beyond the public plugin API:
  - character panel and NPC portrait;
  - chat history, item links and nearby players;
  - hotbar selection, run/sit/attack commands and minimap visibility;
  - inventory `MoveAmount`, `TransferToInventorySlot` and `ConfirmDrop`.

  C# 14 extension members put these services on `IGameWindows`, `IGameChat`, `IGameHotbar`, `IGameCommands`,
  `IGameMap` and `IGameInventory`, so skin code reads like the old fork API. `ClientGame` is the runtime source.
  Each member falls back to what the public API already offers when native support is absent. For example,
  `MoveAmount` moves the whole stack when the client has no `MoveBetween(int,int,int)`. The preview replaces
  `NativeGame.Source` with fixture data.
- **Live enable and disable (`NativeHooks`, `NativeSetup.Unregister`).** Upstream switches UI themes from Settings >
  Plugins without a restart. On disable the client calls `Plugin.Shutdown`, removes every `context.Ui` registration
  and its leftover `PluginGame` handlers, and rebuilds the World in place. The plugin assembly stays loaded, and a
  re-enable creates a new `Plugin` instance, so static state survives. The plugin therefore records every hook it
  attaches to an object it does not own in `NativeHooks`:
  - scene-tree watchers (`NativeHooks.WatchTree`: the window, content-anchor, familiar-bar and merchant-sign
    watchers and every `OnDescendantAdded`);
  - handlers on `Net` events (`NativeHooks.Listen`), including every `Native.Subscribe`;
  - handlers that replace one of the World's own handlers (`NativeHooks.Replace`: anvil open, nation transfer open)
    or run ahead of it (the prepended mail-list and gold handlers);
  - the `Game.Windows.Source` wrapper.

  Each hook is released once, by its caller, when its owner (normally the World) leaves the tree, or on shutdown;
  on shutdown a replaced World handler is restored while that World is still in the tree. `Plugin.Shutdown` then
  calls `NativeSetup.Unregister`: it releases all hooks, removes the plugin delegates still attached to the
  client's game API (the Classic HUD and windows of the World that is about to be rebuilt), sweeps `Net` and the
  tree's `node_added` for unrecorded plugin handlers (a warning names them; the audit expects none), and clears the
  static registries (`NativeWindows`, `NativeHud`, `NativeOverlays`, `NativeLayout`, `FamiliarBar`,
  `NativeGame.Source`, `NativeChat`, `NativeQuestLog`, `NativeCommunication`, `NativeHudChat`, `Native` reports and
  `MemberMissing` listeners). It also frees plugin nodes outside the World (the developer check, dialogs on the
  root), the NPC portrait cache, the straight-art cache, the character page router and the cached NPC heading font,
  and resets `Plugin.Kit`. `Plugin.Active` guards code that can run after shutdown (deferred preparers, `_ExitTree`
  of Classic nodes freed by the rebuild). `plugin-toggle-audit` drives the host path on a full native World:
  native start, enable, disable, re-enable, disable, re-enable.
- **Preview fixtures (`preview/Fixtures`).** The fork's `World.UiPreview.*Classic*` partials are rebuilt in the
  preview project through `Native`: `PreviewFixtures` plus one partial file per group. The preview's `PluginUi` runs
  no window extenders, so fixtures and audits call the group preparer (`NativeServices.Prepare`,
  `NativeTrade.Prepare` and so on) before they apply a skin. Fixtures that stand in for a server reply call the
  client handler first and then raise the `Net` event, in the same order as the game.

## 2. Feature support

"Bare upstream" means `upstream/main` with no fix branch. `classic-integration` is upstream plus all 23 branches.
On `classic-integration`, behaviour and rendering match the former fork build except for the differences in
section 4.

### Per window group

| Group | Works on bare upstream | Needs a fix branch |
| --- | --- | --- |
| HUD, chat, whispers (`NativeHudChat`, `NativeChat`, `NativeLayout`, `NativeHud`) | Classic chat and Info, history format, timestamps and custom colours, private-message exclusion, `ChatSystem.Info` and server notices as yellow System lines, bubble colours, Shift-click item links, nearby players, whisper styling and compose target, chat colours editor and palette, legacy resize grip, chat/Info seam snapping, hidden HUD icons, taskbar service toggles, notification count, hotbar selection, command state, gold income/expense lines in Info. | `fix/chat-whisper-state`: an existing whisper window comes to the front when it is reopened or receives a whisper, and a click focuses the top-most window. On bare upstream the window can stay behind another conversation, and a click can focus the window underneath. `fix/escape-order`: Escape closes chat colours, the mail reader, mail compose (hidden with its draft kept, as with its X; only Cancel clears it) and nine more windows before it opens the game menu, and it closes the shop after its amount prompt. |
| Character and NPC (`NativeCharacterPanel`, `NativeCharacterNpc`, `NativeNpc`, `NativeQuestLog`, `NativeParty`, `NativeSkills`, `NativeEvents`) | Character Report, Quest, Clan and Friend pages through `CharacterPanelBridge`; Knights Management; NPC dialog portraits and quest captions; Quest Details reward rows; preset label wording; party roster, click-to-target and top-right docking; seek-party board; skill weapon requirement and mastery trees; temple-event leave confirmation. | `fix/quest-rewards`: the "Selected reward" and "Received rewards" states in Quest Details. On bare upstream `_questRewardSelection`, `_questRewardOptionTitle` and `_questRewardOptionBox` are reported missing once, and the rows always list the reward options. `fix/stat-presets`: preset totals and validation from the class base. The plugin's "Allocated X / Y    Remaining Z" text works either way. |
| Inventory and storage (`NativeInventory`, `NativeStorage`, `VipVaultPinPrompt`) | Classic inventory; identity-checked destroy; magic-bag right-click target; warehouse with 24-cell pages and amount prompt; VIP and clan vault pages; in-vault rearrangement, which the plugin sends itself; Classic VIP PIN prompt; clan coin prompt. | `feat/magic-bag-amount`: part of a stack between bag and magic bag. On bare upstream a partial amount moves the whole stack. `fix/storage-transfers`: VIP and clan quantity prompts with merge-aware destinations (`VipWhPending`/`ClanWhPending` `Count`/`Merge`), merging into existing stacks, `StorageTransferBusy`, and stale-reply guards. On bare upstream vault transfers are whole-stack with no prompt, and the busy state comes from the in-flight fields. |
| Vendor, trade, merchant (`NativeVendor`, `NativeTrade`, `NativeMerchant`) | Vendor buy/sell confirmations; compact 24-cell catalogue pages; drag payload; count badges; gold log; Classic exchange request layer, final approval with offer snapshot, and gold prompt; merchant advert step; amount prompt metas; release activation; drop onto a listing slot; wanted-sale gold cap; Silver/Copper signs. | `fix/vendor-exchange-validation`: `SellSlot(abs, count, itemId)` re-checks the item (bare upstream falls back to the 2-argument `SellSlot`); untradable items are refused through `ItemSlot.IsTradable` (skipped on bare upstream); `_exConfirmedByPartner` locks a confirmed partner's offer (on bare upstream `ex_partner_locked` stays false and one member is reported missing). `fix/merchant-stack-sync`: stall purchases keep both inventories in step with the server. The plugin does not depend on it. |
| Magic Anvil and NPC services (`NativeAnvil`, `AnvilPlacementRules`, `NativeServices`) | Anvil selection window, placement rules, embedded 28-cell bag, Cancel/Back, busy, compound, error and scan metas, count badges, the `IsTradable` rule (mirrored from `Flag`/`UniqueId`); warp, seal, Chaotic Generator (28-cell bag and drop target), item combination quantity, recipe book, redistribution, Classic repair mode. | `fix/anvil-session`: `UpgradeInteractionLocked`, the preview gate, the confirmation snapshot and the retained-item result, plus a server that echoes the requested upgrade type on refusal. On bare upstream the bench lock falls back to `_upgradeSession`, the pending result and the scan tween, and upstream's own anvil bugs remain. `fix/npc-service-requests`: repair applies to the confirmed item and plays its sound only after the acknowledgement; the seal uses a `Notice.Confirm` confirmation with an in-flight guard and identity checks; the redistribution request (`_reset`). On bare upstream redistribution reads `_resetKind`, and seal and repair keep upstream's flow. |
| Market and communication (`NativeMarket`, `NativeCommunication`) | Mail inbox, reader and compose; messenger and chat rooms; Market Price with the Classic chart background; merchant search; Power-Up Store with compact-screen and touch layout; Akara's Altar. The plugin also works around two upstream bugs: the chat-room log hang at the 101st line, and empty states that never return. It also repairs the cached Market Price "no history" reply every frame. | `fix/auction-replies`: bid and retract replies apply to the submitted lot; busy locking; tab pressed state kept in sync; `_auctionPlaceBid`, `_auctionRefresh`, `_auctionRowButtons`. On bare upstream Place Bid and Refresh are found by structure, and `auction_row_action` goes on every row button. `fix/mail-claims`: claims fill existing stacks, the unread badge follows reads, and compose cannot send twice. `fix/chat-room-lists` and `fix/market-price-no-history` fix the two bugs in the client and make the plugin's workarounds redundant but harmless. |
| Identity and appearance (`NativeIdentity`, `NativeEquipView`, `NativeLook`, `NativeAppearance`, `NativeRebirth`, `NativeCape`) | Name change with Cancel, clan creation names, transformation, council tax rate, equipment inspection rebuild, gender change (Classic editor and preview framing), rebirth (Total column, plugin-owned confirmation), cape service (preview, pages, cost confirmation). | `fix/beauty-shop`: the Classic Beauty Shop needs `_changeHairApply` and `_changeHairInFlight`; without them the preparer stops and the client's own window stays. The branch also adds the server's in-game Beauty Shop handler, `LookEditor.SetLocked` (bare upstream disables the editor buttons instead) and `_faceSteps`/`_hairSteps` (bare upstream finds the steppers by structure). `fix/nation-transfer`: the Classic Confirm calls `World.SendNationTransfer(int, IReadOnlyList<NationTransferPick>)` and uses `_transferRevision`, `_transferNotice`, `_transferCompleted`, `SetTransferLocked` and `CancelTransferConfirmation`, which only this branch has. On bare upstream the plugin does not take over the transfer window, so the client's own window handles the transfer. The server side saves the transfer in one step. `fix/clan-requests`: cape replies are matched to their request; a cape reply for another clan raises `CapeResetEvent` (upstream 7ef0f8e): the picker unlocks the same draft and shows text 16810, while a disconnect, recognised by `PetResetEvent`/`RebirthResetEvent` following in the same reset, closes the picker; the clan fee confirmation (`_clanCreateNotice`, reworded by the plugin) is shown, while bare upstream creates the clan without it. `fix/rebirth-requests`, `fix/beauty-shop`, `fix/clan-requests`: `Send*` methods return `bool`, so a refused send is detected. On bare upstream they return `void`, and a call counts as sent. |
| Familiar (`NativePet`, `FamiliarWindow`, `FamiliarTrainer`, `FamiliarBar`, `FamiliarSkillButton`, `FamiliarPortraitView`) | Familiar window (details, 2×8 skills, 3D portrait), trainer (hatching and transformation with confirmation and keyboard handling), familiar bar (names, grip, default position, slot frames, tooltips). | `fix/familiar-replies`: replies are matched to the request and never applied twice; `SendPetHatch`/`SendPetTransform` return `bool` (on bare upstream a live connection counts as sent); reset on disconnect. |

The nation-transfer behaviour on bare upstream follows from the code paths above. No audit runs on bare upstream.

### Branch index

| Branch | Plugin relationship |
| --- | --- |
| `feat/magic-bag-amount` | Feature-detected: `MoveBetween(int,int,int)`, `IGameInventory.MoveAmount`. Without it a partial amount moves the whole stack. |
| `fix/anvil-session` | Feature-detected: `UpgradeInteractionLocked`, `ItemSlot.IsTradable`. Fixes the anvil session and the server's preview refusal type. |
| `fix/auction-replies` | Feature-detected: `_auctionPlaceBid`, `_auctionRefresh`, `_auctionRowButtons`. |
| `fix/beauty-shop` | Required for the Classic Beauty Shop. Optional for the editor lock and stepper names. |
| `fix/chat-room-lists` | Makes the plugin's chat-room and messenger workaround redundant. |
| `fix/chat-whisper-state` | Behaviour only: whisper raise and top-most click focus. |
| `fix/clan-requests` | Feature-detected: `CapeResetEvent`, `_clanCreateNotice`, `bool SendCapeBuy`. |
| `fix/escape-order` | Behaviour only: Escape entries for 12 windows and the shop/amount-prompt order. |
| `fix/familiar-replies` | Feature-detected: `bool SendPetHatch`/`SendPetTransform`. |
| `fix/inventory-move-snapshots` | Core correctness. No plugin interaction. |
| `fix/magic-bag-stacks` | Core correctness. The plugin keeps its own magic-bag target rule. |
| `fix/mail-claims` | Behaviour only. |
| `fix/market-price-no-history` | Makes the plugin's per-frame no-history repair redundant. |
| `fix/merchant-stack-sync` | Behaviour only. |
| `fix/nation-transfer` | Required for the Classic nation transfer window; without it the client's own window is used. |
| `fix/npc-service-requests` | Feature-detected: `_reset`. Behaviour for repair and seal. |
| `fix/preview-effects` | Weapon glow and emitters in character previews. Not reflected. |
| `fix/quest-rewards` | Feature-detected: `_questRewardSelection`, `_questRewardOptionTitle`, `_questRewardOptionBox`. |
| `fix/rebirth-requests` | Feature-detected: `bool SendRebirthStatChange`. Server validation. |
| `fix/stat-presets` | Behaviour only. |
| `fix/storage-transfers` | Feature-detected: `VipWhPending`/`ClanWhPending` `Count` and `Merge`, `StorageTransferBusy`. |
| `fix/transport-generations` | Core correctness. A reply decoded before a connection loss is now applied. |
| `fix/vendor-exchange-validation` | Feature-detected: `SellSlot(int,int,int)`, `ItemSlot.IsTradable`, `_exConfirmedByPartner`. |

**Build-time binding.** `InventoryWindow` calls `_game.Inventory.MoveAmount(...)`. Against a client that has
`IGameInventory.MoveAmount` (`feat/magic-bag-amount`), that call compiles to the interface member. Against bare
upstream it compiles to the plugin's extension. Install a build made against the same kind of client it will run on.

## 3. Reflected client members

These tables are the maintenance checklist. Every name was checked against the plugin source as a string literal
under `src/Native`, and against the `classic-integration` client source. A *(branch)* note marks a member that
exists only with that fix branch. The plugin feature-detects it or degrades as described in section 2. "Kind" uses:
F field, P property, M method, E event or delegate backing field, T nested type. `World` is the owner unless a row
says otherwise. Public API used directly, such as `Net.I` events, `UiTheme`, `ItemData` and `HudWindow`, is not
listed. Preview-only members are listed at the end.

### Shared

| Member | Kind | Used for |
| --- | --- | --- |
| `PluginGame._windows`, `PluginGame._inventory`, proxy `Source`, `PluginGameBridge._w` | F/P | Find the World behind the plugin services. Install the `NativeServiceWindows` decorator. |
| `PluginUi.HudHidden` | M (internal) | Chat item-link guard |
| `HudLayout._defaultPosition` (get/set), `ReapplyDefault` | F/M | Content anchors, party docking, familiar bar default position |
| `HudWindow.Closed` | E | Find the World that owns a fixture window (familiar) |
| `ItemSlotView._count`, `_icon`, `RightClicked`, `DoubleClicked` | F/E | Count badge, drag preview, vendor rerouting |
| `QuantityPrompt.Confirm`, `Notice._request` (`DialogRequest`) | F | Prompt wiring, confirmation wording |
| `Inv`, `Sheet`, `Chat`, `_selfDead`, `_ents`, `_myId`, `_selectedId`, `_zone`, `_mainWindows` | F/P | Common state |
| `ShowItemTooltip(int,ItemSlot,string)`, `HideItemTooltip()`, `MoveBetween(int,int)`, `HideMainWindow(string)`, `CombatNotice(string)` | M | Common actions |
| `ResolveMobScene(int)`, static `ForceDoubleSidedOnce(Node,string)`, `ModelNodeName` | M/F | NPC and familiar portrait models |

### HUD, chat, whispers

| Member | Kind | Used for |
| --- | --- | --- |
| `ChatSystem._store`, `PublishedMax`, static `RenderText`, `AddEntry`, `StatusNotice` (set), `PluginTyping`, `IsActive`, `_bubbles` (`Bubble.Label`), `Panel`, `SetChannel`, `Open` | F/P/M | Classic history, System lines, bubble colours, link rule, channel requests |
| `ChatSystem+ChatEntry.Raw` and `Seq`, `Time`, `Category`, `Type`, `Name`, `Nation`, `Gm`, `Text`, `Bbcode` | M/P | Reading and adding entries |
| `_whisperLayer`, `_whispers` (`WhisperChat.Name/Window/Input/Log/Blink`), `_worldReady`, `_hudEditMode`, `_self` | F | Whisper preparation, compose target, attention styler |
| `_nearbyListed`, `_nearbyRows`, `_nearbyFirstPoll`, `RebuildNearby()` | F/M | Nearby players |
| `_chatPalette`, `_chatPaletteSlot`, `_chatColorsDraft` | F | Chat colours palette placement and focus |
| `ShowChatItemTip`, `ShowPlayerMenuByName` | M | Link tooltip, player menu |
| `_mailIconButton`, `_trophy`, `_trophyBlink`, `_attendanceGift`, `_attendanceGiftBlink`, `_powerUpStoreIcon`, `_genieIcon`, `_eventsIcon`, `_drIcon`, `QueueDockLayout()`, `PlaceTopIcons()` | F/M | Hidden HUD icons (the status bar's letter row opens their windows) |
| `_achClaimablePerTab`, `ClaimableAttendanceCount()`, `_eventsAlert`, `_drBlink` | F/M | Notification count (with public `Net.MailUnread`); events and daily rank give an attention state |
| `_mailShown`/`ToggleMail`, `_achShown`/`ToggleAchievements`, `_attendanceShown`/`ToggleAttendance`, `_pusShown`/`ToggleShoppingMall`, `_fullMapShown`/`ToggleFullMap`, `_genieShown`/`ToggleGenie`/`CloseGenie`, `_eventsPanel`/`ToggleEvents`, `_drWindow`/`OnDailyRankIcon` | F/M | Taskbar and status-bar window toggles |
| `_mainWindows["Character"]` `_dock`/`DockTo`, `_geniePanel` `Layout._defaultPosition`/`ReapplyDefault()`, `_pluginHud` | F/M | The docked Character window and the Genie window's first spot stay clear of the Classic status bar, letter row and minimap |
| `_hotkeys`, `_padHotkeys` (entries for `KeyBinds.Get`/`GetPad(KeyAction.Quests)`, re-applied on `KeyBinds.Changed`) | F | The "Quest Journal" key (F10) opens the Quest list like Q; without them it opens Quest Details as before |
| `_hotSelected`, `_hotbar`, `SelectHotAbs(int)` | F/M | Hotbar selection |
| `_running`, `_selfSitting`, `_autoAttack`, `ToggleRunMode()`, `ToggleAutoAttack()`, `StartCameraHalfTurn()`, `ToggleEsc(bool?)` | F/M | Commands |
| `_miniMap` | F | `MiniMapVisible` |
| `_clanBonusChip`, `_clanBonusMarks` (3277df5) | F | Clan online-bonus chip named `clan_bonus_chip` and skinned as a Classic plaque ("Clan" and the O/X marks); hidden while the 2.x top-right group shows its clan bonus bar (`NativeQuestHud`, `NativePremium.HideClientChips`) |
| Public: `IGameTarget.QuestDrops` (`GameTargetDrop`) | API | Quest-item rows under the Classic target frame (`TargetFrame`); replaces nothing (the native `_targetDropRows` sit in the replaced `HudPart.TargetFrame`) |
| Public: `IGameMap.NpcKind`, `NpcKindPresent`, `PickNpcKind`, `MiniMap.Blip.Diamond`, `NpcFinder.KindCaptions` | API | Classic NPC finder over the status minimap (`ClassicNpcList`) and its gold diamonds above the frame art (`MapCanvas.Markers`) |
| Public: `GameLogLine.Kind` | API | Info log tabs All/Status/Item/System (`LogWindow`), the client's `TabbedLog` tabs |
| Public: `PremiumNames.Name`, `PremiumText`/`PremiumFallback`, `Net.ClanOnlineCount`, `Net.NoClanBonus`, `ClanBonusCard` | API | Premium names from the text table in the approved title case (`NativePremium.ClassicCase`, "Gold Premium: 2d 4h" on the plaque, time and benefits in its tooltip/box); clan bonus marks and text (`NativePremium.BonusOf/BonusLines`) |
| `HudLayout._corner`, `_resizing`, `_dragging`, `_dragMoved`, `_resizeOriginMouse`, `_resizeOriginPosition`, `_resizeOriginSize`, `Frozen`, `MinimumSize`, `Place(Vector2,Vector2)` | F/P/M | Legacy grip, seam snapping |

### Character and NPC

| Member | Kind | Used for |
| --- | --- | --- |
| `_selfRace`, `_selfClass`, `_stTitleBtn`, `_characterPages`, `_clanContent`, `_mainLayer`, `_escapeStack`, static `CharacterPageWidth`, `World+CharacterPage`, `World+ClanTab` | F/T | Character panel bridge, clan details composition |
| `ShowCharacterPage`, `ShowClanTab`, `ToggleTitlePicker`, `TogglePreset`, `ToggleClanPoints`, `RequestUserInformation`, `SyncMainWindowState` | M | Page actions |
| `_clanInviteAsk`, `_clanDisbandAsk`, `_clanLeaveAsk`, `_clanRemoveAsk`, `_clanAllianceAsk`, `_clanConfirmAsk`, `_clanMemberMenu`, `_clanStatus`, `_clanMembers`, `_allianceNotice`, `_allianceClans`, `_ctxMember` | F | Clan page rows, status and dialogs |
| `OnLeaveClan`, `AskClanConfirm`, `OnAllianceButton`, `OnMemberMenuAction`, `ApplyMyClan`, `EnsureClanLoaded`, `RefreshClanTab`, static `StatusName` | M | Clan actions |
| `_friendStatus`, `_friends`, `_friendAddInput`, `EnsureFriendsLoaded`, `DoFriendAdd`, `OpenWhisperWith`, `InvitePlayerToParty`, static `FriendDetailLine` | F/M | Friend page |
| `_questFilter`, `_questKind`, `_questViews`, `_questTracked`, `_questSelected`, `_questTrackBtn`, `_questAbandonBtn`, `_questCompleteBtn` | F | Quest page |
| `SetQuestFilter`, `SetQuestKind`, `ToggleTrackSelectedQuest`, `AbandonSelectedQuest`, `CompleteSelectedQuest`, `RefreshQuestDetail`, `QuestsInView`, `QuestFacts`, `QuestName`, `QuestJournal`, `QuestStateOf`, static `StateName` | M | Quest page |
| `_questRewardChoice`, `_questRewardBox`, `_questRewardTitle`, `QuestRewards`, `QuestRewardName`, `QuestItemRow` | F/M | Quest Details reward rows, NPC reward choice |
| `_questRewardSelection` (`Received`, `Chosen`), `_questRewardOptionTitle`, `_questRewardOptionBox` *(fix/quest-rewards)* | F/M | Chosen and received reward state |
| `_npcDialogShown`, `_npcPanel`, `_npcTalkId`, `_vendorNpcId`, `_npcQuestContent`, `_questNotifications`, `_questNotificationIndex` | F | NPC dialog portrait, quest captions |
| `Net.WarehouseNpcEvent`, `Net.RepairNpcEvent`, `_npcMenuBox` (+ `_npcPanel`) | E/F | NPC opening windows (`NativeNpcOpening` marks the client's Inn Hostess and Blacksmith choices; `ClassicNpcOpeningChoice` shows them on `co_inn_us` / `co_smith_us`) |
| static `NoWeaponNpcIds`, `WantedClasses`, `AttachWeapons`; `World+Ent` `IsNpc`, `Dead`, `Name`, `Level`, `ModelId`, `NpcType`, `NpcId`, `Gear`, `Body` | F/M/P | NPC portrait factory |
| `_presetStatPointsLbl` | F | Preset label wording |
| `_partyMembersBox`, `_dockOrder`, `Selectable`, `Select` | F/M | Party roster, click-to-target, docking |
| `_seekListBox`, `_seekRegisterBtn`, `_seeking`, `_seekPageLbl`, `_seekTotal`, `_seekPage`, static `SeekZoneName` | F/M | Seek-party board |
| `_inZoneLeaveAsk`, `OnInZoneLeaveConfirmed`, static `IsTempleEventZone` | F/M | Event leave confirmation |

### Inventory and storage

| Member | Kind | Used for |
| --- | --- | --- |
| `MoveBetween(int,int,int)` *(feat/magic-bag-amount)* | M | Partial moves |
| `_moveInFlight`, `_bagPairing`, `BagPairing<>._openedByCompanion`, `_invDelSlot`, `_invDelItemId`, `RefuseItemInUse(int,int)`, `ConfirmDeleteItem()` | F/M | Embedded bag pairing, identity-checked destroy |
| `_warehouse`, `_whCells`, `_whStatus`, `_whAmount`, `_whShown`, `_whMoney`, `_whInFlight` | F | Warehouse |
| `WithdrawSlot(int)`, `DepositSlot(int,int)`, `AskWithdraw(int,int)`, `CanDropOnWarehouse(int,Variant)`, `DropOnWarehouse(int,Variant)` | M | Warehouse transfers |
| `_vipWh`, `_vipWhPage`, `_vipWhPageLbl`, `_vipWhStatus`, `_vipWhExpiryLbl`, `_vipWhExpirySec`, `_vipWhShown`, `_vipWhInFlight`, `_vipWhPending`, `_vipWhPinDlg`, `_vipWhPinPrompt`, `_vipWhPinEdit` | F | VIP vault and PIN prompt |
| `_clanWh`, `_clanWhPage`, `_clanWhPageLbl`, `_clanWhStatus`, `_clanWhShown`, `_clanWhLoaded`, `_clanWhLoading` *(upstream 7ef0f8e, optional)*, `_clanWhMoney`, `_clanWhInFlight`, `_clanWhPending`, `_clanWhGoldInput` | F | Clan warehouse |
| `RefreshVipWarehouse()`, `RefreshClanWarehouse()`, `VipDepositSlot(int)`, `VipWithdrawSlot(int)`, `ClanWhDepositSlot(int)`, `ClanWhWithdrawSlot(int)`, `ClanWhGoldTransfer(bool)`, `SubmitVipPin()` | M | Vault transfers, coins, PIN |
| `World+VipWhPending`, `World+ClanWhPending` (`Op`, `InvAbs`, `VipIdx`/`WhIdx`; `Count`, `Merge` *(fix/storage-transfers)*) | T | Quantity transfers |
| `StorageTransferBusy` *(fix/storage-transfers)* | P | Busy state. Falls back to the in-flight fields. |
| `Net._conn` and its `Send(Packet)` | F/M | In-vault rearrangement (op 4) |

### Vendor, trade, merchant

| Member | Kind | Used for |
| --- | --- | --- |
| `_vendorCells`, `_vendorCellIds`, `_vendorCatalogue` (set), `_vendorCompanion`, `_vendorEntries`, `_vendorFooter`, `_vendorGroup`, `_vendorNpcId`, `_vendorPanel`, `_vendorSelected`, `_vendorShown`, `_vendorBuy`, `_tradePrompt`, `_tradeInFlight`, `_moveQueue`, `LoyaltyShop`, `VendorWallet`, `VendorCurrency`, `Floaters` | F/P | Vendor metas, compact pages, confirmations |
| `BuyAmount`, `BuyRoom`, `CanBuy`, `CarriedWeight`, `InMainBag`, `SelectVendorItem`, `ShowVendorPage`, `PluginLogAdd` | M | Vendor actions, gold log |
| `SellSlot(int,int,int)` *(fix/vendor-exchange-validation)*, otherwise `SellSlot(int,int)` | M | Sell |
| `ItemSlot.IsTradable` *(fix/anvil-session, fix/vendor-exchange-validation)* | P | Untradable refusal |
| `BagCompanion.Take`, `BagCompanion.IntoBag` (init) | P | Rerouting the bag companion through the confirmation |
| `World.ShowItemTooltip(int, ItemSlot, string note = "", bool shopGoods = false)` *(3277df5)* | M | The client's catalogue hover passes `shopGoods: true` itself and shows its "Purchasing Price" line (tariff included, red above the wallet); the Classic catalogue cell only forwards the hover, the plugin's former "Buying Price" line is gone. Sell prices follow `ItemData.SellPrice`, which applies the premium quarter (`ItemData.PremiumSeller`). |
| `_rbNames`, `_rbPrices`, `_rbTimes` (`Label[10]`), `_rbBuy` (`Button[10]`), `_rbScroll`, `_rbEmpty` *(3277df5)* | F | Buy-back window (`repurchase`, `NativeRepurchase`): names the client's rows, captions and Refresh for `ClassicRepurchaseSkin`; Buy keeps the client's confirmation, expired notice and request. The row states (too dear, expired) and the close come from the facade `IGameRepurchase` (`Rows`, `Close`; its `Buy` is not used because it skips the confirmation). Missing members leave the window on the generic Classic frame. |
| `Net.GoldChangeEvent` | E | Gold log (handler prepended) |
| `_exPanel`, `_exMineList`, `_exTheirsList`, `_exBagList`, `_exWaitLayer`, `_exAmountLayer`, `_exAmountSpin`, `_exAmountIcon`, `_exAmountName`, `_exAmountHint`, `_exAmountMax`, `_exAmountSlot`, `_exAmountShown`, `_exAskDialog`, `_exConfirmBtn`, `_exGoldEdit`, `_exStatus`, `_exShown`, `_exRequestPending`, `_exConfirmedByMe`, `_exAddInFlight`, `_exMyOffer`, `_exTheirOffer`, `_exMyGoldOffer`, `_exTheirGoldOffer`, `_exPartnerName`; `ExOfferItem.ItemId/Count/Dura/SourceAbs` | F/P | Exchange names, metas, request layer, final approval, gold prompt |
| `_exConfirmedByPartner` *(fix/vendor-exchange-validation)* | F | Partner lock |
| `AnswerExchangeRequest`, `OnExchangeConfirm`, `ConfirmExchangeAmount`, `CloseExchangeAmount`, `OnAddGold`, `SetExStatus` | M | Exchange actions |
| `_mctLayer`, `_amountLayer`, `_amountAccept`, `_amountConfirmBtn`, `_amountCount`, `_amountCountRow`, `_amountPrice`, `_amountPriceFixed`, `_amountMarketRow`, `_amountMarketHint`, `_marketPricePanel`, `_marketPriceShown` | F | Merchant amount prompt metas |
| `_sellAdvert`, `_sellStallPanel`, `_myStall`, `_myStallSrc`, `_wantedItems`, `_stalls`, `_stallSignLayer`, `_sellStallCells`, `_sellBagCells`, `_shopCells`, `_shopBagCells`, `_wishCells`, `_wantedCells`, `_wantedBagCells` | F | Merchant windows and signs |
| `AcceptAmount`, `AskStallPrice`, `CloseAmountPrompt`, `ConfirmSellStall`, `SellToWanted`, `SetSellStatus`, `SetWantedStatus`, `StageStallItem`, `UnstageStallItem`, `RefreshStallSign`, static `FreeStallSign` | M | Merchant actions |
| `MerchantCell.Index`, `OnActivate` (set), `OnDropFrom` (set), `Item`, `_count`; `Stall.Sign`, `SignCells`, `IsBuying` | F/P | Release activation, slot drops, sign rebuild |

### Magic Anvil and NPC services

| Member | Kind | Used for |
| --- | --- | --- |
| `_upgradeItemIds`, `_upgradePositions`, `_upgradeSockets`, `_itemSockets`, `_accessorySockets`, `_itemResultSocket`, `_accessoryResultSocket`, `_anvilFooter`, `_upgradeBtn`, `_upgradeLayer`, `_anvilBench`, `_upgradeShown`, `_upgradeSession`, `_upgradePendingResult`, `_upgradeScanTween`, `_upgradeAnvilId`, `_anvilCompanion` | F | Bench state, names, metas, embedded bag |
| `UpgradeInteractionLocked` *(fix/anvil-session)* | P | Bench lock. Falls back to session, pending result and tween. |
| `IsUpgradeTarget(int)`, `UpgradePlacementError(int)`, `ClearUpgradeSocket(int)`, `ClearUpgradeSockets()`, `RefreshBagFit()`, `OnUpgradeBenchChanged()`, `DismissUpgradeConfirm()`, `ShowAnvilPrompt()`, `CloseUpgrade()`, `OpenAnvilBench(AnvilBench)`, `CloseVendor()`, `SlotAt(int)` | M | Staging, Cancel/Back, selection window |
| `OnUpgradeOpen` (delegate on `Net.UpgradeOpenEvent`) | M | Replaced by the Classic selection window. If binding fails, the client's NPC menu stays. |
| `World+BagCompanion` (record constructor), `World+BagFit`, `World+AnvilBench` | T | Anvil bag companion |
| `_warpScroll`, `_warpImage`, `_warpLevels`, `_warpDesc`, `_warpGoldLbl`, `_warpStatus`, `_warpTravel`, `_warpRows`, `_warpSelected` | F | Warp names, selection |
| `_sealSocket`, `_sealHeadline`, `_sealPrompt`, `_sealCodeRow`, `_sealCodeField`, `_sealPad`, `_sealGold`, `_sealConfirm`, `_sealBagCells` | F | Seal names |
| `_pieceMessage`, `_pieceSubMessage`, `_pieceStartBtn`, `_pieceStopBtn`, `_pieceTalkBtn`, `_pieceSocket`, `_pieceResultSockets`, `_pieceBackpackGrid`, `_piecePosition`, `_pieceShown`, `_pieceBusy`, `_pieceSpinning`, `PlacePiece(int)` | F/M | Chaotic Generator |
| `_itemCombineStrip`, `_itemCombineFooter`, `_itemCombineResult`, `_itemCombineButton`, `_itemCombineSlots`, `_itemCombineShown`, `_amountCount` | F | Item combination |
| `_reset` (`Pending`, `Kind`) *(fix/npc-service-requests)*, otherwise `_resetKind` | F | Redistribution pending |
| `_repairShown`, `_repairInFlight`, `_repairQueue`, `_repairFooter`, `RepairTakeFromBag(int)`, `RepairAll()`, `CloseRepair()`, `IsRepairable(int)`, `RepairCostAt(int)`, `RepairableSlots()` | F/M | Classic repair mode |

### Storage, items and services on upstream cd63f98

These members replace the rows above where the 2619 review renamed or removed them (`_vipWhPage`,
`_vipWhPageLbl`, `_vipWhExpirySec`, `_clanWhPageLbl`, `_clanWhLoaded`, `_clanWhLoading`, `_clanWhGoldInput`,
`ClanWhGoldTransfer(bool)`, `ClanWhDepositSlot`, `ClanWhWithdrawSlot`, `VipWithdrawSlot`, `_invDelSlot`,
`_invDelItemId`, `_sealBagCells`, `_pieceBackpackGrid`, `Net._conn`). The plugin no longer sends vault packets or
sets the vaults' pending state itself; every transfer runs through the client's handlers below.

| Member | Kind | Used for |
| --- | --- | --- |
| `_vipWhCells`, `_vipKeyCell`, `_vipWhOpen`, `_vipWhExpiresAt`, `_vipWhPinSet`, `_vipSetPinBtn`, `_vipClearPinBtn`, `_vipWhAmount` | F | VIP vault: Classic 4 x 12 pages over the 48-slot grid, key plaque, lock, PIN actions, amount prompt |
| `VipTakeFromBag(int)`, `VipBagFit(int)`, `AskVipWithdraw(int,int)`, `VipWithdrawInto(int,int)`, `CanDropOnVipVault(int,Variant)`, `DropOnVipVault(int,Variant)` | M | VIP transfers, key use and in-vault moves |
| `_clanWhCells`, `_clanWhDepositBtn`, `_clanWhWithdrawBtn`, `_clanWhAmount`, `TurnClanWhPage(int)` | F/M | Clan bank page, coin buttons with the client's rights, amount and gold prompts |
| `ClanTakeFromBag(int)`, `ClanBagFit(int)`, `AskClanWithdraw(int,int)`, `ClanWithdrawInto(int,int)`, `CanDropOnClanWh(int,Variant)`, `DropOnClanWh(int,Variant)` | M | Clan bank transfers and in-bank moves (rights from `ClanCanWithdraw`) |
| `_whDepositBtn`, `_whWithdrawBtn`, `WhTakeFromBag(int)`, `WhBagFit(int)`, `WithdrawInto(int,int)` | F/M | Warehouse coin buttons, deposit and take-out from the embedded bag |
| `StorageTransferBusy` | P | Busy state of every storage |
| `_invDelQueue` (`DestroyQueue`), `_invDelPanel`, `AskDeleteItem(int)`, `ConfirmDeleteItem()`, `HideDeletePrompt()` | F/M | Bulk destroy through the Classic destroy prompt |
| `OpenVipVaultFromBag()` | M | VIP Vault shortcut in the inventory drawer |
| `Net.GoldChangeLogEvent`, `_tradeInFlight` | E/F | Gold log: shop trades and server gold notices are logged by the client only |
| `_sealCompanion`/`SealTakeFromBag(int)`, `SealBagFit(int)`, `_sealShown`, `_sealWaiting` | F/M | Seal: Classic bag and socket drop instead of the docked inventory |
| `_piecePosition`, `PlacePiece(int)` (`_pieceCompanion` left docked but closed) | F/M | Chaotic Generator: Classic bag rebuilt from the carried items |
| `_smashSocket`, `_smashResults`, `_smashMessage`, `_smashSubMessage`, `_smashStartBtn`, `_smashStopBtn`, `_smashCancelBtn`, `_smashShown`, `_smashSlot`, `SmashTakeFromBag(int)`, `SmashBagFit(int)` | F/M | Disassemble (`itemsmash`) |
| `_csSocket`, `_csTransHeadline`, `_csTransGold`, `_csTransShown`, `_csBagSlot`, `CharacterSealTakeFromBag(int)`, `CharacterSealBagFit(int)` | F/M | Character Seal scroll / Cypher Ring transfer (`char_seal_trans`) |
| `_csSealingPage`, `_csSelectPage`, `_csCodePage`, `_csSelectTitle`, `_csSelectPrompt`, `_csSelectOk`, `_csSlotButtons`, `_csKeys`, `_csCodeStars`, `_csPicked` | F | Character Seal pages (`char_seal`) |
| `_csSheetName`, `_csSheetLevel`, `_csSheetStats`, `_csSheetGold`, `_csSheetCells`, `_csSheetAchievements` | F | Cypher Ring sheet (`char_seal_sheet`) |
| `_expSealJar`, `_expSealPercent`, `_expSealAmount`, `_expSealOn`, `_expSealOff`, `_expSealPick`; `Net.ExpSealState` | F/P | EXP Seal (`exp_seal`) |
| `_tiRows`, `_tiSlots`, `_tiNames`, `_tiPlaces`, `_tiTimes`, `_tiScroll`, `_tiHideToday` | F | Temporary Item List (`timed_items`) |
| `_lootFilterWindow` body controls (named by order) | - | Loot Filter (`loot_filter`) |
| `PluginUi.ReplaceExitMenu` (`ExitMenuRequest`) | API | Classic exit menu (`co_exitmenu`) with the temporary item list |

### Market and communication

| Member | Kind | Used for |
| --- | --- | --- |
| `_mailList`, `_mailListScroll`, `_mailUnreadOnly`, `_mailUnreadPill`, `_mailStatus`, `_mails`, `_mailSelectedId`, `_mailReadSubject`, `_mailReadMeta`, `_mailReadBody`, `_mailReadAttachmentTitle`, `_mailReadAttachmentScroll`, `_mailReadAttachments`, `_mailClaimBtn`, `_mailDeleteBtn` | F | Inbox and reader names, row metas |
| `_mailTo`, `_mailToSuggest`, `_mailSubject`, `_mailBodyRemaining`, `_mailBody`, `_mailGold`, `_mailAttachTitle`, `_mailDropZone` (`MailDropZone._hint/_idle/_hot`), `_mailAttachRows`, `_mailSendBtn`, `_mailComposeStatus`, `_mailAttachments` | F | Compose names, drop zone styling |
| `_msgrList`, `_msgrToInput`, `_msgrTextInput`, `_chatRoomList`, `_chatRoomNameInput`, `_chatRoomStatus`, `_chatRoomLogScroll`, `_chatRoomLog`, `_chatRoomSayInput`, `_chatRoomCurrentId` | F | Messenger and chat rooms |
| `Net.MessengerListEvent`, `Net.ChatRoomListEvent` | E | Handler prepended to detach old rows |
| `_marketPriceSlot`, `_marketPriceName`, `_marketPriceTrades`, `_marketPriceChart`, `_marketPriceUpdated`, `_marketPriceStatus`, `_marketPriceSearch`, `_marketPriceItem`, `_marketPriceCache`, `ClearMarketPriceChart()`, `SetMarketPriceStatus(string)`, const `MarketPriceNoHistoryText` | F/M | Market Price names, no-history repair |
| `ItemSearchPanel._query`, `_pick`, `_group`, `_level`, `_summary`, `_results`, `_tabButtons` | F | Item search names |
| `PriceChart._days`, `_top`, `_bottom`, `_hoverDay`, `_hoverPart`, `Plot`, `Column(int)`, `ValueY(long)`; constants `PlotPadding`, `BarWidth`, `MarkerHalf`, `MarkerHeight`, `AxisWidth`, `AxisFontSize`, `DayFontSize`, `BarFill`, `BarHover`, `BarEdge`, `MaxColor`, `MinColor`, `Midline`, `Baseline` | F/M | `NativeChartBackground` repaint |
| `_merchantSearchEdit`, `_merchantSearchScope`, `_merchantSearchStatus`, `_merchantSearchPages`, `_merchantSearchLines` (`MerchantSearchLine.Root/Whisper/Move/View/Icon/Name/Price`), `_merchantSearch`, `_merchantSearchLoading` | F | Merchant search names |
| The 41 `_pus*` fields of `StoreControls`; `_pusCards` (`PusCardView.Card/Entry`), `_pusCart`, `_pusCatalog`, `_pusCategories`, `_pusCategory`, `_pusHoveredCard`, `_pusModal`, `_pusShown`, `_pusSortPick`, `_pusGiftList`; `FitPowerUpStore()`; constants `PusCategoryWidth`, `PusCartWidth`, `PusWellMargin` | F/M | Power-Up Store names, cards, compact layout |
| The 15 `_auction*` fields of `AuctionFields`; `_auctionTabButtons`, `_auctionPages`, `_auctionLotSlots`; const `SpecialAuctionNoHistoryText` | F | Akara's Altar names, tabs, lots |
| `_auctionPlaceBid`, `_auctionRefresh`, `_auctionRowButtons` *(fix/auction-replies)* | F | Structural fallback without them |

### Social on upstream cd63f98 (nation chat, party HUD, trade grids, boards, prompts)

Upstream removed the chat-room window, the party window and the trade row lists; the `_chatRoom*`,
`ChatRoomListEvent`, `_partyMembersBox` and `_exMineList/_exTheirsList/_exBagList` rows above no longer apply.

| Member | Kind | Used for |
| --- | --- | --- |
| Plugin seams `FrameWindow`, `ReplaceHud(HudPart.Party)`, `ReplacePlayerMenu`, `ReplaceTextInput`; `PluginGame.Party` (`IGameParty`) | API | Classic social frame, party list, player menu, text prompt |
| `_ncRefresh`, `_ncPublicTab`, `_ncPrivateTab`, `_ncPublicRows`, `_ncPrivateRows` | F | Nation chat names, refresh caption |
| `ToggleSeekParty()`, `_seekShown`, `OpenInvitePrompt()`, `ToggleRowMeter()`, `ToggleWarBoard()`, `_warBoardRowShares`, `_partyHudMenu`, `Net.PartyRowMeterMode`, `Net.PartyCommanderId` | F/M | Party list actions, row meter, member menu (Classic context menu), taskbar party board |
| `_wantedClassOpt`, `_wantedMsgInput`, `_seekFilters`, `_fullMoonRow`, `_seekHeading`, `_seekStatusLbl`, `_seekEntries`, `_seekFilter`, `_zone` | F | Party board filter panel, Full Moon Rift actions, row descriptions after the client filter |
| `_exMyOffer`, `_exTheirOffer` (`ExOfferItem.SourceAbs/ItemId/Count/Dura`), `Inv`, `OfferSlot(int)`, `ShowItemTooltip(int, ItemSlot, string)`, `_bagPairing._openedByCompanion`, `HideMainWindow(string)`, `_exStatus`, `Net.ExchangeOtherAddEvent` | F/M/E | Classic trade grids, offering, docked inventory closed, approval withdrawn on partner change |
| `ChatSystem.RenderText/PlainText/ItemOf` (static), whisper header buttons, `_whisperLayer` popups | M | Whisper item links and emoticons, header actions, emoticon and size popups |
| `_cmdGroups`, `_cmdEntries`, `_cmdShown`, `FillCommandEntries(CommandGroup)`, static `CommandUnavailableColour`; `CommandCatalog.Groups`, `CommandEntry.Id` | F/M | Classic command list (`NativeCommandList`): reads the client's rows per group, shows the Classic groups in the client's own list and `_cmdShown`, so the client's `RunListedCommand` runs the activated row. Missing members leave the window on its original art with the client's own two lists. |
| `_escapeStack`, `HudWindow.DockTo(Func<Vector2>)` (public), `DockHudRects()`, `_genieSub` | F/M | Command list: an Escape entry ahead of the main windows (the client registers none for `_cmdWindow`), and the right-middle default position beside the right-side HUD; a dragged position still wins and a layout reset returns to it |
| `ToggleGenie()`, `ToggleGenieFromSub()` (fallback `ToggleGenieRun()`), `_genieShown`, `_genieRequestPending`, `_geniePoints`, `_genieDeadline`, `_genieRunLabel`, `_geniePanel` (its "Use Spirit of Genie" button), `CountInBackpack(int)`, static `SpiritOfGenieItem`, `Net.GenieRunning` | F/M | Genie rows of the command list (`NativeGenieCommands`): each runs the client's own action and is greyed while the client's state refuses it, with the client's status line as the reason. Missing members drop the Genie group. |
| `ChatSystem._input` | F | Prefill the client opens the chat with ("/PM ", "/RoyalOrder ") taken into the Classic chat input (`NativeChat.TakeTyped`) |

### Party, user vote, user card and clan page on upstream 3277df5

The party list, war board, user vote windows and character card use the plugin facades the client added in
3277df5; the reflected `ToggleRowMeter()`, `ToggleWarBoard()` and `_warBoardRowShares` of the cd63f98 social row
above are no longer read.

| Member | Kind | Used for |
| --- | --- | --- |
| `IGameParty.RowMeterMode`, `RowMeterShare`, `CycleRowMeter`, `ToggleWarBoard`, `MoveRow`, `AutoRejoin`, `AutoRejoinTip`, `ToggleAutoRejoin`, `CommanderBarHidden`, `ShowCommanderBar`; `GamePartyMember.HpLook` | API | Classic party list: row meter, War Board action, Move up / Move down arrows in the client's row order, automatic re-invite, commander bar restore, HP look art |
| `HudPart.WarBoard`; `IGameParty.WarBoardOpen`, `WarBoardMode`, `WarBoardAlpha`, `WarBoardRows`, `SetWarBoardMode`, `SetWarBoardAlpha` | API | Classic Party War Board (`ClassicWarBoard`); the client keeps the requests, their cooldown and the ranking |
| `_warBoardCool` (`Label`) | F | 3277df5 only: the client's 5 s tab cooldown, mirrored every frame. No longer read since ff78eef, which exposes `IGameParty.WarBoardCooldown` (see below) |
| `IGameParty.UserVoteHere`, `UserVote(id)`; `IGameUserVote` (`Title`, `SelectPrompt`, `Reasons`, `CostLine`, `AskQuestion`, `AskReason`, `AskSeconds`, `AskSecondsLeft`, `NoticeTitle`, `NoticeLines`, `Send`, `CancelSelect`, `Answer`, `CloseNotice`); `UserVote` constants | API | Classic `user_vote`, `user_vote_ask`, `user_vote_notice` on the message box (`ClassicUserVoteSkin`); the client's member menu offers User Vote in Juraid and answers Draw when the countdown ends |
| `IGameUserVote` proxy `Source` (one or more facade proxies) → bridge `_w`; `_userVoteAskShown` (`UserVoteAsk.Reason`) | F | Only when the client's text table lacks the reason texts (43790-43793, it returns "Reason n" or an empty reason line): the reasons and a received vote's listed reason show English captions (`NativeUserVote`); the client's text is used whenever present |
| `IGameUserInfo.Card` (`GameUserCard`, `GameRankRibbon`, `GameUserRankRow`), `Changed`; `UserInfoCard.PodiumPlaces`; resource `res://assets/ui/daily_rank/medal_{n}.png` | API | Classic character card (`ClassicUserInfoCard`); the client's card body stays hidden in the window, its Confirm button below the Classic card |
| `_fullMoonRow` buttons (Enter, Party Board, Auto Entry) | F | Named `seek_full_moon_enter/shout/auto` for the Classic party board's Full Moon row |
| `OpenClanMemo()` | M | Clan memo from the clan page's own-row menu and the Knights Management Memo action |
| `Net.ClanOnlineCount`, `Net.NoClanBonus`, `ClanBonusCard` (public) | API | Clan online bonus line on the Classic clan page |

### Identity and appearance

| Member | Kind | Used for |
| --- | --- | --- |
| `_nameChangeEdit`, `_nameChangeBtn`, `_nameChangeStatus`, `CloseNameChange` | F/M | Name change |
| `_clanCreateMessage`, `_clanCreateName`; `_clanCreateNotice` *(fix/clan-requests)* | F | Clan creation, fee wording |
| `_disguiseGroupRows`, `_disguiseFormRows`, `_disguiseNote` | F | Transformation |
| `_siegeTaxRatePrompt`, `_nationTaxRateValue`, `_siegeTaxRateValue`, `_nationTaxRateBusy`, `StepNationTaxRate` | F/M | Council tax rate |
| `_equipViewPending`, `_equipViewShown`, `_equipViewStatus`, `RequestEquipmentView`, static `NationName` | F/M | Equipment inspection |
| `_genderPreview`, `_genderEditor`, `_genderStatus`, `_genderConfirm`, `_selfFace`, `_selfHair` | F | Gender change |
| `_changeHairStatus`, `_changeHairShown`, `CloseChangeHair`, `SetChangeHairStatus`, `OnChangeHairResult`; `_changeHairApply`, `_changeHairInFlight` *(fix/beauty-shop)* | F/M | Beauty Shop |
| `_transferPreview`, `_transferEditor`, `_transferStatus`, `_transferConfirm`, `_transferHeader`, `_transferList`, `_transferLayer`, `_transferPanel`, `_transferRows`, `_transferPicks`, `_transferNation`, `_transferInFlight`, `_transferShown`, `CloseNpcDialog`, `OpenNationTransfer`, `SelectTransferCharacter`, `SetTransferStatus` | F/M | Nation transfer window |
| `_transferRevision`, `_transferNotice`, `_transferCompleted`, `SetTransferLocked`, `CancelTransferConfirmation`, `SendNationTransfer(int, IReadOnlyList<>)`; `Net._nationTransferCandidates` *(fix/nation-transfer)*; `Net._nationTransferTarget`; `Net.NationTransferOpenEvent` (handler replaced) | F/M/E | Transfer confirmation, open guard |
| `_rebirthPick`, `_rebirthBonusLbls`, `_rebirthPickLbls`, `_rebirthAddBtns`, `_rebirthRemoveBtns`, `_rebirthBtn`, `_rebirthStatus`, `_rebirthLevelLbl`, `_rebirthPointsLbl`, `_rebirthLayer`, `_rebirthInFlight`, `_rebirthSent`, `_rebirthShown`, `SetRebirthStatus`, `RefreshRebirthUI`, `OnRebirthStatResult` | F/M | Rebirth |
| `_capeR/G/B`, `_capeRVal/GVal/BVal`, `_capeTicket`, `_capeBuyBtn`, `_capeHint`, `_capeStatus`, `_capeChosenLbl`, `_capeReqLbl`, `_capePriceLbl`, `_capeLayer`, `_capeChoice`, `_capePattern`, `_capeCurrent`, `_capeShown`, `_capeRequestInFlight`, `_capePreviewing`, `_selfVisual`, `SelfGear` | F/P | Cape service |
| `SetCapeStatus`, `CloseCape`, `ToggleCape`, `OnCapeResult`, `CapeCellTip`, statics `CapeNeedName`, `CapeSwatch`, `DressCape` (8 args) | M | Cape service |
| `Net.CapeResetEvent` | E | Unlock the draft after a released cape request |
| `Net.PetResetEvent` *(fix/familiar-replies)*, `Net.RebirthResetEvent` *(fix/rebirth-requests)* | E | Tell a disconnect from a cape release: close the picker |
| `RebirthWire.Busy` *(fix/rebirth-requests)* | F | Result reported for a refused local rebirth send (legacy `0` without it) |
| `Net.SendCapeBuy`, `SendChangeHair`, `SendRebirthStatChange` | M | Called reflectively: they return `void` upstream and `bool` on the fix branches |
| `_capeNoteLbl` *(3277df5)*; public `Domain.CapeShop` (`VoucherAllowed`, `ConfirmText`, `ContCost`, `Fill`, `Amount`, texts 10313/10319/10321/10322/15713), `Cape.CapeDef.Castle` | F | Cape service on 3277df5: the client's "Now equipped" or description becomes the cape name's tooltip and "Now equipped" the price line of the worn cape; the voucher is offered for castle capes only, with the client's refusal (10322); the Classic confirmation uses the client's purchase wording (10316/10318, Clan CONT and Noah lines; 10321 for the voucher). The client's own `SelectCape` (run on a clan change) no longer overwrites the Classic grade lines: the gate rewrites them every frame. `_capeContLbl` stays in the client's hidden column (the Classic price line carries the CONT cost). `IGameCapeShop` is not used: it has no pages, dye or preview, and its `Buy` shows the client's confirmation instead of the Classic one. |
| `LookPreview._pivot`, `_model` (set) | F | `LookFraming` |
| `LookEditor._races`, `_faceLbl`/`_hairLbl` (set), `_colour`, `_raceButtons`; `_locked`, `_faceSteps`/`_hairSteps`, `SetLocked` *(fix/beauty-shop)* | F/M | Editor names, 1-based display, lock |

### Familiar

| Member | Kind | Used for |
| --- | --- | --- |
| `_petBagCells`, `_petNameLbl`, `_petLevelLbl`, `_petHpBar`, `_petMpBar`, `_petExpBar`, `_petSatBar`, `_petAttackBtn`, `_petDefendBtn`, `_petLootBtn`, `_petFeedBtn`, `_petDismissBtn`, `_petStatus`, `_itemTipPanel` | F | Familiar window names and details |
| `UsePetSkill(int)`, `SkillTooltip(Skill)`, `Now()`, `PetCooldown(int,double)`, `MyPetEntity()` | M | Skills, cooldowns, portrait |
| `_petHatchTabs`, `_petHatchPage`, `_petTransformPage`, `_petHatchName`, `_petHatchBtn`, `_petHatchStatus`, `_petHatchEggPick`, `_petTransformPetPick`, `_petTransformScrollPick`, `_petHatchInFlight`, `_petHatchShown`, `_petHatchNpc`, `_petHatchSlot`, `_petTransformSlot`, `_petScrollSlot`, `_petHatchLayer` | F | Trainer state (the native fields stay authoritative) |
| `IsPetEgg`, `IsFamiliarItem`, `IsTransformScroll`, `IsValidPetName`, `RefreshPetPickLooks`, `SetPetHatchStatus`, `ClosePetHatch` | M | Trainer rules and actions |
| `WhisperChat.Window`, `ServiceTabs._buttons` | F | Keyboard yielding, tab lock |
| `Net.SendPetHatch`, `SendPetTransform` | M | `bool` on fix/familiar-replies, `void` upstream |
| `HudLayout._moveGripOverlay`; bar recognised by the type names `PetSkillCell` and `HotGrip` | F | Familiar bar grip and default position |

### World overlays

`NativeOverlays` prepares the overlays without a window id once the world is attached (`BecameAvailable`). It names
the controls below and mirrors the countdown urgency and registration state into metas; the client keeps every
control, callback, timer and position rule. All members exist on bare upstream.

| Member | Kind | Used for |
| --- | --- | --- |
| `_joinModal`, `_joinModalTitle`, `_joinModalTimer`, `_joinModalBar`, `_joinModalStatus`, `_joinModalCount`, `_joinModalBtn`, `_joinModalCloseBtn`, `_bifrostBanner`, `_bifrostTitleLbl`, `_bifrostTimerLbl`, `_bifrostBar`, `_bifrostRemaining`, `_isEventRegistered`, const `BifrostUrgentSecs` | F | Temple event registration message box and countdown plaque |
| `_inZoneLeaveBanner`, `_inZoneLeaveTitleLbl`, `_inZoneLeaveBtn` | F | In-zone leave plaque |
| `_deathPanel`, `_inviteAskDialog`, `_challengeAskDialog`, `_townRecallDialog` | F | Death notice and confirmation prompts on the message box art |
| `_upgradeNoticeLabel`, `_shoutLabel`, `_battleeventBanner`, `_battleeventResult`, `_clanBattlePanel`, `_lootPrompt` | F | Outlined broadcast lines |
| `_zoneabilityChip`, `_battleeventBoard`, `_bdwScoreBanner` | F | Zone chip and war score plaques |
| `_lootPanel`, `_lootCells`, `CloseLoot()` | F/M | Loot box: the client's twelve cells on two dropped-item grids, plus a close button |
| `_lootRuleMenu`, `_lootRuleItem`, `_clanMemberMenu`, `_ctxMember`, `PopupMenu.id_pressed` | F | Loot item, party member and clan member right-click menus shown as the Classic context menu; rows emit the client's own `id_pressed` |

Overlays the client builds later are prepared through their fields four times a second (`NativeOverlays.PrepareLater`):

| Member | Kind | Used for |
| --- | --- | --- |
| `_soccerBoard` | F | Soccer score strip (war score plaque with close) |
| `_nestTimerPanel` | F | Monster Stone and Chaos timer toast (outlined text) |
| `_royaleBoard`, `_genieSub` | F | Knight Royale sign-up board and genie quick panel (event frame) |
| `_eventTitleColumn`, `_gatherChip`, `_nereidFlagBoard`, `_nereidScoreBoard` | F | Information plaques |
| `_nereidStrip`, `_wantedToggle`, `_wantedOpen`, `_vanguardLayer`, `_zone` | F | Mini-map plates and Wanted buttons re-hosted at the top right (the Classic HUD hides the client mini map) |
| `_areaBanner`, `_msgOut`, `_orderMessage`, `_killSplash` | F | Outlined Arial Bold banners |
| `_towerButton`, `_dropOff` | F | Siege tower Board button (moved under the Classic target frame) and drop-off |
| `_rankerPaper` | F | Ranker paper buttons and text |

### Events, rankings and progression

| Member | Kind | Used for |
| --- | --- | --- |
| `_ladderKarus`, `_ladderElMorad`, `_ladderOwnKarus`, `_ladderOwnElMorad`; `LadderRow.Rank`, `Mark`, `Name`, `Clan`, `Points`, `Bonus` | F | PK ladder (`rank`) columns |
| `_achTitleBanner`, `_achSearch`, `_achStatus`, `_achCategoryBox`, `_achListRow` | F | Achievement title banner, filters and category column |
| `_genieReturnHome`, `_genieHuntInPlace`, `_genieHideRange`, `_genieCureCurse`, `_genieOther` | F | Genie Others page |
| `_genieSlots` (`Button[28]`: Attack 0-7, Recovery 8-15, Support 16-27), `_genieModes` (`Dictionary<GenieMode, CheckButton>`), `_genieParty` (`CheckBox[12]`), `_genieRecovery`, `_geniePetPotion`, `_geniePetSelect`, `_geniePetPercent`, `_genieHammerThreshold`, `_genieHammerStatus` *(3277df5)* | F | Genie window (`NativeProgression.PrepareGenie`): the three skill columns (8 + 8 + 12 cells, a party check under each support cell), the mode toggles named by `GenieMode` (`genie_mode_attack`, `_basicattack`, `_combo`, `_recovery`, `_support`, `_leadertarget`, `_hppotion`, `_mppotion`), the recovery percent, the Familiar potion, the 11 "Set Others" rows (`GenieTownReturn.OtherRows`, hammer rows included) and the hammer threshold. The removed `_genieAutoHammer`, `_genieSkills` and the Combo hint are no longer read. |
| `_genieSub` rows (`HudPart.GenieSub` seam, 3277df5) | F | The Classic quick panel keeps skinning the client's own panel (now on its own layer); the third monster row has no action button and keeps the row height of the others. |
| `StatBar._frac` | F | Original progress bar mirroring the vanguard countdown |

The event, ranking and war windows of the event-window family (`ClassicEventFamily.WindowIds`) are restyled from their
own controls and need no reflected member.

### HUD on the bundled 2.x art (`assets/myko`)

Used only when the layouts are found (bundled in `assets/myko`, see `src/LocalAssets.cs`); without them nothing below is read.

| Member | Kind | Used for |
| --- | --- | --- |
| `_premiumChip`, `_clanPremiumChip`, `_clanPremiumInClan`, `_clockLabel`, `PlaceTopIcons` | F/M | Top-right group: clan premium bar and the time box; the client's chips and clock are hidden while the group shows them (the premium chip stays as an invisible spacer so the client's remaining top icons keep clear) (`NativePremium`) |
| `Net.GmListEvent` (`Action<IReadOnlyList<string>>`) | E | Not in the client yet. When a client adds it, the GM List bar follows it (`NativeGmList.Attach`); until then the list is empty |

### Chat rooms, voice and chat categories on upstream 3277df5

The nation chat rooms open a group-chat style room window (`chat_room`). The Classic room (`ClassicChatRoomSkin`,
`ClassicChatRoomLayout`, preparer `NativeChatRoom`) frames the client's own room through `FrameWindow("chat_room")`;
posts, voice notes, the voice call, the roster, the popups and every callback stay native. No plugin facade covers
the room or `VoiceChat`; the plugin subscribes no room or voice event (it reads the client's controls and tweens).

| Member | Kind | Used for |
| --- | --- | --- |
| Plugin seam `FrameWindow("chat_room")`, `WindowFrame.SetDragHandle/Close/TitleChanged`, `HudWindow.SetMinimized/Minimized/MinimizedChanged/Body` | API | Classic frame, compact whisper bar when minimized, title `Name (n/max)` |
| `_roomScroll`, `_roomLog`, `_roomOlder`, `_roomComposerRow`, `_roomInput`, `_roomRecord`, `_roomSend`, `_roomRecordStrip`, `_roomRecordClock`, `_roomRecordLevel`, `_roomVoiceRow`, `_roomMembersToggle`, `_roomConfigButton`, `_roomJoin`, `_roomDeafen`, `_roomVolume`, `_roomMic`, `_roomVoiceGear`, `_roomVoiceLeave`, `_roomMembersColumn`, `_roomRosterRows`, `_roomRosterEmpty`, `_roomLeaderBox`, `_roomLeaderHandOver`, `_roomLeaderKick`, `_roomChannelsButton`, `HudWindow._header` (door and channel-list header icons) | F | Names for the skin and the audit; all must exist or the room keeps the plain Classic frame around the client's content |
| `_roomLayer` (popups: `_roomConfig`, `_roomVoiceSettings`, `_roomPeerPopup`, `_roomMemberMenu`, `_roomEmoticons`) | F | Classic menu panel on the popups as the client creates them |
| `_roomBlink` (`Tween`) | F | The minimized blink becomes the whisper attention blink on the compact bar |
| Roster row `StyleBoxFlat` (`UiTheme.Row` colours: `RowHover` selected, `7fd98b` speaking) | Look | Classic row state mirrored each frame |
| `_ncPubOpen`, `_ncPriOpen`, `_ncMembersTitle`, `_ncMembersCount`, `_ncMemberRows` | F | Nation chat Open buttons, members title and summary in the body |
| `ChatSystem.ChatEntry.Place`; `ChatCategory.NoahKnights/ChatRoom/All`, `ChatType.NoahKnights` (34); `IGameCharacter.NoahKnight` | F/API | Room lines show `[ Zone ]`; the Classic chat masks with `ChatCategory.All` (saved filters migrate with `chat.filter_rev` 2 like the client's `ChatPrefs`); Noah Knights tab (`\|`) only for Noah Knights, who lose Clan and Alliance. `IGameChat.Lines` carries rendered BBCode only, so the Classic chat keeps reading `_store` for its own format |
| `IGameCommands.Run(KeyAction.ToggleRun/AutoAttack)`, `ShowExitMenu()` | API | Replace the reflected `ToggleRunMode`, `ToggleAutoAttack`, `ToggleEsc(true)`; `StartCameraHalfTurn` has no key action and stays reflected |
| `WhisperLinks.Linkify/IsWebLink` | M | Whisper notices with web links open them, as the client does |
| `EmoticonPicker.InputButton` (the `TextureButton` in `_roomInput`) | F | Named `room_emoticons` and set to the inline emoticon size inside the Classic composer field |

`chat_room`, `user_vote`, `user_vote_ask`, `user_vote_notice` and `repurchase` are excluded from the generic
`ClassicSkin` window extender in `Plugin.cs`; their skins no longer undo its theme and stray frame. Item links and
emoticons in room posts need the installed content (`items.json`, `assets/ui/emoticons/sheet.png`), which
`chat-room-audit` mounts from `build/client/source-content/knightonline.pck`.

### Changed shapes on upstream ff78eef

The plugin compiles unchanged on ff78eef; these reflected members changed shape at run time. Each call adapts to
the member it finds, so older clients keep the previous path.

| Member | Kind | Used for |
| --- | --- | --- |
| `World+BagCompanion.IntoBag` (`Func<int, int, int?, bool>`: the amount picked with the mouse wheel; before `Func<int, int, bool>`) | P/T | `NativeVendor` reroutes buys through the Classic approval and `NativeAnvil` builds the anvil companion; the delegate shape comes from `Native.MemberType`, the amount is ignored (the approval asks the count, anvil items move whole) |
| `WithdrawInto`, `VipWithdrawInto`, `ClanWithdrawInto` `(int, int, int? amount)`; `AskWithdraw`, `AskVipWithdraw`, `AskClanWithdraw`, `DepositSlot`, `OfferSlot` gained an optional `int? amount` | M | Storage drops into the bag pass the drag's `DragLayer.AmountKey` amount (null = whole stack); the optional ones bind through `Native.Method`'s default filling |
| `AttachWeapons(Node3D, int[], HandWearer, int npcType)`, static `WeaponWearer(bool npc, bool statue, int race, int npcId, int size, int modelId)`, static `AttachBuiltInPlugs(Node3D, string)`, `_mobIndex`; `World+Ent.Race`, `Size`; `NoWeaponNpcIds` removed | M/F | NPC portrait: weapons by the model's plug slots and scale, the model's own built-in weapons on a freshly loaded model; the unarmed list is read only where it still exists |

### Party HUD and War Board on upstream ff78eef

Upstream rewrote its party HUD (class diamond with a level gauge, ping signal, buff row, drag reordering, a settings
popup) and removed its row meter switch ("the damage/healing switch lives on the war board only"). The Classic party
list keeps the original cells and shows the new member data through the party facade; nothing new is reflected.

| Member | Kind | Used for |
| --- | --- | --- |
| `IGameParty.MemberBuffs(id)` (`GamePartyBuff`: `SkillId`, `Icon`, `Tooltip`) | API | Buff icons on each cell's name line, 14 px in the client's order, ending under the bars; the name gives way (ellipsis) down to two icons, further buffs are not shown (the client also shows one line). Each icon has the client's buff tooltip; without art the skill's initial |
| `IGameParty.PingMs(id)`; `PartySignal` (`Level`, `Arcs`, `Tooltip`, `Unknown`, `NoSignal`) | API | Connection glyph in the arrow column beside the name line (three arcs in the client's colours, struck through without a signal, the client's "Ping: N ms" tooltip); the member tooltip's last line |
| `IGameParty.LevelPercent(id)` | API | "Level progress N.N%" in the member tooltip (the own row: the experience percent) |
| `GamePartyMember.Picked` (= the client's `PartyHighlight`) | API | The green selection frame follows the client's highlight: the picked member, or the party member who is the world target; targeting someone outside the party clears it. The client raises `Changed` when it moves |
| `IGameParty.WarBoardCooldown` | API | The War Board tab cooldown beside the Classic tabs, refreshed on `Changed` (raised whenever the shown seconds change); replaces the per-frame `_warBoardCool` mirror and the board's World accessor |
| `IGameParty.RowMeterMode`, `RowMeterShare`, `CycleRowMeter`; `Config.PartyRowMeter` (default now off) | API | Kept: the Classic "Meter" action still cycles Off, Damage, Healing. The client forces the row meter off at World build only while its own party HUD is shown (`KeepRowMeterOff` is skipped when a plugin replaces `HudPart.Party`), so the replacement keeps it |
| `IGameParty.AutoRejoinTip` (long official texts 44730/44731) | API | Re-invite tooltip wrapped to the client's buff tooltip width (`TextWrap.Wrap`, 44 letters); the party list and the war board use the original tooltip look (co_tooltip_us) |

`MoveRowTo` (drag reordering) is not used: the Classic cells keep the Move up / Move down arrows (`MoveRow`).

### Preview-only members

The preview fixtures also reach client internals that the plugin never touches at runtime. Examples are the
`Build*Panel`/`Build*Window` builders, the `On*` reply handlers, `Net.SeedPreviewEnter`/`SeedPreviewPet`, the
`ChatSystem` constructor with `Build`/`DetachNetwork`, `_itemTipLayer`, and the `Net` event backing fields used
to deliver replies. An upstream rename there breaks an audit, not the game. The full lists are in the group port
reports (see section 6).

## 4. Known differences from the former fork build

These differences remain on `classic-integration`. Bare-upstream degradations are listed in section 2.

**Chat and HUD**

- Service notices that `World` code sends through `CombatNotice` (upgrade results, warp travel, Beauty Shop
  success, cape updates and others) appear in Info as status lines, as upstream does. The fork had rerouted about
  40 call sites to the chat. `ChatSystem.Info` and server notices still reach the Classic chat as System lines.
- Auto-run continues while the player types in the Classic chat. This matches upstream's native chat: the plugin
  reports typing through upstream's `SetTyping`, and upstream does not stop movement for it.
- Upstream publishes a chat entry before it stores it. `ChatWindow` therefore re-reads the history one deferred call
  later, coalescing bursts. There is no visible difference.
- The minimap and full-map heading follow upstream's camera yaw. The fork's travel-direction heading was not ported.
- Upstream still builds its own nearby-players card inside the hidden chat panel. It stays invisible and the
  plugin rebuilds its rows from it. On touch UI there is no card, so `NearbyPlayers` is empty.
- A native whisper row, or the whisper fade tween, can exist for one frame before the plugin replaces or kills it.
  In practice this happens before the frame is drawn.
- The client's own combat-log panel gets no gold line, because the plugin's gold log handler runs first. Only
  Classic Info shows gold income and spending, and Classic hides that panel.

**Character and NPC**

- The Escape entry for the clan details window is inserted before the main windows, not directly after clan points.
- The preset label is rewritten one frame after each native refresh.
- Party docking is reapplied on visibility, resize and viewport change. Party no longer takes part in the
  right-hand dock stack, as in the fork.

**Inventory, storage, anvil, services**

- A partial bag-to-grid move goes to one destination (`MoveBetween(from, to, amount)`). The fork's multi-step plan
  (fill several stacks, then one free cell) is not reproduced. An amount that does not fit is refused by the client.
- Returning a staged anvil scroll stack to another bag cell releases the socket and leaves the stack where it is.
  The fork moved one scroll on the client only. The server refuses partial in-bag splits, so the fork ended in the
  same state.
- Repair All checks the cost of the first damaged item only. Later items are refused by the server, and the queue
  is cleared.
- The in-vault rearrangement (op 4) is written by the plugin to `Net._conn`. The client only knows about it through
  the plugin's in-flight flag.
- Storage drag previews keep Godot's default grab point and scale. The anvil uses the Classic `IconDragPreview`.
- The warehouse's embedded bag cells read the live inventory every frame. The fork could show a stale count until
  the next warehouse refresh.

**Vendor, trade, merchant**

- `MoveBetween` does not refuse while a vendor reply is pending. The Classic shop's bag cells refuse moves while a
  modal is open, and the separate inventory windows are closed while the shop is open.
- The client still pops its exchange request dialog. The plugin hides it in the same event dispatch, before it is
  drawn.
- Compact vendor pages replace `_vendorCatalogue`. Buy packets keep the source line and list.

**Identity and appearance**

- The player menu always sends an equipment-view request. The plugin applies only the reply with the requested name,
  but it cannot refuse a second request while one is pending.
- Toggling the cape window while a purchase is pending hides it and closes it at the end of the frame. A second
  toggle in that same frame closes it instead of being ignored.
- Nation transfer replaces the client's `OnNationTransferOpen` handler. When the plugin ignores a list it did not
  ask for, it restores Net's cached candidates.

**Familiar**

- Calling `OpenPetHatch` while the trainer is open resets the name and the staged items. The trainer restores the
  staged items on the next frame, but not the name.
- A native familiar refresh that has no Net event (for example a death state change) reaches the details on the
  next tick.
- Item tooltip panels become mouse-transparent only after a familiar cell has shown them.

**Market and store**

- The compact Power-Up Store layout (below 960 px) and touch centring exist only in the plugin's store preparer,
  under the Classic store skin. The fork had them in core for every theme. The legacy `upstream-integration-audit`
  puts the generic frame on the store at 800×600, so its `pus-compact` check fails. `store-audit` covers the real
  case and passes.
- Godot names unnamed nodes `@Class@N`. The plugin names native controls after the client adds them, so these
  counters in audit check strings differ from the fork's. Normalise them before you compare check lists.

## 5. Optional client hooks

None of these is required. Each would replace a reflection or a workaround.

**Windows and dialogs**

- A window action and state registry: `IGameWindows.Invoke("anvil", "reset" | "back")`,
  `("repair", "all" | "close" | "take")`, and a state bag (busy, compound, error, scan outcome, pending,
  selected). It would replace the per-frame meta mirroring.
- One interception point for confirmations and prompts: vendor confirm, exchange request, final and amount, merchant
  open, rebirth, cape and nation-transfer submit. This should include a submit entry point that does not ask again.
- `ui.ReplaceNpcMenu(...)` for the anvil selection window.
- `SetBodySuppressed("repair")` for the repair catalogue.
- A `HudWindow` VIP PIN dialog (`vipwarehouse_pin`).
- A wildcard `ExtendWindow("whisper_*")`, plus a row builder or row metas.
- `PluginUi.EscapeCloses(Func<bool>, Action)`.
- Buy-back close (3277df5): the `repurchase` header X hides the window before its `Closed` handler runs, so
  `CloseRepurchase` returns early and never sends `RepurchaseReply.Close`. The plugin closes through
  `IGameRepurchase.Close()`; a core fix is to send the close request in the `Closed` handler regardless of `Visible`.
- `HudWindow` keep-position and dock-override flags.
- `HudWindow.AttentionChanged`.
- A replaceable resize grip with `Resizing`/`ResizeEnded` events, or seam snapping as a `HudLayout` option.
- Stable names, or public accessors, on `LookEditor` and `LookPreview`. On `LookEditor` the plugin uses the rows,
  steppers, value labels, colour button and colour availability. On `LookPreview` it uses the camera, the model and
  a gear-aware `Show`. Both also need `RemoveChild` before `QueueFree`.
- A `PriceChart.Background` style box.
- `UpgradeSocket.CanDrop`/`Dropped` and an "embeds full inventory" option for piece change.

**Inventory and items**

- On `IGameInventory`:
  - bag-companion access (`Companion`, `CompanionBusy`, `Fit`, `ReservedCount`, `CloseCompanion`) and a "do not
    open the inventory" attach option;
  - `ConfirmDrop` and `TransferToInventorySlot`.
- An `IGameStorage` read model with `Deposit`, `Withdraw`, `Move`, `Page` and `PageSize`.
- `Net.SendVipWarehouseStore`/`SendClanWhStore` with op-4 acknowledgement handling.
- `IGameItemRules.PlacementFilter` for anvil sockets.
- In `ItemSlotView`: the Classic count badge, the full-cell count layout, and a drag preview that keeps icon size
  and grab point.
- `MoneyEdit.GroupDigits`.
- A `quantityOnly` flag on the amount prompt.
- `MoveBetween` refusing while a vendor reply is pending.
- Server: partial in-bag splits, if returning one material to a chosen cell is wanted.

**Chat, HUD, notices**

- A `SystemNotice` → `GameLogKind.System` classification for World-side notices.
- Movement that treats plugin typing like native typing (`!Chat.IsActive || Chat.PluginTyping`).
- Store-before-publish in `ChatSystem.AddEntry`, or structured entry events.
- A gold-change log hook, or `GoldIncome`/`GoldExpense` log kinds.
- `PluginUi.ExtendHud(HudPart.FamiliarBar, ...)` with default position and drag-handle options.
- A native-slot tooltip factory.

**Content and requests**

- An event (or names) for `ShowQuestView`, quest notification captions, and quest-log reward refreshes.
- A name on `_clanContent` and an `OpenClanTab` API.
- `IGameModels.BuildNpc(modelId)` for portraits.
- A `PetSheetShown` notification.
- An `OpenPetHatch` that keeps an open draft.
- Client request guards the fork had:
  - one outstanding equipment-view request;
  - Net ignoring a nation-transfer candidate list while an account is open;
  - `ToggleCape` refusing while a request is in flight.
- `Send*` methods returning `bool` upstream. The fix branches already do this.

**Preview correctness**

- `World.UpdateInventoryTooltip` using `_itemTipPanel.GetViewport()`.
- `MouseFilter = Ignore` on item tooltip containers.
- A shared coin maximum constant.

## 6. Verifying after an upstream update

1. **Build the plugin twice.** Build once against a bare upstream client and once against `classic-integration`
   (upstream plus the remaining fix branches, rebased):
   `dotnet build KnightOnlineUiClassic.csproj -c Release -p:LibreKOClientDir=<folder with LibreKO.dll>`.
   Both builds must have 0 errors. The only known warning is CS8604 in `ClassicVendorSkin`. A failure here means a
   public API type changed.
2. **Check reflected names statically.** Extract every string literal passed to `Native.*` under `src/Native`.
   Check each identifier against the new client's `Client/src`. Compare method arity too, because overloads change
   between upstream and the fix branches (`MoveBetween`, `SellSlot`, `SendNationTransfer`). Every name in section 3
   must exist on `classic-integration`. Only the names marked with a branch may be absent upstream.
3. **Run the plugin unit tests:** `dotnet test tests/AnvilRules.Tests` and `dotnet test tests/ClassicChatFormat.Tests`.
   The plugin csproj excludes `tests/**`.
4. **Build the preview against `classic-integration`:**
   `dotnet build preview/Preview.csproj -p:LibreKOClientDir=...`. The audits use APIs from the fix branches
   (for example `bool SendPetSkill` and the `ushort` move amount), so the preview targets the integration client.
5. **Run every audit mode for both nations.** Use `godot --path preview -- <mode>` for Human and append `karus`
   for Karus. Set `LIBREKO_AUDIT_CLIENT_PACK` to the client's content pack and `LIBREKO_AUDIT_OUTPUT_DIR` to a fresh
   folder per mode. The modes are the routes in `preview/Preview.cs`:
   - preview variants: `preview`, plus `horizontal`, `hotbar-check`, `detail` and `native-reference` combinations;
   - HUD and chat: `hud-audit`, `chat-colours-audit`;
   - character: `character-audit` (with `inventory-parent-audit`), `details-audit`, `skill-audit`, `party-audit`;
   - NPC: `npc-integration-audit` (with `npc-portrait-regression`), `npc-portrait-audit`, `npc-design-audit`,
     `hunt-icon-options`, `upstream-npc-audit`, `forgotten-temple-audit`;
   - items and storage: `inventory-audit`, `storage-audit`;
   - trade: `vendor-audit`, `trade-audit`, `merchant-audit`, `merchant-palette`;
   - anvil and services: `anvil-audit`, `classic-services-audit`, `npc-services-audit`;
   - market and communication: `mail-classic-audit`, `communication-audit`, `market-price-audit`,
     `merchant-search-audit`, `store-audit`, `auction-audit`;
   - identity and appearance: `identity-audit`, `council-rate-audit`, `equipview-audit`, `gender-audit`,
     `beauty-audit`, `nationtransfer-audit`, `rebirth-audit`, `cape-audit`;
   - familiar: `pet-audit`, `pet-portrait-audit`, `pet-connection-audit`, `pet-keyboard-audit`,
     `pet-world-reconnect-audit`;
   - legacy: `upstream-integration-audit`.

   Some audits are known to fail on the fork baseline too, with the same messages:
   - `details-audit` and `npc-portrait-audit`: a stale `ClassicNpcDialogue` → `ClassicDetailPanel` cast;
   - `npc-services-audit`: "gender not registered as open in Escape stack";
   - `preview-hotbar-check` and `preview-horizontal-multi-detail`: the F4 hotbar check;
   - `upstream-integration-audit`: `pus-compact` (section 4).
6. **Compare with the previous accepted run.** The audit runner and comparison scripts live in the maintainer's
   workspace, not in this repository. Any equivalent works:
   - Launch both nations per mode with a timeout and record exit codes.
   - Normalise `@Class@N` in check lists, then require the same checks in the same order.
   - Compare images against the previous run. Use two runs of the baseline as a noise mask.
   - Known noise: real desktop cursor hover and tooltip position, text caret, 3D preview and cloth or particle
     variation, animation timing, random rewards, and mail clock stamps.
   - Search the logs for `native member unavailable`. On `classic-integration` it must not appear.
7. **Review the result visually.** Check the eight Character Report, Quest, Clan and Friend pages for Human and
   Karus, plus selection and disabled states. They must stay pixel-identical. Then install the verified build into
   each client's plugin folder.

The per-group port reports and fix-branch reports were written during the port. They record parity runs, reviewed
image differences and draft pull request texts. They are kept in the maintainer's workspace under
`research/upstream-plugin-port/` (`port/*.md`, `fixes/*.md`).

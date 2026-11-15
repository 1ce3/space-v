using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using RocketGame.Building;

namespace RocketGame.Research;

/// <summary>
/// Root of scenes/research_screen.tscn (guide B 6.4). Header with RP and max build weight, one column per tech Tier,
/// one card per node with a buy button. Emits BuildRequested; never calls Control (decision #22).
/// Part names and icons come from PartCatalog (UI only); without a definition the ModelId is prettified.
/// All colors and sizes are exports so the look can be tuned in the Inspector.
/// </summary>
[GlobalClass]
public partial class ResearchScreenRoot : Godot.Control
{
    /// <summary>The player wants to go back to the build screen.</summary>
    [Signal] public delegate void BuildRequestedEventHandler();

    /// <summary>"Go to Workshop" button.</summary>
    [ExportGroup("Nodes")]
    [Export] public Button BuildButton { get; set; }
    /// <summary>Big RP number in the header.</summary>
    [Export] public Label RPLabel { get; set; }
    /// <summary>Max build weight number in the header.</summary>
    [Export] public Label WeightLabel { get; set; }
    /// <summary>Last action message at the bottom.</summary>
    [Export] public Label StatusLabel { get; set; }
    /// <summary>Receives one column per Tier.</summary>
    [Export] public HBoxContainer Columns { get; set; }
    /// <summary>Full-screen background.</summary>
    [Export] public ColorRect Background { get; set; }
    /// <summary>Header bar panel.</summary>
    [Export] public PanelContainer HeaderPanel { get; set; }
    /// <summary>Badge around the RP number.</summary>
    [Export] public PanelContainer RPBadge { get; set; }
    /// <summary>Badge around the max build weight.</summary>
    [Export] public PanelContainer WeightBadge { get; set; }

    /// <summary>Screen background.</summary>
    [ExportGroup("Colors")]
    [Export] public Color BackgroundColor { get; set; } = new(0.055f, 0.067f, 0.094f);
    /// <summary>Header and badge panels.</summary>
    [Export] public Color PanelColor { get; set; } = new(0.094f, 0.114f, 0.153f);
    /// <summary>Card body.</summary>
    [Export] public Color CardColor { get; set; } = new(0.118f, 0.141f, 0.188f);
    /// <summary>Highlight for nodes that can be bought and for the main button.</summary>
    [Export] public Color AccentColor { get; set; } = new(0.278f, 0.553f, 0.957f);
    /// <summary>Border and badge of purchased nodes.</summary>
    [Export] public Color PurchasedColor { get; set; } = new(0.243f, 0.725f, 0.471f);
    /// <summary>Border of locked nodes.</summary>
    [Export] public Color LockedColor { get; set; } = new(0.243f, 0.267f, 0.318f);
    /// <summary>Warning text (not enough RP).</summary>
    [Export] public Color WarningColor { get; set; } = new(0.949f, 0.663f, 0.247f);
    /// <summary>Main text.</summary>
    [Export] public Color TextColor { get; set; } = new(0.918f, 0.933f, 0.953f);
    /// <summary>Secondary text.</summary>
    [Export] public Color MutedTextColor { get; set; } = new(0.565f, 0.604f, 0.671f);
    /// <summary>Opacity of locked cards (0..1).</summary>
    [Export(PropertyHint.Range, "0.2,1,0.05")] public float LockedOpacity { get; set; } = 0.6f;

    /// <summary>Card width (px).</summary>
    [ExportGroup("Layout")]
    [Export(PropertyHint.Range, "160,600,1")] public float CardWidth { get; set; } = 260f;
    /// <summary>Space between cards and columns (px).</summary>
    [Export(PropertyHint.Range, "0,64,1")] public int Spacing { get; set; } = 14;
    /// <summary>Inner padding of cards and panels (px).</summary>
    [Export(PropertyHint.Range, "0,48,1")] public int Padding { get; set; } = 14;
    /// <summary>Corner radius of cards, panels and buttons (px).</summary>
    [Export(PropertyHint.Range, "0,32,1")] public int CornerRadius { get; set; } = 10;
    /// <summary>Card title font size.</summary>
    [Export(PropertyHint.Range, "8,48,1")] public int TitleFontSize { get; set; } = 17;
    /// <summary>Small text font size.</summary>
    [Export(PropertyHint.Range, "8,32,1")] public int SmallFontSize { get; set; } = 13;
    /// <summary>Size of part icons on cards (px).</summary>
    [Export(PropertyHint.Range, "8,64,1")] public int IconSize { get; set; } = 18;

    /// <summary>Debug: RP added when the screen opens (0 in the game).</summary>
    [ExportGroup("Debug")]
    [Export] public int DebugStartRP { get; set; }

    private sealed class Card
    {
        public PanelContainer Panel;
        public StyleBoxFlat Style;
        public Button BuyButton;
        public Label CostLabel;
    }

    private readonly Dictionary<string, Card> _cards = new();
    private ResearchCampaignEngine _campaign;
    private TechTreeManager _tree;

    public override void _Ready()
    {
        if (!Require(BuildButton, nameof(BuildButton)) || !Require(RPLabel, nameof(RPLabel))
            || !Require(Columns, nameof(Columns))) return;
        _campaign = ResearchCampaignEngine.Instance;
        _tree = _campaign?.TechTree;
        if (_tree == null)
        {
            GD.PushError("[Research] ResearchScreenRoot needs the ResearchCampaignEngine autoload with a TechTree.");
            return;
        }

        ApplyStaticStyles();
        BuildButton.Pressed += OnBuildButtonPressed;
        _campaign.RPChanged += OnRPChanged;
        _tree.NodeUnlocked += OnNodeUnlocked;
        if (DebugStartRP > 0) _campaign.AddRP(DebugStartRP);

        BuildCards();
        Refresh();
    }

    public override void _ExitTree()
    {
        if (BuildButton != null) BuildButton.Pressed -= OnBuildButtonPressed;
        if (_campaign != null) _campaign.RPChanged -= OnRPChanged;
        if (_tree != null) _tree.NodeUnlocked -= OnNodeUnlocked;
    }

    /// <summary>Buys a node like the card button does. Returns true on success. Used by the buttons and by tests.</summary>
    public bool TryPurchase(string nodeId)
    {
        if (_tree == null) return false;
        bool ok = _tree.PurchaseNode(nodeId);
        string name = NodeName(nodeId);
        SetStatus(ok ? $"Unlocked {name}." : $"Cannot unlock {name} yet.");
        Refresh();
        return ok;
    }

    /// <summary>Updates the header numbers and every card's look and button.</summary>
    public void Refresh()
    {
        if (_tree == null) return;
        RPLabel.Text = _campaign.CurrentRP.ToString(CultureInfo.InvariantCulture);
        if (WeightLabel != null) WeightLabel.Text = _tree.GetMaxBuildWeight().ToString(CultureInfo.InvariantCulture);

        foreach ((string nodeId, Card card) in _cards)
        {
            TechNode node = _tree.GetNode(nodeId);
            if (node == null) continue;
            bool bought = _tree.UnlockedNodes.Contains(nodeId);
            bool prerequisitesMet = node.PrerequisiteNodeIds.All(_tree.UnlockedNodes.Contains);
            bool canBuy = !bought && _tree.CanPurchase(nodeId);

            Color border = bought ? PurchasedColor : canBuy ? AccentColor : LockedColor;
            card.Style.BorderColor = border;
            card.Panel.Modulate = new Color(1f, 1f, 1f, bought || prerequisitesMet ? 1f : LockedOpacity);
            card.CostLabel.Text = bought ? "UNLOCKED" : $"{node.CostRP} RP";
            card.CostLabel.AddThemeColorOverride("font_color", bought ? PurchasedColor : canBuy ? AccentColor : MutedTextColor);

            card.BuyButton.Disabled = !canBuy;
            card.BuyButton.Text = bought ? "Unlocked" : canBuy ? "Unlock" : prerequisitesMet ? "Not enough RP" : "Locked";
            StyleButton(card.BuyButton, bought ? PurchasedColor : AccentColor);
            card.BuyButton.AddThemeColorOverride("font_disabled_color",
                bought ? PurchasedColor : prerequisitesMet ? WarningColor : MutedTextColor);
        }
    }

    // ---------- building the cards ----------

    // ---------- War Thunder style tree: one tab per category, rows = tiers, lines = prerequisites ----------

    [ExportGroup("Tree")]
    [Export(PropertyHint.Range, "40,200,1")] public float TierLabelWidth { get; set; } = 80f;

    /// <summary>Tab order; categories not listed here come after.</summary>
    public static readonly string[] CategoryOrder =
        { "Động cơ", "Nhiên liệu", "Vật liệu", "Khung & khoang", "Điều khiển", "Liên lạc", "Cảm biến nhiên liệu", "Cảm biến áp suất", "Cảm biến nhiệt", "Năng lượng & nhiệt", "Hạ cánh" };

    /// <summary>Category of a node without one set in its .tres.</summary>
    public static string DefaultCategory(TechNode node)
    {
        if (node == null) return "Khác";
        if (!string.IsNullOrEmpty(node.Category)) return node.Category;
        string id = node.NodeId;
        if (id.StartsWith("fuel_")) return "Nhiên liệu";
        if (id.StartsWith("mat_")) return "Vật liệu";
        if (id.StartsWith("comm_")) return "Liên lạc";
        if (id.StartsWith("sensor_")) return "Cảm biến";
        return id switch
        {
            "t1_heavy" or "t2_main_engine" or "t2_upper_stage" => "Động cơ",
            "t1_frame" or "t2_frame" or "t3_frame" or "t2_pod" => "Khung & khoang",
            "t1_avionics" or "t3_navigation" => "Điều khiển",
            "t2_power" or "t3_thermal" or "t4_rtg" => "Năng lượng & nhiệt",
            "t3_edl" or "t3_lander" => "Hạ cánh",
            _ => "Khác",
        };
    }

    /// <summary>Branch order inside a tab; empty branch = no sub-header.</summary>
    public static readonly string[] SubCategoryOrder = { "Tầng thấp (mặt đất – 50 km)", "Tầng giữa (30 km – quỹ đạo thấp)", "Tầng trên & không gian sâu" };

    public static string DefaultSubCategory(TechNode node)
    {
        if (node == null) return "";
        if (!string.IsNullOrEmpty(node.SubCategory)) return node.SubCategory;
        return node.NodeId switch
        {
            "t1_lr87_aj7" or "t1_rocketdyne_f1" or "t4_merlin_1d" or "t5_raptor_2_3" or "t1_heavy" or "t2_main_engine" => SubCategoryOrder[0],
            "t2_rocketdyne_j2" or "t3_rs25_ssme" => SubCategoryOrder[1],
            "t1_aj10_137" or "t2_rl10" or "t3_nstar_ion" or "t2_upper_stage" => SubCategoryOrder[2],
            _ => "",
        };
    }

    private HFlowContainer _tabs;
    private string _category;

    private void BuildCards()
    {
        Columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (Columns.GetParent()?.GetParent() is ScrollContainer sc) sc.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        if (Columns.GetParent() is Godot.Control margin) margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (_tabs == null)
        {
            // Tabs go above the scroll area (Layout > [tabs] > Scroll > Margin > Columns).
            Node scroll = Columns.GetParent()?.GetParent();
            Node layout = scroll?.GetParent();
            _tabs = new HFlowContainer();
            _tabs.AddThemeConstantOverride("h_separation", 6);
            _tabs.AddThemeConstantOverride("v_separation", 6);
            if (layout != null)
            {
                layout.AddChild(_tabs);
                layout.MoveChild(_tabs, scroll.GetIndex());
            }
        }
        foreach (Node child in _tabs.GetChildren()) child.QueueFree();

        List<string> categories = _tree.AllNodes.Where(n => n != null).Select(DefaultCategory).Distinct()
            .OrderBy(c => { int i = System.Array.IndexOf(CategoryOrder, c); return i < 0 ? 99 : i; }).ToList();
        if (_category == null || !categories.Contains(_category)) _category = categories.FirstOrDefault();
        foreach (string cat in categories)
        {
            string c = cat;
            var tab = new Button { Text = cat, ToggleMode = true, ButtonPressed = cat == _category, FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0f, 38f) };
            tab.AddThemeFontSizeOverride("font_size", SmallFontSize + 2);
            StyleButton(tab, cat == _category ? AccentColor : LockedColor);
            tab.Pressed += () => { _category = c; BuildCards(); Refresh(); };
            _tabs.AddChild(tab);
        }
        BuildTree();
    }

    /// <summary>Selected tab: one row per tier, cards side by side (no connector lines).</summary>
    private void BuildTree()
    {
        foreach (Node child in Columns.GetChildren()) { Columns.RemoveChild(child); child.QueueFree(); }
        _cards.Clear();

        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", Spacing * 2);
        Columns.AddChild(rows);
        var nodes = _tree.AllNodes.Where(n => n != null && DefaultCategory(n) == _category).ToList();
        foreach (IGrouping<string, TechNode> branch in nodes.GroupBy(DefaultSubCategory)
                     .OrderBy(g => { int i = System.Array.IndexOf(SubCategoryOrder, g.Key); return g.Key.Length == 0 ? -1 : i < 0 ? 99 : i; }))
        {
            if (branch.Key.Length > 0)
            {
                rows.AddChild(MakeLabel(branch.Key.ToUpperInvariant(), TitleFontSize, AccentColor));
                rows.AddChild(new ColorRect { Color = LockedColor, CustomMinimumSize = new Vector2(0f, 2f) });
            }
            foreach (IGrouping<int, TechNode> tier in branch.GroupBy(n => n.Tier).OrderBy(g => g.Key))
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", Spacing);
                var cards = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                cards.AddThemeConstantOverride("h_separation", Spacing);
                cards.AddThemeConstantOverride("v_separation", Spacing);
                Label label = MakeLabel($"TIER {tier.Key}", SmallFontSize + 1, MutedTextColor);
                label.CustomMinimumSize = new Vector2(TierLabelWidth, 0f);
                label.VerticalAlignment = VerticalAlignment.Center;
                row.AddChild(label);
                row.AddChild(cards);
                foreach (TechNode node in tier.OrderBy(n => n.CostRP).ThenBy(n => n.NodeId)) cards.AddChild(MakeCard(node));
                rows.AddChild(row);
            }
        }
    }

    private Godot.Control TierHeader(int tier)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(MakeLabel($"TIER {tier}", SmallFontSize, MutedTextColor));
        var line = new ColorRect { Color = LockedColor, CustomMinimumSize = new Vector2(0f, 2f) };
        box.AddChild(line);
        return box;
    }

    private PanelContainer MakeCard(TechNode node)
    {
        _currentCardId = node.NodeId;
        StyleBoxFlat style = MakeBox(CardColor, Padding);
        style.SetBorderWidthAll(2);
        style.ShadowColor = new Color(0f, 0f, 0f, 0.35f);
        style.ShadowSize = 6;
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0f) };
        panel.AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);

        var titleRow = new HBoxContainer();
        Label title = MakeLabel(node.DisplayName.Length > 0 ? node.DisplayName : node.NodeId, TitleFontSize, TextColor, wrap: true);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        Label cost = MakeLabel($"{node.CostRP} RP", SmallFontSize, MutedTextColor);
        cost.VerticalAlignment = VerticalAlignment.Top;
        titleRow.AddChild(cost);
        box.AddChild(titleRow);

        if (node.PrerequisiteNodeIds.Length > 0)
        {
            box.AddChild(MakeLabel("Cần: " + string.Join(", ", node.PrerequisiteNodeIds.Select(PrereqName)),
                SmallFontSize, MutedTextColor, wrap: true));
        }
        if (node.UnlocksModelIds.Length > 0 || node.BuildWeightBonus > 0)
        {
            box.AddChild(new HSeparator());
            foreach (string modelId in node.UnlocksModelIds) box.AddChild(ModelRow(modelId));
            if (node.BuildWeightBonus > 0)
            {
                box.AddChild(MakeLabel($"+{node.BuildWeightBonus} max build weight", SmallFontSize, AccentColor));
            }
        }

        string nodeId = node.NodeId;
        var buy = new Button { CustomMinimumSize = new Vector2(0f, 34f), FocusMode = FocusModeEnum.None };
        buy.Pressed += () => TryPurchase(nodeId);
        box.AddChild(buy);

        _cards[nodeId] = new Card { Panel = panel, Style = style, BuyButton = buy, CostLabel = cost };
        return panel;
    }

    /// <summary>One unlocked part: icon (if any) and name from PartCatalog, or the prettified ModelId.</summary>
    private HBoxContainer ModelRow(string modelId)
    {
        ComponentDefinition def = PartCatalog.Instance?.GetDefinition(modelId);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        if (def?.Icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = def.Icon,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }
        else
        {
            row.AddChild(MakeLabel("▸", SmallFontSize, AccentColor));
        }
        string name = def != null && def.DisplayName.Length > 0 ? def.DisplayName : Prettify(modelId);
        Label label = MakeLabel(name, SmallFontSize, TextColor, wrap: true);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }

    // ---------- styles ----------

    private void ApplyStaticStyles()
    {
        if (Background != null) Background.Color = BackgroundColor;
        if (GetNodeOrNull<Label>("Layout/HeaderPanel/Header/TitleBox/Subtitle") is Label sub)
        {
            sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            sub.CustomMinimumSize = new Vector2(120f, 0f);
        }
        HeaderPanel?.AddThemeStyleboxOverride("panel", MakeBox(PanelColor, Padding));
        RPBadge?.AddThemeStyleboxOverride("panel", MakeBox(BackgroundColor, Padding / 2));
        WeightBadge?.AddThemeStyleboxOverride("panel", MakeBox(BackgroundColor, Padding / 2));
        RPLabel.AddThemeColorOverride("font_color", AccentColor);
        WeightLabel?.AddThemeColorOverride("font_color", TextColor);
        StatusLabel?.AddThemeColorOverride("font_color", MutedTextColor);
        StyleButton(BuildButton, AccentColor);
        BuildButton.FocusMode = FocusModeEnum.None;
    }

    private void StyleButton(Button button, Color color)
    {
        button.AddThemeStyleboxOverride("normal", MakeBox(color.Darkened(0.15f), 8));
        button.AddThemeStyleboxOverride("hover", MakeBox(color.Lightened(0.1f), 8));
        button.AddThemeStyleboxOverride("pressed", MakeBox(color.Darkened(0.3f), 8));
        button.AddThemeStyleboxOverride("disabled", MakeBox(CardColor.Lightened(0.06f), 8));
        button.AddThemeColorOverride("font_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
    }

    private StyleBoxFlat MakeBox(Color color, int padding)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(CornerRadius);
        box.SetContentMarginAll(padding);
        return box;
    }

    private Label MakeLabel(string text, int fontSize, Color color, bool wrap = false)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        if (wrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(CardWidth * 0.5f, 0f);
        }
        return label;
    }

    // ---------- helpers ----------

    /// <summary>"engine_lv2" -> "Engine LV2" until PartCatalog provides real names.</summary>
    private static string Prettify(string modelId)
    {
        IEnumerable<string> words = modelId.Split('_').Where(w => w.Length > 0).Select(w =>
            w.Any(char.IsDigit) ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(" ", words);
    }

    private string PrereqName(string nodeId)
    {
        TechNode pre = _tree?.GetNode(nodeId);
        string cat = DefaultCategory(pre);
        return pre != null && cat != DefaultCategory(_tree.GetNode(_currentCardId)) ? $"{NodeName(nodeId)} ({cat})" : NodeName(nodeId);
    }

    private string _currentCardId;

    private string NodeName(string nodeId)
    {
        TechNode node = _tree?.GetNode(nodeId);
        return node != null && node.DisplayName.Length > 0 ? node.DisplayName : nodeId;
    }

    private void SetStatus(string text)
    {
        if (StatusLabel != null) StatusLabel.Text = text;
        GD.Print($"[Research] {text}");
    }

    private bool Require(GodotObject value, string name) => Core.NodeGuard.Require(this, value, name);

    private void OnRPChanged(int newValue) => Refresh();

    private void OnNodeUnlocked(string nodeId)
    {
        BuildTree();
        Refresh();
    }

    private void OnBuildButtonPressed()
    {
        GD.Print("[Research] BuildRequested");
        EmitSignal(SignalName.BuildRequested);
    }
}

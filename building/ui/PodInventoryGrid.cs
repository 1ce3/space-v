using System.Collections.Generic;
using Godot;
using RocketGame.Core;

namespace RocketGame.Building;

/// <summary>
/// A11 (PHASE2_PLAN.md 2.3): the command pod inventory grid on the build screen. Drop a module from the palette on a cell to install it,
/// drag an installed module to move it, drag it off the grid or right-click it to remove it. While dragging, the cells under the
/// module turn green (fits) or red (does not fit).
/// </summary>
public partial class PodInventoryGrid : Godot.Control
{
    /// <summary>Drag data key: ModelId of the module being dragged.</summary>
    public const string ModuleKey = "pod_module";
    /// <summary>Drag data key: top-left cell of an installed module being moved (absent for palette drags).</summary>
    public const string FromCellKey = "pod_from_cell";

    [Export] public BuildingSystem Building { get; set; }
    [Export] public int CellPixels { get; set; } = 46;
    [Export] public Color GridLineColor { get; set; } = new(1f, 1f, 1f, 0.25f);
    [Export] public Color EmptyCellColor { get; set; } = new(1f, 1f, 1f, 0.05f);
    [Export] public Color FitColor { get; set; } = new(0.3f, 1f, 0.4f, 0.45f);
    [Export] public Color NoFitColor { get; set; } = new(1f, 0.3f, 0.3f, 0.45f);

    private Vector2I? _hoverCell;
    private Vector2I _hoverSize = Vector2I.One;
    private bool _hoverFits;
    private Vector2I? _draggingFrom;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        UpdateMinimumSize();
    }

    public override Vector2 _GetMinimumSize()
    {
        Vector2I size = Building?.InventorySize ?? Vector2I.Zero;
        return new Vector2(size.X * CellPixels + 1, size.Y * CellPixels + 1);
    }

    /// <summary>Redraws after the design or pod changed.</summary>
    public void Refresh()
    {
        UpdateMinimumSize();
        QueueRedraw();
    }

    /// <summary>Fill colour of a module by group, so the player sees what kind it is.</summary>
    public static Color GroupColor(ModuleSlotType group) => group switch
    {
        ModuleSlotType.Avionics => new Color(0.35f, 0.6f, 1f),
        ModuleSlotType.Thermal => new Color(1f, 0.55f, 0.25f),
        ModuleSlotType.Power => new Color(0.95f, 0.85f, 0.25f),
        ModuleSlotType.Sensor => new Color(0.35f, 0.85f, 0.5f),
        ModuleSlotType.Comms => new Color(0.75f, 0.45f, 1f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };

    /// <summary>Short label drawn on a module block.</summary>
    public static string ShortName(ModuleDefinition def) =>
        string.IsNullOrEmpty(def?.DisplayName) ? def?.ModelId ?? "" : def.DisplayName;

    public override void _Draw()
    {
        if (Building == null) return;
        Vector2I size = Building.InventorySize;
        if (size == Vector2I.Zero) return;
        Font font = ThemeDB.FallbackFont;
        int fontSize = Mathf.Max(9, CellPixels / 4);

        for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                DrawRect(CellRect(new Vector2I(x, y), Vector2I.One).Grow(-1f), EmptyCellColor);

        foreach (KeyValuePair<Vector2I, ModuleDefinition> pair in Building.InstalledModules)
        {
            if (_draggingFrom == pair.Key) continue; // drawn as the drag preview instead
            Rect2 rect = CellRect(pair.Key, PodInventory.SizeOf(pair.Value)).Grow(-3f);
            Color fill = GroupColor(pair.Value.SlotType);
            DrawRect(rect, fill * new Color(1f, 1f, 1f, 0.75f));
            DrawRect(rect, fill, false, 2f);
            DrawString(font, rect.Position + new Vector2(4f, fontSize + 3f), ShortName(pair.Value), HorizontalAlignment.Left,
                rect.Size.X - 8f, fontSize, Colors.Black);
        }

        if (_hoverCell != null)
            DrawRect(CellRect(_hoverCell.Value, _hoverSize).Grow(-1f), _hoverFits ? FitColor : NoFitColor);

        for (int x = 0; x <= size.X; x++)
            DrawLine(new Vector2(x * CellPixels, 0f), new Vector2(x * CellPixels, size.Y * CellPixels), GridLineColor);
        for (int y = 0; y <= size.Y; y++)
            DrawLine(new Vector2(0f, y * CellPixels), new Vector2(size.X * CellPixels, y * CellPixels), GridLineColor);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click) return;
        Vector2I? cell = CellAt(click.Position);
        if (cell == null || Building?.GetModuleCellAt(cell.Value) == null) return;
        Building.RemoveModule(cell.Value);
        AcceptEvent();
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        Vector2I? cell = CellAt(atPosition);
        Vector2I? anchor = cell == null ? null : Building?.GetModuleCellAt(cell.Value);
        if (anchor == null || !Building.InstalledModules.TryGetValue(anchor.Value, out ModuleDefinition def))
            return "Drag modules from the list here";
        return def.GetFormattedTooltip() + "\nDrag to move; drag out or right-click to remove";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        Vector2I? cell = CellAt(atPosition);
        Vector2I? anchor = cell == null ? null : Building?.GetModuleCellAt(cell.Value);
        if (anchor == null || !Building.InstalledModules.TryGetValue(anchor.Value, out ModuleDefinition def)) return default;
        _draggingFrom = anchor;
        SetDragPreview(MakeDragPreview(def, CellPixels));
        QueueRedraw();
        return new Godot.Collections.Dictionary { { ModuleKey, def.ModelId }, { FromCellKey, anchor.Value } };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadDrag(data, out string modelId, out Vector2I? from) || Building?.GetDefinitionOf(modelId) is not ModuleDefinition def)
            return false;
        Vector2I? cell = CellAt(atPosition);
        _hoverSize = PodInventory.SizeOf(def);
        _hoverCell = cell;
        _hoverFits = cell != null && (from != null ? Building.CanMoveModule(from.Value, cell.Value) : Building.CanInstallModule(modelId, cell.Value));
        QueueRedraw();
        return _hoverFits;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadDrag(data, out string modelId, out Vector2I? from)) return;
        Vector2I? cell = CellAt(atPosition);
        if (cell == null) return;
        if (from != null) Building.MoveModule(from.Value, cell.Value);
        else Building.InstallModule(modelId, cell.Value);
        ClearHover();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            // An installed module dragged somewhere that did not take it (off the grid): remove it.
            if (_draggingFrom != null && !GetViewport().GuiIsDragSuccessful()) Building?.RemoveModule(_draggingFrom.Value);
            _draggingFrom = null;
            ClearHover();
        }
        else if (what == NotificationMouseExit)
        {
            ClearHover();
        }
    }

    /// <summary>A coloured block the size of the module, shown under the mouse while dragging.</summary>
    public static Godot.Control MakeDragPreview(ModuleDefinition def, int cellPixels)
    {
        Vector2I size = PodInventory.SizeOf(def);
        var block = new ColorRect
        {
            Color = GroupColor(def.SlotType) * new Color(1f, 1f, 1f, 0.7f),
            Size = new Vector2(size.X * cellPixels - 6, size.Y * cellPixels - 6),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        block.AddChild(new Label { Text = ShortName(def), Position = new Vector2(4f, 2f), Modulate = Colors.Black });
        return block;
    }

    /// <summary>Reads ModelId (and the source cell for moves) from drag data.</summary>
    public static bool TryReadDrag(Variant data, out string modelId, out Vector2I? fromCell)
    {
        modelId = null;
        fromCell = null;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        Godot.Collections.Dictionary dict = data.AsGodotDictionary();
        if (!dict.ContainsKey(ModuleKey)) return false;
        modelId = dict[ModuleKey].AsString();
        if (dict.ContainsKey(FromCellKey)) fromCell = dict[FromCellKey].AsVector2I();
        return !string.IsNullOrEmpty(modelId);
    }

    private void ClearHover()
    {
        if (_hoverCell == null) return;
        _hoverCell = null;
        QueueRedraw();
    }

    private Rect2 CellRect(Vector2I cell, Vector2I size) =>
        new(cell.X * CellPixels, cell.Y * CellPixels, size.X * CellPixels, size.Y * CellPixels);

    private Vector2I? CellAt(Vector2 position)
    {
        Vector2I size = Building?.InventorySize ?? Vector2I.Zero;
        var cell = new Vector2I(Mathf.FloorToInt(position.X / CellPixels), Mathf.FloorToInt(position.Y / CellPixels));
        return cell.X >= 0 && cell.Y >= 0 && cell.X < size.X && cell.Y < size.Y ? cell : null;
    }
}

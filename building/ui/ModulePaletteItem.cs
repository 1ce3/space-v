using Godot;

namespace RocketGame.Building;

/// <summary>A11: one unlocked module under the pod grid. Drag it onto the grid to install; click installs at the first free cell.</summary>
public partial class ModulePaletteItem : Button
{
    public ModuleDefinition Module { get; private set; }
    private int _cellPixels = 46;

    /// <summary>Sets the module, its label, tooltip and whether it can be afforded and placed.</summary>
    public void Setup(ModuleDefinition module, bool available, int cellPixels)
    {
        Module = module;
        _cellPixels = cellPixels;
        Vector2I size = PodInventory.SizeOf(module);
        Text = $"{PodInventoryGrid.ShortName(module)}  {size.X}x{size.Y}  w{module.BuildWeight}";
        TooltipText = module.GetFormattedTooltip() + "\nDrag onto the grid, or click to place in the first free cell";
        Alignment = HorizontalAlignment.Left;
        FocusMode = FocusModeEnum.None;
        Disabled = !available;
        Modulate = new Color(1f, 1f, 1f, available ? 1f : 0.45f);
        AddThemeColorOverride("font_color", PodInventoryGrid.GroupColor(module.SlotType).Lightened(0.3f));
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Module == null || Disabled) return default;
        SetDragPreview(PodInventoryGrid.MakeDragPreview(Module, _cellPixels));
        return new Godot.Collections.Dictionary { { PodInventoryGrid.ModuleKey, Module.ModelId } };
    }
}

using System.Collections.Generic;
using Godot;

namespace RocketGame.Building;

/// <summary>
/// Phase 2 (PHASE2_PLAN.md 2.2): rectangle packing rules of the command pod inventory grid, shared by the build screen,
/// the assembler and CommandPodPart. Cell (0,0) is the top-left; Size = (columns, rows).
/// </summary>
public static class PodInventory
{
    /// <summary>Marks a design module entry whose cell is unknown (designs saved before phase 2): placed at the first free cell.</summary>
    public static readonly Vector2I Unplaced = new(-1, -1);

    /// <summary>One placed rectangle.</summary>
    public readonly record struct Item(Vector2I Cell, Vector2I Size);

    /// <summary>True when a rectangle at cell lies inside an inventory of this size.</summary>
    public static bool InBounds(Vector2I inventorySize, Vector2I cell, Vector2I size) =>
        cell.X >= 0 && cell.Y >= 0 && size.X > 0 && size.Y > 0 &&
        cell.X + size.X <= inventorySize.X && cell.Y + size.Y <= inventorySize.Y;

    /// <summary>True when two rectangles share a cell.</summary>
    public static bool Overlaps(Item a, Item b) =>
        a.Cell.X < b.Cell.X + b.Size.X && b.Cell.X < a.Cell.X + a.Size.X &&
        a.Cell.Y < b.Cell.Y + b.Size.Y && b.Cell.Y < a.Cell.Y + a.Size.Y;

    /// <summary>True when the rectangle is inside the grid and overlaps none of the occupied items.</summary>
    public static bool Fits(Vector2I inventorySize, IEnumerable<Item> occupied, Vector2I cell, Vector2I size)
    {
        if (!InBounds(inventorySize, cell, size)) return false;
        var item = new Item(cell, size);
        foreach (Item other in occupied)
            if (Overlaps(item, other)) return false;
        return true;
    }

    /// <summary>First free cell (row by row from the top-left) for a rectangle, or null when it fits nowhere.</summary>
    public static Vector2I? FindFreeCell(Vector2I inventorySize, IEnumerable<Item> occupied, Vector2I size)
    {
        var items = new List<Item>(occupied);
        for (int y = 0; y < inventorySize.Y; y++)
            for (int x = 0; x < inventorySize.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (Fits(inventorySize, items, cell, size)) return cell;
            }
        return null;
    }

    /// <summary>The item covering a cell, or null.</summary>
    public static Item? ItemAt(IEnumerable<Item> occupied, Vector2I cell)
    {
        foreach (Item item in occupied)
            if (Overlaps(item, new Item(cell, Vector2I.One))) return item;
        return null;
    }

    /// <summary>Grid size of a module definition (at least 1x1).</summary>
    public static Vector2I SizeOf(ModuleDefinition def) =>
        def == null ? Vector2I.One : new Vector2I(Mathf.Max(1, def.GridSize.X), Mathf.Max(1, def.GridSize.Y));
}

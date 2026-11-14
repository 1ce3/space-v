using System.Collections.Generic;
using Godot;
using RocketGame.Core;
using Side = RocketGame.Core.Side;

namespace RocketGame.Building;

/// <summary>Occupancy grid of the build screen and the neighbor rules (A_BUILDING.md 5.1).</summary>
public class BuildGrid
{
    public int Width { get; }
    public int Height { get; }
    /// <summary>Cell size (m).</summary>
    public float CellSize { get; }

    private readonly Dictionary<Vector2I, RocketPart> _cells = new();
    private readonly Dictionary<Edge, DecouplerPart> _decouplers = new();

    private static readonly Side[] AllSides = { Side.Top, Side.Bottom, Side.Left, Side.Right };

    public BuildGrid(int width, int height, float cellSize)
    {
        Width = width;
        Height = height;
        CellSize = cellSize;
    }

    /// <summary>Every part on the grid (each once).</summary>
    public List<RocketPart> Parts
    {
        get
        {
            var result = new List<RocketPart>();
            foreach (RocketPart part in _cells.Values)
                if (!result.Contains(part)) result.Add(part);
            return result;
        }
    }

    /// <summary>Every decoupler on the grid.</summary>
    public List<DecouplerPart> Decouplers => new(_decouplers.Values);

    /// <summary>True when the cell is inside the grid.</summary>
    public bool IsInside(Vector2I cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    /// <summary>True when def fits at cell and every touching neighbor allows it both ways.</summary>
    public bool CanPlace(PartDefinition def, Vector2I cell)
    {
        if (def == null) return false;

        foreach (Vector2I c in GetFootprintCells(cell, def.Footprint))
            if (!IsInside(c) || _cells.ContainsKey(c)) return false;

        foreach (Vector2I c in GetFootprintCells(cell, def.Footprint))
        {
            foreach (Side side in AllSides)
            {
                Vector2I next = c + Offset(side);
                if (IsInFootprint(next, cell, def.Footprint)) continue;
                RocketPart neighbor = GetPartAt(next);
                if (neighbor?.Definition == null) continue;
                if (!def.Allows(side, neighbor.PartGroup)) return false;
                if (!neighbor.Definition.Allows(Opposite(side), def.PartGroup)) return false;
            }
        }
        return true;
    }

    /// <summary>True when the edge separates two different touching parts and has no decoupler yet.</summary>
    public bool CanPlaceDecoupler(PartDefinition def, Edge edge)
    {
        if (def is not DecouplerPartDefinition) return false;
        if (!TryGetEdgeParts(edge, out RocketPart a, out RocketPart b, out Edge normalized)) return false;
        if (_decouplers.ContainsKey(normalized)) return false;

        // a is below/left of b: a's Top/Right side touches b.
        Side side = normalized.Side;
        return a.Definition.Allows(side, b.PartGroup) && b.Definition.Allows(Opposite(side), a.PartGroup);
    }

    /// <summary>Marks the footprint of part as occupied.</summary>
    public void Occupy(RocketPart part)
    {
        if (part == null) return;
        foreach (Vector2I c in GetFootprintCells(part.Cell, part.Footprint))
            _cells[c] = part;
    }

    /// <summary>Frees the footprint of part.</summary>
    public void Vacate(RocketPart part)
    {
        if (part == null) return;
        foreach (Vector2I c in GetFootprintCells(part.Cell, part.Footprint))
            if (_cells.TryGetValue(c, out RocketPart current) && current == part)
                _cells.Remove(c);
    }

    /// <summary>Puts a decoupler on an edge (normalized; sets decoupler.Edge). False when the edge is taken.</summary>
    public bool AddDecoupler(DecouplerPart decoupler, Edge edge)
    {
        if (decoupler == null) return false;
        Edge normalized = Normalize(edge);
        if (_decouplers.ContainsKey(normalized)) return false;
        decoupler.Edge = normalized;
        _decouplers[normalized] = decoupler;
        return true;
    }

    /// <summary>Removes and returns the decoupler on an edge, or null.</summary>
    public DecouplerPart RemoveDecoupler(Edge edge)
    {
        Edge normalized = Normalize(edge);
        if (!_decouplers.TryGetValue(normalized, out DecouplerPart decoupler)) return null;
        _decouplers.Remove(normalized);
        return decoupler;
    }

    /// <summary>Decoupler on an edge, or null.</summary>
    public DecouplerPart GetDecouplerAt(Edge edge) =>
        _decouplers.TryGetValue(Normalize(edge), out DecouplerPart decoupler) ? decoupler : null;

    /// <summary>Decouplers on the contact edges between a and b.</summary>
    public List<DecouplerPart> GetDecouplersBetween(RocketPart a, RocketPart b)
    {
        var result = new List<DecouplerPart>();
        foreach (Edge edge in GetContactEdges(a, b))
            if (_decouplers.TryGetValue(edge, out DecouplerPart decoupler)) result.Add(decoupler);
        return result;
    }

    /// <summary>Decouplers that no longer sit between two different parts (after a remove or move).</summary>
    public List<DecouplerPart> GetOrphanDecouplers()
    {
        var result = new List<DecouplerPart>();
        foreach (KeyValuePair<Edge, DecouplerPart> pair in _decouplers)
            if (!TryGetEdgeParts(pair.Key, out _, out _, out _)) result.Add(pair.Value);
        return result;
    }

    /// <summary>Empties the grid (parts and decouplers are not freed).</summary>
    public void Clear()
    {
        _cells.Clear();
        _decouplers.Clear();
    }

    /// <summary>Part covering cell, or null.</summary>
    public RocketPart GetPartAt(Vector2I cell) => _cells.TryGetValue(cell, out RocketPart part) ? part : null;

    /// <summary>Parts sharing at least one edge with part.</summary>
    public List<RocketPart> GetNeighbors(RocketPart part)
    {
        var result = new List<RocketPart>();
        if (part == null) return result;
        foreach (Vector2I c in GetFootprintCells(part.Cell, part.Footprint))
        {
            foreach (Side side in AllSides)
            {
                RocketPart other = GetPartAt(c + Offset(side));
                if (other != null && other != part && !result.Contains(other)) result.Add(other);
            }
        }
        return result;
    }

    /// <summary>Edges where a and b touch, normalized to Top or Right of the lower/left cell.</summary>
    public List<Edge> GetContactEdges(RocketPart a, RocketPart b)
    {
        var result = new List<Edge>();
        if (a == null || b == null || a == b) return result;
        foreach (Vector2I c in GetFootprintCells(a.Cell, a.Footprint))
        {
            foreach (Side side in AllSides)
            {
                if (!IsInFootprint(c + Offset(side), b.Cell, b.Footprint)) continue;
                Edge edge = Normalize(new Edge(c, side));
                if (!result.Contains(edge)) result.Add(edge);
            }
        }
        return result;
    }

    /// <summary>One representation per edge: Bottom/Left become Top/Right of the neighbor cell. Grid Y grows upward (not Godot 2D convention).</summary>
    public static Edge Normalize(Edge edge) => edge.Side switch
    {
        Side.Bottom => new Edge(edge.Cell + new Vector2I(0, -1), Side.Top),
        Side.Left => new Edge(edge.Cell + Vector2I.Left, Side.Right),
        _ => edge,
    };

    /// <summary>Cell on the other side of a normalized edge (above for Top, right for Right).</summary>
    public static Vector2I GetOtherCell(Edge normalizedEdge) => normalizedEdge.Cell + Offset(normalizedEdge.Side);

    /// <summary>Neighbor cell direction of a side. Y grows upward.</summary>
    public static Vector2I Offset(Side side) => side switch
    {
        Side.Top => new Vector2I(0, 1),
        Side.Bottom => new Vector2I(0, -1),
        Side.Left => new Vector2I(-1, 0),
        _ => new Vector2I(1, 0),
    };

    public static Side Opposite(Side side) => side switch
    {
        Side.Top => Side.Bottom,
        Side.Bottom => Side.Top,
        Side.Left => Side.Right,
        _ => Side.Left,
    };

    /// <summary>Every cell from cell to cell + footprint - (1,1).</summary>
    public static List<Vector2I> GetFootprintCells(Vector2I cell, Vector2I footprint)
    {
        var result = new List<Vector2I>();
        for (int x = 0; x < Mathf.Max(1, footprint.X); x++)
            for (int y = 0; y < Mathf.Max(1, footprint.Y); y++)
                result.Add(cell + new Vector2I(x, y));
        return result;
    }

    private static bool IsInFootprint(Vector2I c, Vector2I cell, Vector2I footprint) =>
        c.X >= cell.X && c.Y >= cell.Y && c.X < cell.X + Mathf.Max(1, footprint.X) && c.Y < cell.Y + Mathf.Max(1, footprint.Y);

    /// <summary>The two different parts on both sides of an edge (a = lower/left).</summary>
    private bool TryGetEdgeParts(Edge edge, out RocketPart a, out RocketPart b, out Edge normalized)
    {
        normalized = Normalize(edge);
        a = GetPartAt(normalized.Cell);
        b = GetPartAt(GetOtherCell(normalized));
        return a?.Definition != null && b?.Definition != null && a != b;
    }
}

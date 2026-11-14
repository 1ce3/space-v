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

    public BuildGrid(int width, int height, float cellSize)
    {
        Width = width;
        Height = height;
        CellSize = cellSize;
    }

    /// <summary>True when def fits at cell and every touching neighbor allows it both ways.</summary>
    public bool CanPlace(PartDefinition def, Vector2I cell)
    {
        // TODO(A3)
        return false;
    }

    /// <summary>True when the edge separates two different touching parts and has no decoupler yet.</summary>
    public bool CanPlaceDecoupler(PartDefinition def, Edge edge)
    {
        // TODO(A3)
        return false;
    }

    /// <summary>Marks the footprint of part as occupied.</summary>
    public void Occupy(RocketPart part)
    {
        // TODO(A3)
    }

    /// <summary>Frees the footprint of part.</summary>
    public void Vacate(RocketPart part)
    {
        // TODO(A3)
    }

    /// <summary>Part covering cell, or null.</summary>
    public RocketPart GetPartAt(Vector2I cell) => _cells.TryGetValue(cell, out RocketPart part) ? part : null;

    /// <summary>Parts sharing at least one edge with part.</summary>
    public List<RocketPart> GetNeighbors(RocketPart part)
    {
        // TODO(A3)
        return new List<RocketPart>();
    }

    /// <summary>Edges where a and b touch, normalized to Top or Right of the lower/left cell.</summary>
    public List<Edge> GetContactEdges(RocketPart a, RocketPart b)
    {
        // TODO(A3)
        return new List<Edge>();
    }

    /// <summary>One representation per edge: Bottom/Left become Top/Right of the neighbor cell. Grid Y grows upward (not Godot 2D convention).</summary>
    public static Edge Normalize(Edge edge) => edge.Side switch
    {
        Side.Bottom => new Edge(edge.Cell + new Vector2I(0, -1), Side.Top),
        Side.Left => new Edge(edge.Cell + Vector2I.Left, Side.Right),
        _ => edge,
    };
}

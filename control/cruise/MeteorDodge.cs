using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;
using RocketGame.Physics;

namespace RocketGame.Control;

/// <summary>
/// Real-time meteor dodge in the flight scene during cruise (PHASE2_PLAN 5.6, owner C). The vehicle stays frozen for
/// the physics engine; this node moves it as a scripted body in the X-Y plane:
/// A/D rotate, Shift main-engine burst (forward), Q/E RCS strafe (needs a working RCS module).
/// Meteors come from one side after the warning time; every part a meteor touches is passed to MeteorImpact.
/// Emits Finished(hits) when the wave is over; the caller puts the vehicle back.
/// </summary>
public partial class MeteorDodge : Node3D
{
    [Signal] public delegate void FinishedEventHandler(int hits, string report);

    public VehicleRoot Vehicle { get; set; }
    public float WarningSeconds { get; set; } = 6f;
    public bool AutoDodge { get; set; }
    public bool HasRcs { get; set; }

    public int MeteorCount { get; set; } = 7;
    public float MeteorSpeedMps { get; set; } = 32f;
    public float MeteorRadiusM { get; set; } = 1.6f;
    public float MeteorSpacingSeconds { get; set; } = 1.1f;
    public float PartRadiusM { get; set; } = 1.4f;
    public float TurnRateDeg { get; set; } = 70f;
    public float MainAccel { get; set; } = 12f;
    public float RcsAccel { get; set; } = 6f;
    public float Damping { get; set; } = 0.4f;

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<(MeshInstance3D Node, Vector3 Velocity)> _meteors = new();
    private readonly List<(float Time, Vector3 Start, Vector3 Velocity)> _schedule = new();
    private Transform3D _start;
    private Vector3 _pos;
    private Vector3 _vel;
    private float _angleDeg;
    private float _time;
    private int _hits;
    private readonly List<string> _report = new();
    private Label _hud;
    private CanvasLayer _layer;
    private bool _done;
    private Vector3 _from;

    public override void _Ready()
    {
        _rng.Randomize();
        _start = Vehicle.GlobalTransform;
        _pos = _start.Origin;
        _angleDeg = Mathf.RadToDeg(_start.Basis.GetEuler().Z);

        // Meteors fly in from one side, aimed across the whole rocket so the player has to move.
        float side = _rng.Randf() < 0.5f ? -1f : 1f;
        float slant = _rng.RandfRange(-0.35f, 0.35f);
        _from = new Vector3(side, slant, 0f).Normalized();
        Vector3 across = new Vector3(-_from.Y, _from.X, 0f);
        float height = Mathf.Max(8f, Vehicle.GetHeightM());
        Vector3 center = Vehicle.GetWorldCenterOfMass();
        float dist = MeteorSpeedMps * WarningSeconds;
        for (int i = 0; i < MeteorCount; i++)
        {
            float offset = _rng.RandfRange(-0.6f, 0.6f) * height;
            Vector3 start = center + _from * dist + across * offset + _from * (i * MeteorSpacingSeconds * MeteorSpeedMps);
            _schedule.Add((0f, start, -_from * MeteorSpeedMps * _rng.RandfRange(0.9f, 1.15f)));
        }

        _layer = new CanvasLayer { Layer = 20 };
        AddChild(_layer);
        _hud = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _hud.AddThemeFontSizeOverride("font_size", 22);
        _hud.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hud.AddThemeConstantOverride("outline_size", 6);
        _layer.AddChild(_hud);
        _hud.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.TopWide);
        _hud.Position = new Vector2(0f, 24f);

        foreach (var s in _schedule) SpawnMeteor(s.Start, s.Velocity);
    }

    private void SpawnMeteor(Vector3 at, Vector3 velocity)
    {
        var mesh = new SphereMesh { Radius = MeteorRadiusM, Height = MeteorRadiusM * 1.7f, RadialSegments = 10, Rings = 6 };
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.36f, 0.3f), Roughness = 1f, EmissionEnabled = true, Emission = new Color(0.25f, 0.08f, 0f) };
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, RotationDegrees = new Vector3(_rng.Randf() * 360f, _rng.Randf() * 360f, 0f) };
        AddChild(node);
        node.GlobalPosition = at;
        _meteors.Add((node, velocity));
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        float dt = (float)delta;
        _time += dt;

        // --- player / autopilot control (2D: X-Y plane, angle around Z) ---
        float turn = 0f, thrust = 0f, strafe = 0f;
        if (AutoDodge) AutoSteer(ref strafe);
        else
        {
            if (Input.IsPhysicalKeyPressed(Key.A)) turn += 1f;
            if (Input.IsPhysicalKeyPressed(Key.D)) turn -= 1f;
            if (Input.IsPhysicalKeyPressed(Key.Shift) || Input.IsPhysicalKeyPressed(Key.W)) thrust = 1f;
            if (HasRcs)
            {
                if (Input.IsPhysicalKeyPressed(Key.Q)) strafe -= 1f;
                if (Input.IsPhysicalKeyPressed(Key.E)) strafe += 1f;
            }
        }
        _angleDeg += turn * TurnRateDeg * dt;
        float a = Mathf.DegToRad(_angleDeg);
        var forward = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f); // nose direction (rocket +Y)
        var right = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        _vel += (forward * thrust * MainAccel + right * strafe * RcsAccel) * dt;
        _vel *= 1f - Mathf.Clamp(Damping * dt, 0f, 1f);
        _pos += _vel * dt;
        Vehicle.ScriptedTransform = new Transform3D(new Basis(Vector3.Back, a), _pos);

        // --- meteors ---
        int alive = 0;
        for (int i = 0; i < _meteors.Count; i++)
        {
            var (node, v) = _meteors[i];
            if (node == null || !IsInstanceValid(node)) continue;
            node.GlobalPosition += v * dt;
            node.RotateX(dt * 1.3f);
            alive++;
            if (CheckHit(node, v.Length()))
            {
                node.QueueFree();
                _meteors[i] = (null, v);
            }
            else if ((node.GlobalPosition - _pos).Dot(_from) < -150f)
            {
                node.QueueFree(); // passed by
                _meteors[i] = (null, v);
            }
        }

        float firstArrival = WarningSeconds - _time;
        _hud.Text = firstArrival > 0f
            ? $"⚠ METEOROIDS from the {(_from.X > 0 ? "RIGHT" : "LEFT")} in {firstArrival:0.0} s\n" + Controls()
            : $"DODGE THE METEOROIDS!  {alive} left   hits {_hits}\n" + Controls();
        if (alive == 0) Finish();
    }

    private string Controls() => AutoDodge
        ? "Antenna + RCS dodging automatically..."
        : "A/D rotate · Shift/W main engine" + (HasRcs ? " · Q/E RCS sideways" : " · (no RCS)");

    /// <summary>Strafes away from the nearest meteor that will pass close (auto-dodge from comms + RCS).</summary>
    private void AutoSteer(ref float strafe)
    {
        float a = Mathf.DegToRad(_angleDeg);
        var right = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        foreach (var (node, v) in _meteors)
        {
            if (node == null || !IsInstanceValid(node)) continue;
            Vector3 rel = node.GlobalPosition - Vehicle.GetWorldCenterOfMass();
            Vector3 dir = v.Normalized();
            float along = -rel.Dot(dir);
            if (along < 0f) continue;
            Vector3 miss = rel + dir * along;
            if (miss.Length() > Vehicle.GetHeightM() * 0.6f + MeteorRadiusM) continue;
            strafe = miss.Dot(right) > 0f ? -1f : 1f;
            return;
        }
    }

    private bool CheckHit(MeshInstance3D meteor, float speed)
    {
        if (Vehicle.PartTree == null) return false;
        float cell = RocketPart.GetCellSize();
        foreach (RocketPart part in Vehicle.PartTree.ActiveParts)
        {
            if (part is DecouplerPart) continue;
            Vector3 c = part.GlobalTransform * new Vector3(part.Footprint.X * cell * 0.5f, part.Footprint.Y * cell * 0.5f, 0f);
            float r = PartRadiusM * Mathf.Max(1f, Mathf.Max(part.Footprint.X, part.Footprint.Y) * 0.6f);
            if (meteor.GlobalPosition.DistanceTo(c) > r + MeteorRadiusM) continue;
            _hits++;
            string name = string.IsNullOrEmpty(part.Definition?.DisplayName) ? part.ModelId : part.Definition.DisplayName;
            bool damaged = MeteorImpact.Resolve(part, speed);
            _report.Add(damaged ? $"{name} damaged" : $"{name} held");
            RocketGame.Presentation.ExplosionEffect.Spawn(this, meteor.GlobalPosition, 0.4f);
            return true;
        }
        return false;
    }

    private void Finish()
    {
        if (_done) return;
        _done = true;
        Vehicle.ScriptedTransform = null;
        string report = _hits == 0 ? "Dodged the whole meteoroid field!" : $"Hit {_hits} time(s): {string.Join(", ", _report)}";
        EmitSignal(SignalName.Finished, _hits, report);
        QueueFree();
    }
}

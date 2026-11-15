using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Command pod: RCS puffs, solar panels / radiator when deployed, optional robot payload.</summary>
public partial class PodVisual : PartVisual
{
    [ExportGroup("RCS")]
    [Export] public Color RcsColor { get; set; } = new(0.9f, 0.95f, 1f, 0.8f);
    /// <summary>Command magnitude (0..1) under which no puff is shown.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RcsThreshold { get; set; } = 0.05f;
    [Export] public float RcsPuffSizeM { get; set; } = 0.5f;

    [ExportGroup("Deployables")]
    [Export] public Color SolarColor { get; set; } = new(0.15f, 0.25f, 0.6f);
    [Export] public Color RadiatorColor { get; set; } = new(0.85f, 0.85f, 0.8f);
    /// <summary>Length of one solar wing (m).</summary>
    [Export] public float SolarWingLengthM { get; set; } = 3f;
    [Export] public float RadiatorLengthM { get; set; } = 1.5f;

    [ExportGroup("Payload")]
    /// <summary>Shows the payload scene when one is given (the robot rides inside the pod).</summary>
    [Export] public bool ShowPayloadModel { get; set; } = true;

    public bool SolarVisible => _solar != null && _solar.Visible;
    public bool RadiatorVisible => _radiator != null && _radiator.Visible;

    private Node3D _solar;
    private Node3D _radiator;
    private MeshInstance3D _rcsLeft;
    private MeshInstance3D _rcsRight;
    private Node3D _payload;

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (Part is not CommandPodPart pod) return;

        bool solar = false, radiator = false;
        float torque = 0f, translate = 0f;
        foreach (PodModule module in pod.Modules)
        {
            switch (module)
            {
                case SolarArrayModule s when s.IsDeployed && !s.IsBrokenByAero:
                    solar = true;
                    break;
                case ActiveRadiatorModule r when r.IsPanelExtended:
                    radiator = true;
                    break;
                case RcsThrusterModule rcs when rcs.IsEnabled:
                    float max = rcs.GetMaxTorque();
                    if (max > 0f) torque += rcs.CommandedTorque / max;
                    translate += rcs.TranslateCommand;
                    break;
            }
        }
        UpdateDeployables(solar, radiator);
        UpdateRcs(Mathf.Clamp(torque, -1f, 1f), Mathf.Clamp(translate, -1f, 1f));
    }

    /// <summary>Puffs on the side the thrusters push from. torqueCommand and translateCommand are -1..1.</summary>
    public void UpdateRcs(float torqueCommand, float translateCommand)
    {
        float left = Mathf.Max(0f, Mathf.Max(torqueCommand, translateCommand));
        float right = Mathf.Max(0f, Mathf.Max(-torqueCommand, -translateCommand));
        if (left < RcsThreshold && right < RcsThreshold)
        {
            if (_rcsLeft != null) _rcsLeft.Visible = false;
            if (_rcsRight != null) _rcsRight.Visible = false;
            return;
        }
        EnsureRcs();
        SetPuff(_rcsLeft, left);
        SetPuff(_rcsRight, right);
    }

    public void UpdateDeployables(bool solarDeployed, bool radiatorExtended)
    {
        if (solarDeployed || _solar != null)
        {
            _solar ??= BuildSolar();
            _solar.Visible = solarDeployed;
        }
        if (radiatorExtended || _radiator != null)
        {
            _radiator ??= BuildRadiator();
            _radiator.Visible = radiatorExtended;
        }
    }

    /// <summary>Puts the payload model (e.g. the robot) in the pod; null removes it.</summary>
    public void ShowPayload(PackedScene payloadScene)
    {
        _payload?.QueueFree();
        _payload = null;
        if (payloadScene == null) return;
        _payload = payloadScene.Instantiate<Node3D>();
        _payload.Name = "Payload";
        _payload.Visible = ShowPayloadModel;
        AddChild(_payload);
    }

    private void SetPuff(MeshInstance3D puff, float strength)
    {
        bool on = strength >= RcsThreshold;
        puff.Visible = on;
        if (!on) return;
        float s = RcsPuffSizeM * Mathf.Lerp(0.5f, 1f, strength);
        puff.Scale = new Vector3(s * 1.6f, s, s);
    }

    private void EnsureRcs()
    {
        if (_rcsLeft != null) return;
        float half = RocketPart.GetCellSize() * 0.5f;
        _rcsLeft = MakePuff("RcsLeft", new Vector3(-half - RcsPuffSizeM, half * 0.4f, 0f));
        _rcsRight = MakePuff("RcsRight", new Vector3(half + RcsPuffSizeM, half * 0.4f, 0f));
    }

    private MeshInstance3D MakePuff(string name, Vector3 position)
    {
        var puff = new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = MakeGlowMaterial(RcsColor),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = position,
            Visible = false,
        };
        AddChild(puff);
        return puff;
    }

    private Node3D BuildSolar()
    {
        var root = new Node3D { Name = "SolarWings" };
        float half = RocketPart.GetCellSize() * 0.5f;
        var material = new StandardMaterial3D { AlbedoColor = SolarColor, Metallic = 0.4f, Roughness = 0.3f };
        var mesh = new BoxMesh { Size = new Vector3(SolarWingLengthM, half * 0.8f, 0.05f), Material = material };
        root.AddChild(new MeshInstance3D { Name = "Left", Mesh = mesh, Position = new Vector3(-half - SolarWingLengthM * 0.5f, 0f, 0f) });
        root.AddChild(new MeshInstance3D { Name = "Right", Mesh = mesh, Position = new Vector3(half + SolarWingLengthM * 0.5f, 0f, 0f) });
        AddChild(root);
        return root;
    }

    private Node3D BuildRadiator()
    {
        float half = RocketPart.GetCellSize() * 0.5f;
        var material = new StandardMaterial3D { AlbedoColor = RadiatorColor, Roughness = 0.5f };
        var panel = new MeshInstance3D
        {
            Name = "Radiator",
            Mesh = new BoxMesh { Size = new Vector3(0.05f, RadiatorLengthM, half), Material = material },
            Position = new Vector3(0f, half + RadiatorLengthM * 0.5f, -half * 0.6f),
        };
        AddChild(panel);
        return panel;
    }
}

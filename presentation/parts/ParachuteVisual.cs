using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Presentation;

/// <summary>Canopy grows open when the chute deploys, sways a little, and disappears when cut.</summary>
public partial class ParachuteVisual : PartVisual
{
    /// <summary>Seconds from Stowed to fully open.</summary>
    [Export] public float OpenTimeS { get; set; } = 1.2f;
    /// <summary>Canopy scale when it starts opening (fraction of full).</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float OpenStartScale { get; set; } = 0.15f;
    [Export] public Color CanopyColor { get; set; } = new(1f, 0.55f, 0.1f);
    /// <summary>Sway amplitude (degrees) and frequency (Hz) of the open canopy.</summary>
    [Export] public float SwayDeg { get; set; } = 4f;
    [Export] public float SwayHz { get; set; } = 0.4f;

    /// <summary>0 = stowed, 1 = fully open.</summary>
    public float OpenFraction { get; private set; }
    public bool CanopyVisible => _canopy != null && _canopy.Visible;

    private Node3D _canopy;
    private Node3D _attach;
    private Vector3 _canopyScale = Vector3.One;
    private ParachuteState _state = ParachuteState.Stowed;
    private float _time;

    public override void Bind(RocketPart part)
    {
        base.Bind(part);
        _canopy = FindAnchor("Canopy");
        _attach = FindAnchor("ChuteAttach");
        if (_canopy != null)
        {
            _canopyScale = _canopy.Scale;
            if (_canopy is MeshInstance3D mesh)
                mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = CanopyColor, Roughness = 0.8f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
            _canopy.Visible = false;
        }
    }

    protected override void UpdateVisual(float deltaTime)
    {
        base.UpdateVisual(deltaTime);
        if (Part is not ParachutePart chute) return;
        if (chute.ChuteState != _state) UpdateChute(chute.ChuteState);
        if (_state != ParachuteState.Deployed || _canopy == null) return;

        _time += deltaTime;
        OpenFraction = OpenTimeS > 0f ? Mathf.Min(1f, OpenFraction + deltaTime / OpenTimeS) : 1f;
        float eased = 1f - (1f - OpenFraction) * (1f - OpenFraction);
        float s = Mathf.Lerp(OpenStartScale, 1f, eased);
        _canopy.Scale = new Vector3(_canopyScale.X * s, _canopyScale.Y * Mathf.Lerp(OpenStartScale, 1f, OpenFraction), _canopyScale.Z * s);
        if (_attach != null)
            _attach.RotationDegrees = new Vector3(0f, 0f, SwayDeg * OpenFraction * Mathf.Sin(_time * Mathf.Tau * SwayHz));
    }

    /// <summary>Applies a chute state: Stowed hides, Deployed starts opening, Cut hides.</summary>
    public void UpdateChute(ParachuteState state)
    {
        _state = state;
        if (_canopy == null) return;
        switch (state)
        {
            case ParachuteState.Deployed:
                OpenFraction = 0f;
                _time = 0f;
                _canopy.Scale = _canopyScale * OpenStartScale;
                _canopy.Visible = true;
                break;
            default:
                OpenFraction = 0f;
                _canopy.Visible = false;
                if (_attach != null) _attach.RotationDegrees = Vector3.Zero;
                break;
        }
    }
}

using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using RocketGame.Core;

namespace RocketGame.Physics;

/// <summary>Drag, fin lift and parachute drag summed over parts (guide B 4.3.2). Vehicle-local frame. Also used by WindTunnelAnalyzer (A).</summary>
public class AerodynamicSolver
{
    /// <summary>Dynamic pressure warning limit (Pa).</summary>
    public float MaxQLimit { get; set; }
    /// <summary>A chute opened above its limits tore: (chute, q in Pa, Mach). Control can show a warning.</summary>
    public event System.Action<ParachutePart, float, float> ParachuteTorn;

    private const float MinSpeedMps = 0.01f;
    private readonly PhysicsSettings _settings;
    private readonly HashSet<ParachutePart> _checkedChutes = new();

    public AerodynamicSolver(PhysicsSettings settings)
    {
        _settings = settings;
        MaxQLimit = settings?.MaxQLimit ?? 40000f;
    }

    /// <summary>Forget which parachutes were already checked (call when a new segment starts).</summary>
    public void Reset() => _checkedChutes.Clear();

    /// <summary>
    /// Aero force and torque on the parts. airVelBody = air velocity relative to the vehicle in the vehicle-local frame (= -velocity), m/s.
    /// com is the local center of mass (m). partLoads (optional) receives the aero force of each part.
    /// </summary>
    public AeroResult Solve(PartTree parts, Vector2 airVelBody, AtmosphereSample air, Vector2 com, List<PartLoad> partLoads = null)
    {
        float speed = airVelBody.Length();
        float q = CalculateDynamicPressure(air.Density, speed);
        float mach = air.SpeedOfSound > 0f ? speed / air.SpeedOfSound : 0f;
        var result = new AeroResult { CenterOfPressure = com, DynamicPressure = q, MachNumber = mach };
        if (parts == null || q <= 0f || speed < MinSpeedMps) return result;

        Vector2 flowDir = airVelBody / speed;       // where the air goes, body frame
        Vector2 velDir = -flowDir;                  // where the vehicle goes, body frame
        float cosA = Mathf.Abs(velDir.Y);           // up = (0, 1) in body frame
        float sinA = Mathf.Abs(velDir.X);
        float machMult = _settings?.SampleMachDrag(mach) ?? 1f;
        float sideCd = _settings?.SideDragCoefficient ?? 1f;
        float stallSin = Mathf.Sin(Mathf.DegToRad(_settings?.FinStallDeg ?? 25f));

        bool onlyExposed = _settings?.FrontalDragOnlyExposed ?? false;
        bool noseFirst = velDir.Y >= 0f;
        float weight = 0f;
        Vector2 weightedPos = Vector2.Zero;
        foreach (RocketPart part in parts.ActiveParts)
        {
            if (part.IsShielded) continue;
            float frontal = !onlyExposed || IsLeadingFaceExposed(part, parts, noseFirst) ? part.FrontalArea * part.DragCoefficient * cosA : 0f;
            Vector2 force = -velDir * (q * (frontal + part.SideArea * sideCd * sinA) * machMult);

            if (part is AeroSurfacePart fin) force += FinForce(fin, flowDir, q, stallSin);
            else if (part is ParachutePart chute) force += ChuteForce(chute, velDir, q, mach);

            Vector2 r = part.GetLocalPosition() - com;
            result.Force += force;
            result.TorqueZ += FlightMath.Cross(r, force);
            partLoads?.Add(new PartLoad { Part = part, Force = force });
            float magnitude = force.Length();
            weight += magnitude;
            weightedPos += part.GetLocalPosition() * magnitude;
        }
        if (weight > 0f) result.CenterOfPressure = weightedPos / weight;
        return result;
    }

    /// <summary>q = 0.5 rho v^2 (Pa).</summary>
    public float CalculateDynamicPressure(float airDensity, float speed) => 0.5f * airDensity * speed * speed;

    /// <summary>True when q is above MaxQLimit.</summary>
    public bool IsOverMaxQ(float dynamicPressure) => dynamicPressure > MaxQLimit;

    /// <summary>True when no other part (decouplers excluded) touches the face of part that meets the flow first.</summary>
    private static bool IsLeadingFaceExposed(RocketPart part, PartTree parts, bool noseFirst)
    {
        if (part is DecouplerPart) return true;
        int left = part.Cell.X;
        int right = part.Cell.X + part.Footprint.X;
        int faceRow = noseFirst ? part.Cell.Y + part.Footprint.Y : part.Cell.Y - 1;
        foreach (RocketPart other in parts.ActiveParts)
        {
            if (other == part || other is DecouplerPart) continue;
            bool rowHit = faceRow >= other.Cell.Y && faceRow < other.Cell.Y + other.Footprint.Y;
            bool columnsOverlap = other.Cell.X < right && other.Cell.X + other.Footprint.X > left;
            if (rowHit && columnsOverlap) return false;
        }
        return true;
    }

    /// <summary>Lift from the flow component across the body plus control deflection (+deflection pushes +X).</summary>
    private static Vector2 FinForce(AeroSurfacePart fin, Vector2 flowDir, float q, float stallSin)
    {
        AeroSurfacePartDefinition def = fin.AeroDefinition;
        if (def == null) return Vector2.Zero;
        float deflection = fin is ControlFinPart control ? control.DeflectionAngleDeg : 0f;
        var perp = new Vector2(flowDir.X, 0f);
        float qcs = q * def.LiftCoefficient * def.PlanformArea;
        Vector2 force = (perp + new Vector2(Mathf.Sin(Mathf.DegToRad(deflection)), 0f)) * qcs;
        float limit = qcs * stallSin;
        float length = force.Length();
        return length > limit && length > 0f ? force * (limit / length) : force;
    }

    /// <summary>Drag of an open chute; a chute opened above its q or Mach limit tears (Broken) on its first open tick.</summary>
    private Vector2 ChuteForce(ParachutePart chute, Vector2 velDir, float q, float mach)
    {
        if (chute.ChuteState != ParachuteState.Deployed || chute.State == ComponentState.Broken) return Vector2.Zero;
        ParachutePartDefinition def = chute.ChuteDefinition;
        if (def == null) return Vector2.Zero;
        if (_checkedChutes.Add(chute) && (q > def.MaxDeployDynamicPressure || mach > def.MaxDeployMach))
        {
            chute.State = ComponentState.Broken;
            GD.PushWarning($"[Physics] Parachute torn: opened at q {q:F0} Pa (max {def.MaxDeployDynamicPressure:F0}), Mach {mach:F2} (max {def.MaxDeployMach:F1})");
            ParachuteTorn?.Invoke(chute, q, mach);
            return Vector2.Zero;
        }
        return -velDir * (q * def.DeployedDragArea);
    }
}

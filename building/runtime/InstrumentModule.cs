namespace RocketGame.Building;

/// <summary>Phase 2: runtime of an InstrumentModuleDefinition (no tick behaviour; events read its stats).</summary>
public class InstrumentModule : PodModule
{
    public InstrumentModuleDefinition InstrumentDefinition => Definition as InstrumentModuleDefinition;
}

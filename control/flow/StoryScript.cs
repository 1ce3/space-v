namespace RocketGame.Control;

/// <summary>
/// All story text in one place (placeholder: edit freely). A card = title + body, shown by CutscenePlayer
/// when the cutscene has no animation scene assigned in GameFlowManager.
/// </summary>
public static class StoryScript
{
    public readonly record struct Card(string Title, string Body, float Seconds = 6f);

    public const string CompanyName = "Space-V";
    public const string TelescopeName = "AURORA telescope";

    public static readonly Card[] SatelliteDeploy =
    {
        new("Stage separation", "The rocket nose opens at orbital altitude..."),
        new(TelescopeName, "The AURORA telescope unfolds its panels and turns to the deep sky. The first mission of " + CompanyName + " is a success!"),
    };

    public static readonly Card[] Discovery =
    {
        new("5 years later", CompanyName + " has grown into a small but ambitious space company.", 5f),
        new("A strange signal", "On the night of March 14, AURORA recorded a streak of light falling onto Mars near Jezero crater. No spacecraft from Earth was there."),
        new("The decision", "Nobody knows what it is. The board decides to build a ship that will carry a robot to Mars to see it up close."),
        new("Your mission", "Research technology, design rockets and land the robot safely on Mars. Every failure is a lesson."),
    };

    public static readonly Card[] MarsEnding =
    {
        new("Touchdown", "The robot opens its camera eyes. Red dust settles around the lander's legs."),
        new("Tracks", "400 m from the landing site there is a long furrow in the ground... and at its end, a smooth metal object unlike anything humans have ever built."),
        new("Discovery", "As the robot comes closer, the object gives off a blue pulse of light, repeating steadily as if waiting for someone to answer..."),
        new("To be continued?", "The data is sent back to Earth. The world will never be the same.", 5f),
    };

    public static readonly string Credits =
        CompanyName + "\n\n\n" +
        "Programming\nDev A · Dev B · Dev C\n\n" +
        "Design & 3D models\nSpace-V team\n\n" +
        "Data\nNASA (atmosphere, radiation, Mars)\n\n" +
        "Tools\nGodot Engine 4.7 (.NET)\n\n\n" +
        "Thanks for playing!";

    // ---- tutorial steps (build screen, then flight) ----
    public static readonly string[] TutorialIntro =
    {
        "Welcome to " + CompanyName + "! First mission: launch the AURORA telescope into orbit.",
    };
}

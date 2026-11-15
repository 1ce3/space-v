namespace RocketGame.Control;

/// <summary>
/// All story text in one place (placeholder: edit freely). A card = title + body, shown by CutscenePlayer
/// when the cutscene has no animation scene assigned in GameFlowManager.
/// </summary>
public static class StoryScript
{
    public readonly record struct Card(string Title, string Body, float Seconds = 6f);

    public const string CompanyName = "Space-V";
    public const string TelescopeName = "Kính viễn vọng AURORA";

    public static readonly Card[] SatelliteDeploy =
    {
        new("Tách tầng", "Phần mũi tên lửa mở ra ở độ cao quỹ đạo..."),
        new(TelescopeName, "Kính viễn vọng AURORA bung tấm pin và hướng về bầu trời sâu. Sứ mệnh đầu tiên của " + CompanyName + " thành công!"),
    };

    public static readonly Card[] Discovery =
    {
        new("5 năm sau", CompanyName + " đã trở thành một công ty vũ trụ nhỏ nhưng đầy tham vọng.", 5f),
        new("Tín hiệu lạ", "Đêm 14/3, AURORA ghi lại một vệt sáng lao xuống bề mặt sao Hỏa, gần miệng núi lửa Jezero. Không có tàu nào của Trái Đất ở đó."),
        new("Quyết định", "Không ai biết nó là gì. Ban lãnh đạo quyết định: chế tạo một con tàu đưa robot lên sao Hỏa để tận mắt kiểm tra."),
        new("Nhiệm vụ của bạn", "Nghiên cứu công nghệ, thiết kế tên lửa và đưa robot hạ cánh an toàn xuống sao Hỏa. Mỗi lần thất bại là một bài học."),
    };

    public static readonly Card[] MarsEnding =
    {
        new("Chạm đất", "Robot mở mắt camera. Bụi đỏ lắng xuống quanh chân tàu."),
        new("Dấu vết", "Cách bãi đáp 400 m là một rãnh cày dài trên mặt đất... và ở cuối rãnh, một vật thể kim loại nhẵn bóng, không giống bất cứ thứ gì con người từng chế tạo."),
        new("Phát hiện", "Khi robot tiến lại gần, vật thể phát ra một nhịp sáng xanh, lặp lại đều đặn như đang chờ ai đó trả lời..."),
        new("Còn tiếp?", "Dữ liệu được gửi về Trái Đất. Thế giới sẽ không bao giờ như cũ.", 5f),
    };

    public static readonly string Credits =
        CompanyName + "\n\n\n" +
        "Lập trình\nDev A · Dev B · Dev C\n\n" +
        "Thiết kế & mô hình 3D\nNhóm Space-V\n\n" +
        "Dữ liệu\nNASA (khí quyển, bức xạ, sao Hỏa)\n\n" +
        "Công cụ\nGodot Engine 4.7 (.NET)\n\n\n" +
        "Cảm ơn bạn đã chơi!";

    // ---- tutorial steps (build screen, then flight) ----
    public static readonly string[] TutorialIntro =
    {
        "Chào mừng tới " + CompanyName + "! Nhiệm vụ đầu tiên: phóng kính viễn vọng AURORA lên quỹ đạo.",
    };
}

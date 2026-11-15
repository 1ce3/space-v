using System;
using System.Collections.Generic;
using Godot;
using RocketGame.Building;
using FlightSceneNode = RocketGame.Control.FlightScene;

namespace RocketGame.Control;

/// <summary>
/// Step-by-step tutorial panel on top of every screen (lives under GameFlowManager). A step with a condition
/// advances by itself when the player has done it; a step without one has a "Tiếp" button.
/// </summary>
public partial class TutorialOverlay : CanvasLayer
{
    [Signal] public delegate void SkipRequestedEventHandler();

    private record Step(string Text, Func<bool> Done);

    private readonly List<Step> _steps = new();
    private int _index;
    private int _launchStep;
    private Label _text;
    private Button _next;
    private PanelContainer _panel;
    private string _prefix = "";

    public float TargetAltitudeM { get; set; } = 40000f;
    /// <summary>True: only the research screen pages; the last page closes the panel (Finished).</summary>
    public bool ResearchOnly { get; set; }
    [Signal] public delegate void FinishedEventHandler();

    public override void _Ready()
    {
        Layer = 50;
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.09f, 0.16f, 0.93f), BorderColor = new Color(0.3f, 0.6f, 1f) };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(14);
        _panel.AddThemeStyleboxOverride("panel", style);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        var head = new Label { Text = "HƯỚNG DẪN", Modulate = new Color(0.5f, 0.75f, 1f) };
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(410, 0) };
        _text.AddThemeFontSizeOverride("font_size", 15);
        var row = new HBoxContainer();
        _next = new Button { Text = "Tiếp  ›", FocusMode = Godot.Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 34) };
        _next.Pressed += () => Advance();
        var skip = new Button { Text = "Bỏ qua hướng dẫn", Flat = true, FocusMode = Godot.Control.FocusModeEnum.None };
        skip.Pressed += () => EmitSignal(SignalName.SkipRequested);
        row.AddChild(skip);
        row.AddChild(new Godot.Control { SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill });
        row.AddChild(_next);
        v.AddChild(head);
        v.AddChild(_text);
        v.AddChild(row);
        _panel.AddChild(v);
        AddChild(_panel);
        PlacePanel();
        GetViewport().SizeChanged += PlacePanel;
        BuildSteps();
        Show(0);
    }

    private static GameFlowManager Flow => GameFlowManager.Instance;
    private static BuildingSystem Building => (Flow?.CurrentScreen as BuildScreenRoot)?.Building;
    private static FlightSceneNode Flight => Flow?.CurrentScreen as FlightSceneNode;

    private static bool Has<T>() where T : RocketPart
    {
        BuildingSystem b = Building;
        return b?.Grid != null && b.Grid.Parts.Exists(p => p is T);
    }

    private int _flightStep;

    /// <summary>Spaceflight-Simulator-like pages: what is on this screen, rocket parts, build rules; then the launch procedure.</summary>
    private void BuildSteps()
    {
        if (ResearchOnly)
        {
            _flightStep = int.MaxValue;
            _steps.Add(new("MÀN NGHIÊN CỨU\nMỗi chuyến bay, kể cả thất bại, cho Research Points (RP) theo độ cao, tốc độ và kỷ lục mới. Số RP hiện ở góc trên phải.", null));
            _steps.Add(new("CÁC TAB\nĐộng cơ (chia tầng thấp / giữa / trên), Nhiên liệu, Vật liệu, Khung & khoang, Liên lạc, các loại Cảm biến...\nMỗi hàng là một TIER: tier cao mạnh hơn nhưng đắt hơn.", null));
            _steps.Add(new("THẺ NGHIÊN CỨU\n• Giá RP và các part / thiết bị được mở khoá.\n• \"Cần:\" là node phải mua trước (có thể ở tab khác).\n• Viền xanh dương: mua được · xanh lá: đã có · xám: chưa đủ điều kiện.", null));
            _steps.Add(new("MAX BUILD WEIGHT\nNgân sách lắp ráp. Các node Khung (Frame) tăng ngân sách để chế tạo tên lửa lớn hơn.", null));
            _steps.Add(new("ĐƯỜNG TỚI SAO HỎA\nCần: động cơ tầng trên + bình Hydrolox (Upper Stage), khiên nhiệt và dù (Entry, Descent and Landing), điện cho 210 ngày bay (pin mặt trời). Thiết bị liên lạc / cảm biến giúp xử lý sự cố.\n\nBấm \"Go to Workshop\" để lắp ráp.", null));
            return;
        }
        // ---- workshop ----
        _steps.Add(new(StoryScript.TutorialIntro[0] + "\n\nĐây là XƯỞNG LẮP RÁP. Bên trái là danh sách part, ở giữa là lưới lắp ráp, bên phải là thông số tên lửa.", null));
        _steps.Add(new("CÁC LOẠI PART\n• Khoang điều khiển: đầu tên lửa, chứa thiết bị và vệ tinh.\n• Bình nhiên liệu: Kerolox, Hydrolox, Methalox...\n• Động cơ lỏng / tên lửa đẩy rắn (gắn hai bên).\n• Bộ tách tầng (decoupler), cánh lái, chóp mũi (fairing), dù, khiên nhiệt.", null));
        _steps.Add(new("MỘT TÊN LỬA CƠ BẢN (từ trên xuống)\nChóp mũi → Khoang điều khiển → Bình nhiên liệu → Động cơ.\nTên lửa nhiều tầng: chèn bộ tách tầng (phím Tab) giữa các tầng để bỏ tầng đã hết nhiên liệu.", null));
        _steps.Add(new("NGUYÊN TẮC LẮP RÁP\n• Cần đúng 1 khoang điều khiển, mọi part phải nối liền với nó.\n• Động cơ phải chạm trực tiếp bình CÙNG loại nhiên liệu.\n• Tổng weight không vượt ngân sách (Weight ở bảng phải).\n• Trọng tâm cao, cánh lái ở dưới giúp tên lửa ổn định (thử Wind tunnel).", null));
        _steps.Add(new("TÚI ĐỒ KHOANG LÁI (bảng phải)\nKéo thiết bị vào lưới: IMU để bật SAS giữ thăng bằng, pin để có điện, cảm biến và ăng-ten để xử lý sự cố. Vật liệu vỏ đổi độ bền / khối lượng.", null));
        _steps.Add(new("Thử lắp: Khoang điều khiển Mk1 → Bình Kerolox S1 → Động cơ LV1, kéo IMU G1 vào túi đồ.\nKhi \"Kiểm tra thiết kế\" báo hợp lệ, bấm \"Phóng\".", () => Flow?.State == Core.GameState.Flight));
        _launchStep = _steps.Count - 1;
        // ---- launch pad ----
        _flightStep = _steps.Count;
        _steps.Add(new("BỆ PHÓNG\nThông số bên trái màn hình:\n• Độ cao / Tốc độ (đứng, ngang)\n• Góc: hướng mũi tên lửa (90° = thẳng đứng)\n• Ga và nhiên liệu còn lại\n• q: áp suất động, Mach, G: gia tốc\n• Nhiệt / tải: quá 100% là part hỏng.", null));
        _steps.Add(new("QUY TRÌNH PHÓNG\n1. T: bật SAS (giữ hướng).\n2. Z: ga tối đa (Shift/Ctrl tăng/giảm, X tắt máy).\n3. Space: kích hoạt tầng đầu, phóng!", () => Flight?.Director != null && Flight.Director.Phase != Core.FlightPhase.PreLaunch));
        _steps.Add(new("KHI BAY\n• Qua ~3 km: nghiêng nhẹ bằng A/D để bay xiên (gravity turn).\n• Chú ý q lớn nhất (Max-Q) và nhiệt: giảm ga nếu quá cao.\n• Hết nhiên liệu: Space để tách tầng.", () => (Flight?.Director?.LastFrame.AltitudeM ?? 0f) > 3000f));
        _steps.Add(new($"Bay lên độ cao {TargetAltitudeM / 1000f:0} km để đưa kính viễn vọng AURORA vào quỹ đạo!", null));
    }

    /// <summary>Bottom-left corner, right of the build palette (250 px). Re-run when the text changes size.</summary>
    private void PlacePanel()
    {
        if (_panel == null) return;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        _panel.Size = _panel.GetCombinedMinimumSize();
        _panel.Position = new Vector2(262f, view.Y - _panel.Size.Y - 12f);
    }

    private void Show(int i)
    {
        _index = Mathf.Clamp(i, 0, _steps.Count - 1);
        _text.Text = _prefix + _steps[_index].Text;
        bool last = _index == _steps.Count - 1;
        _next.Visible = _steps[_index].Done == null && (!last || ResearchOnly);
        _next.Text = last && ResearchOnly ? "Xong" : "Tiếp  ›";
        Callable.From(PlacePanel).CallDeferred();
    }

    private void Advance()
    {
        _prefix = "";
        if (_index < _steps.Count - 1) Show(_index + 1);
        else if (ResearchOnly) EmitSignal(SignalName.Finished);
    }

    public override void _Process(double delta)
    {
        if (_index < _flightStep && Flow?.State == Core.GameState.Flight) { _prefix = ""; Show(_flightStep); return; }
        Func<bool> done = _steps.Count > 0 ? _steps[_index].Done : null;
        if (done != null && done()) Advance();
    }

    /// <summary>The tutorial flight failed: back to the launch step with a hint.</summary>
    public void OnRetry(string why)
    {
        _prefix = $"Chưa thành công ({why}). Chỉnh lại tên lửa rồi thử lại!\n\n";
        Show(_launchStep);
    }
}

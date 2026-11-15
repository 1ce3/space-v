using Godot;

public partial class Explosion : Node3D
{
	public override async void _Ready()
	{
		var fireBlast = GetNode<GpuParticles3D>("FireBlast");
		var debrisSparks = GetNode<GpuParticles3D>("DebrisSparks");
		var shockwave = GetNodeOrNull<MeshInstance3D>("Shockwave");
		var flashLight = GetNodeOrNull<OmniLight3D>("FlashLight");

		// 1. Kích nổ đồng thời toàn bộ hạt
		fireBlast.Restart();
		debrisSparks.Restart();
		fireBlast.Emitting = true;
		debrisSparks.Emitting = true;

		// 2. Tạo Tween cho sóng xung kích và đèn chớp
		Tween tween = CreateTween().SetParallel(true);

		if (shockwave != null)
		{
			shockwave.Scale = Vector3.One * 0.1f;
			tween.TweenProperty(shockwave, "scale", Vector3.One * 8.0f, 0.25f)
				 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			
			// Làm mờ dần vật liệu của Shockwave
			if (shockwave.GetActiveMaterial(0) is StandardMaterial3D mat)
			{
				Color startColor = mat.AlbedoColor;
				Color endColor = new Color(startColor.R, startColor.G, startColor.B, 0.0f);
				tween.TweenProperty(mat, "albedo_color", endColor, 0.25f);
			}
		}

		if (flashLight != null)
		{
			flashLight.LightEnergy = 15.0f;
			tween.TweenProperty(flashLight, "light_energy", 0.0f, 0.2f);
		}

		// 3. Chờ 1.5s cho khói tan rồi tự hủy
		await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		QueueFree();
	}
}

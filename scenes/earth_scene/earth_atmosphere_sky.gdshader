shader_type sky;

uniform float rocket_altitude = 0.0;
uniform float visual_planet_radius = 6000000.0;
uniform float space_altitude = 50000.0;

uniform vec3 day_zenith_color : source_color = vec3(0.12, 0.34, 0.76);
uniform vec3 high_alt_zenith_color : source_color = vec3(0.015, 0.04, 0.14);
uniform vec3 space_color : source_color = vec3(0.002, 0.003, 0.008);

uniform vec3 low_horizon_color : source_color = vec3(0.76, 0.88, 0.98);
uniform vec3 limb_glow_color : source_color = vec3(0.32, 0.68, 1.0);
uniform vec3 ground_nadir_color : source_color = vec3(0.14, 0.28, 0.52);

void sky() {
	float h = max(rocket_altitude, 0.0);
	// Normalized altitude progression: 0 at sea level, 1 in space
	float alt_t = clamp(h / max(space_altitude, 1.0), 0.0, 1.0);
	// Faster curve for zenith darkening (noticeably dark navy by 8-15 km)
	float zenith_t = clamp(pow(h / 22000.0, 0.75), 0.0, 1.0);
	float space_t = smoothstep(18000.0, space_altitude, h);

	// Geometric depression of the curved planet horizon: sin(theta_h) = -sqrt(2Rh + h^2) / (R + h)
	float r = max(visual_planet_radius, 100000.0);
	float sin_h = -sqrt(max(0.0, 2.0 * r * h + h * h)) / (r + h);
	// Relative elevation above the apparent planet horizon
	float elev = EYEDIR.y - sin_h;

	// 1. Zenith & Upper Sky (Rayleigh gradient)
	vec3 zenith = mix(day_zenith_color, high_alt_zenith_color, zenith_t);
	zenith = mix(zenith, space_color, space_t);

	// 2. Horizon Scattering Band (Cyan-white Mie haze at low alt -> bright cobalt-cyan limb at high alt -> fades in deep space)
	float limb_strength = 1.0 - smoothstep(32000.0, space_altitude * 1.3, h);
	vec3 horizon_band = mix(low_horizon_color, limb_glow_color, smoothstep(3000.0, 25000.0, h));
	vec3 effective_horizon = mix(space_color, horizon_band, limb_strength);

	// 3. Lower hemisphere (fills behind/below the planet mesh with matching atmospheric haze, never dark green!)
	vec3 nadir = mix(ground_nadir_color, space_color, clamp(alt_t * 1.2, 0.0, 1.0));

	// Vertical sky blend above the horizon
	// Band tightens with altitude so from high up it forms a crisp glowing atmospheric limb
	float sky_sharpness = mix(3.2, 16.0, clamp(h / 28000.0, 0.0, 1.0));
	float above_t = clamp(elev / max(0.001, 1.0 - sin_h), 0.0, 1.0);
	float sky_curve = 1.0 - exp(-above_t * sky_sharpness);
	vec3 upper_col = mix(effective_horizon, zenith, sky_curve);

	// Below the horizon line: stay bright hazy blue near the limb so any planet mesh edge is invisible
	float below_t = clamp(-elev / max(0.001, 1.0 + sin_h), 0.0, 1.0);
	float ground_curve = smoothstep(0.0, 0.45, below_t);
	vec3 lower_col = mix(effective_horizon, nadir, ground_curve);

	// Smoothly bridge across the horizon line (elev == 0)
	float bridge = smoothstep(-0.015, 0.015, elev);
	vec3 col = mix(lower_col, upper_col, bridge);

	// Intense narrow forward-scattering glow right along the horizon line
	float band_width = mix(0.09, 0.022, clamp(h / 30000.0, 0.0, 1.0));
	float glow = exp(-pow(elev / band_width, 2.0)) * limb_strength;
	col = mix(col, low_horizon_color, glow * 0.55);

	// Subtle solar disc & Mie halo if DirectionalLight0 is active
	if (LIGHT0_ENABLED) {
		float sun_cos = clamp(dot(EYEDIR, LIGHT0_DIRECTION), 0.0, 1.0);
		float mie = pow(sun_cos, 12.0) * 0.25 * (1.0 - space_t);
		float disc = smoothstep(0.9992, 0.9998, sun_cos);
		col += LIGHT0_COLOR * (mie + disc * 1.5);
	}

	COLOR = col;
}

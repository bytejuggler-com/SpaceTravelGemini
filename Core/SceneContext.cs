using System.Numerics;

namespace SpaceTravel.Core;

public sealed class SceneContext
{
    public int ScreenWidth { get; set; } = 1280;
    public int ScreenHeight { get; set; } = 720;
    public Vector2 Resolution => new(ScreenWidth, ScreenHeight);

    public float TotalTime { get; set; }
    public float SceneTime { get; set; }
    public float DeltaTime { get; set; }

    // Audio-reactive parameters (0.0 .. 1.0)
    public float BassEnergy { get; set; }
    public float MidEnergy { get; set; }
    public float HighEnergy { get; set; }
    public float BeatPulse { get; set; }
    public float[] Spectrum { get; } = new float[32];

    // Visual toggles
    public bool CrtEnabled { get; set; } = false;
    public bool BloomEnabled { get; set; } = true;
    public bool HudEnabled { get; set; } = true;
    public bool AudioMuted { get; set; } = false;
}

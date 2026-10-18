using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Scenes;

/// <summary>
/// Scene 2: Horizontal Parallax Starfield & Nebula Sector Cruise
/// Multi-layered horizontal starfield with procedural emission nebula backdrop,
/// Amiga-style raster horizon glow, foreground cosmic dust streaks, and a patrolling starship squadron.
/// </summary>
public sealed class HorizontalParallaxScene : ISpaceScene
{
    public string Name => "Horizontal Sector Cruise";
    public string Subtitle => "6-Layer Parallax Starfield + Procedural Emission Nebula + Patrol Squadron";
    public float DefaultDurationSeconds => 13.0f;

    private struct ParallaxStar
    {
        public Vector2 Pos;
        public int Layer; // 0 = deepest, 5 = closest foreground
        public float Speed;
        public float Size;
        public Color Color;
        public float TwinklePhase;
    }

    private const int StarCount = 1600;
    private readonly ParallaxStar[] _stars = new ParallaxStar[StarCount];
    private readonly List<Vector2> _shipTrail = new(64);

    private Shader _nebulaShader;
    private Texture2D _dummyTex;
    private int _locRes, _locTime, _locBass, _locBeat;

    private const string NebulaFragmentShader = @"#version 330
in vec2 fragTexCoord;
out vec4 finalColor;

uniform vec2 uResolution;
uniform float uTime;
uniform float uBass;
uniform float uBeat;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x),
        f.y
    );
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    mat2 rot = mat2(0.8, -0.6, 0.6, 0.8);
    for (int i = 0; i < 5; i++) {
        v += a * noise(p);
        p = rot * p * 2.02;
        a *= 0.5;
    }
    return v;
}

void main()
{
    vec2 uv = fragTexCoord;
    vec2 aspectUv = vec2(uv.x * (uResolution.x / max(1.0, uResolution.y)), uv.y);

    // Scrolling multi-layer emission nebula
    vec2 p1 = aspectUv * 2.2 + vec2(uTime * 0.14, 0.0);
    vec2 p2 = aspectUv * 3.8 + vec2(uTime * 0.28, -uTime * 0.03);

    float q = fbm(p1);
    float r = fbm(p1 + vec2(q * 1.6 + uTime * 0.08, q * 1.2));
    float dust = fbm(p2);

    vec3 bg = vec3(0.012, 0.016, 0.042);

    // Emission colors: Orion magenta/crimson, cyan ionized oxygen, deep indigo
    vec3 col1 = vec3(0.04, 0.48, 0.78) * smoothstep(0.25, 0.85, r);
    vec3 col2 = vec3(0.82, 0.14, 0.52) * smoothstep(0.38, 0.92, q * r * 1.6);
    vec3 col3 = vec3(0.95, 0.55, 0.18) * pow(max(0.0, r - 0.55), 2.2) * (1.2 + uBass);

    vec3 neb = (col1 + col2 + col3) * (0.65 + 0.35 * sin(uv.y * 3.14159));

    // Dark cosmic dust lanes carving through the nebula
    float dustMask = smoothstep(0.42, 0.75, dust);
    neb *= (1.0 - dustMask * 0.62);

    // Classic Amiga-inspired atmospheric copper-bar horizon glow along lower & upper bands
    float horizonGlow = exp(-abs(uv.y - 0.78) * 9.0) * (0.32 + uBeat * 0.22);
    vec3 copper = vec3(0.15, 0.75, 1.0) * horizonGlow;

    finalColor = vec4(bg + neb + copper, 1.0);
}
";

    public void Load(SceneContext ctx)
    {
        Image img = Raylib.GenImageColor(2, 2, Color.White);
        _dummyTex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        _nebulaShader = ShaderHelper.LoadFragmentShader(NebulaFragmentShader);
        _locRes = Raylib.GetShaderLocation(_nebulaShader, "uResolution");
        _locTime = Raylib.GetShaderLocation(_nebulaShader, "uTime");
        _locBass = Raylib.GetShaderLocation(_nebulaShader, "uBass");
        _locBeat = Raylib.GetShaderLocation(_nebulaShader, "uBeat");

        InitStars(ctx.ScreenWidth, ctx.ScreenHeight);
    }

    public void OnEnter(SceneContext ctx)
    {
        _shipTrail.Clear();
    }

    private void InitStars(int w, int h)
    {
        var rng = new Random(4242);
        // Layer speeds: exponential parallax from distant background (layer 0) to hyper-fast foreground (layer 5)
        float[] layerSpeeds = { 28f, 65f, 135f, 260f, 480f, 860f };
        float[] layerSizes = { 1.0f, 1.3f, 1.7f, 2.2f, 2.8f, 3.6f };

        for (int i = 0; i < StarCount; i++)
        {
            // Weight more stars in deeper layers, fewer in close foreground
            double roll = rng.NextDouble();
            int layer = roll switch
            {
                < 0.36 => 0,
                < 0.62 => 1,
                < 0.80 => 2,
                < 0.91 => 3,
                < 0.97 => 4,
                _      => 5
            };

            Color col = layer switch
            {
                0 => new Color((byte)130, (byte)155, (byte)210, (byte)160),
                1 => new Color((byte)165, (byte)200, (byte)245, (byte)195),
                2 => new Color((byte)210, (byte)235, (byte)255, (byte)225),
                3 => new Color((byte)130, (byte)230, (byte)255, (byte)245),
                4 => new Color((byte)255, (byte)215, (byte)155, (byte)255),
                _ => new Color((byte)255, (byte)140, (byte)235, (byte)255)
            };

            _stars[i] = new ParallaxStar
            {
                Pos = new Vector2((float)rng.NextDouble() * w, (float)rng.NextDouble() * h),
                Layer = layer,
                Speed = layerSpeeds[layer] * (0.88f + 0.24f * (float)rng.NextDouble()),
                Size = layerSizes[layer],
                Color = col,
                TwinklePhase = (float)rng.NextDouble() * MathF.Tau
            };
        }
    }

    public void Update(SceneContext ctx)
    {
        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;
        float speedBoost = 1.0f + ctx.BassEnergy * 0.45f;

        for (int i = 0; i < StarCount; i++)
        {
            ref ParallaxStar s = ref _stars[i];
            s.Pos.X -= s.Speed * speedBoost * ctx.DeltaTime;

            if (s.Pos.X < -60f)
            {
                s.Pos.X = w + 40f + (float)Random.Shared.NextDouble() * 40f;
                s.Pos.Y = (float)Random.Shared.NextDouble() * h;
            }
        }

        // Update patrolling starship position & ribbon trail
        Vector2 shipPos = GetLeadShipPosition(ctx);
        _shipTrail.Insert(0, shipPos);
        if (_shipTrail.Count > 45)
        {
            _shipTrail.RemoveAt(_shipTrail.Count - 1);
        }
        for (int i = 1; i < _shipTrail.Count; i++)
        {
            _shipTrail[i] = new Vector2(_shipTrail[i].X - 420f * ctx.DeltaTime, _shipTrail[i].Y);
        }
    }

    private static Vector2 GetLeadShipPosition(SceneContext ctx)
    {
        float x = ctx.ScreenWidth * (0.34f + 0.08f * MathF.Sin(ctx.TotalTime * 0.9f));
        float y = ctx.ScreenHeight * (0.50f + 0.16f * MathF.Sin(ctx.TotalTime * 1.35f) + 0.05f * MathF.Cos(ctx.TotalTime * 2.7f));
        return new Vector2(x, y);
    }

    public void Render(SceneContext ctx)
    {
        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;

        // 1. Procedural Scrolling Emission Nebula Backdrop
        ShaderHelper.SetVec2(_nebulaShader, _locRes, ctx.Resolution);
        ShaderHelper.SetFloat(_nebulaShader, _locTime, ctx.TotalTime);
        ShaderHelper.SetFloat(_nebulaShader, _locBass, ctx.BassEnergy);
        ShaderHelper.SetFloat(_nebulaShader, _locBeat, ctx.BeatPulse);
        ShaderHelper.DrawFullscreenShaderQuad(_nebulaShader, _dummyTex, w, h);

        Raylib.BeginBlendMode(BlendMode.Additive);

        // 2. Render 6 Horizontal Parallax Star Layers
        for (int i = 0; i < StarCount; i++)
        {
            ref readonly ParallaxStar s = ref _stars[i];
            float twinkle = 0.72f + 0.28f * MathF.Sin(ctx.TotalTime * 4.5f + s.TwinklePhase);
            Color c = new(s.Color.R, s.Color.G, s.Color.B, (byte)(s.Color.A * twinkle));

            if (s.Layer <= 1)
            {
                Raylib.DrawCircleV(s.Pos, s.Size, c);
            }
            else
            {
                // Horizontal motion streak proportional to layer speed
                float streakLen = (s.Layer - 1) * (8f + ctx.BeatPulse * 10f);
                Vector2 tail = new(s.Pos.X + streakLen, s.Pos.Y);
                Raylib.DrawLineEx(s.Pos, tail, s.Size, c);
                Raylib.DrawCircleV(s.Pos, s.Size * 0.85f, new Color((byte)255, (byte)255, (byte)255, c.A));
            }
        }

        // 3. Render Patrolling Starship & Wingmen with Plasma Thruster Trails
        DrawPatrolShip(ctx);

        Raylib.EndBlendMode();
    }

    private void DrawPatrolShip(SceneContext ctx)
    {
        Vector2 shipPos = GetLeadShipPosition(ctx);

        // Glowing ion ribbon trail
        for (int i = 0; i < _shipTrail.Count - 1; i++)
        {
            float t = 1.0f - (i / (float)_shipTrail.Count);
            Vector2 p0 = _shipTrail[i] + new Vector2(-26f, 0f);
            Vector2 p1 = _shipTrail[i + 1] + new Vector2(-26f, 0f);
            Raylib.DrawLineEx(p0, p1, 7.0f * t, new Color((byte)40, (byte)210, (byte)255, (byte)(190 * t)));
            Raylib.DrawLineEx(p0, p1, 3.0f * t, new Color((byte)255, (byte)255, (byte)255, (byte)(220 * t)));
        }

        // Thruster afterburner flare (pulsing on beat)
        float flareRadius = 14f + ctx.BeatPulse * 12f + MathF.Sin(ctx.TotalTime * 40f) * 3f;
        Vector2 thrusterPos = shipPos + new Vector2(-26f, 0f);
        Raylib.DrawCircleGradient((int)thrusterPos.X, (int)thrusterPos.Y, flareRadius * 2.2f,
            new Color((byte)80, (byte)220, (byte)255, (byte)210), new Color((byte)0, (byte)80, (byte)255, (byte)0));
        Raylib.DrawCircleV(thrusterPos, flareRadius * 0.45f, Color.White);

        // Sleek vector-style interceptor hull
        Vector2 nose = shipPos + new Vector2(34f, 0f);
        Vector2 topWing = shipPos + new Vector2(-22f, -15f);
        Vector2 bottomWing = shipPos + new Vector2(-22f, 15f);
        Vector2 rearCenter = shipPos + new Vector2(-14f, 0f);

        Raylib.DrawTriangle(nose, topWing, rearCenter, new Color((byte)140, (byte)220, (byte)255, (byte)230));
        Raylib.DrawTriangle(nose, rearCenter, bottomWing, new Color((byte)70, (byte)155, (byte)235, (byte)230));
        Raylib.DrawLineEx(nose, topWing, 2.0f, Color.White);
        Raylib.DrawLineEx(nose, bottomWing, 2.0f, Color.White);

        // Forward sensor sweep cone
        float sweepAngle = MathF.Sin(ctx.TotalTime * 2.5f) * 0.15f;
        Vector2 beamTop = nose + new Vector2(260f, -55f + sweepAngle * 140f);
        Vector2 beamBot = nose + new Vector2(260f, 55f + sweepAngle * 140f);
        Raylib.DrawTriangle(nose, beamBot, beamTop, new Color((byte)0, (byte)245, (byte)255, (byte)22));
    }

    public void Dispose()
    {
        Raylib.UnloadShader(_nebulaShader);
        Raylib.UnloadTexture(_dummyTex);
    }
}

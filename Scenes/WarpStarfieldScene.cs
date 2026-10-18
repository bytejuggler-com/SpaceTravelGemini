using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;
using SpaceTravel.Rendering;

namespace SpaceTravel.Scenes;

/// <summary>
/// Scene 1: 3D Warp Starfield
/// Volumetric 3D starfield with camera banking/roll, velocity-stretched warp streaks,
/// beat-reactive hyperspace surges, and a procedural GLSL warp-nebula tunnel backdrop.
/// </summary>
public sealed class WarpStarfieldScene : ISpaceScene
{
    public string Name => "3D Hyperspace Warp Field";
    public string Subtitle => "6,000 Volumetric 3D Stars + Relativistic Streak Projection + GLSL Nebula Tunnel";
    public float DefaultDurationSeconds => 13.0f;

    private struct Star3D
    {
        public Vector3 Position;
        public float BaseSpeed;
        public Color Color;
        public float Size;
    }

    private const int StarCount = 4500;
    private const float FieldRadius = 950f;
    private const float FarZ = 1600f;
    private const float NearZ = 8f;

    private readonly Star3D[] _stars = new Star3D[StarCount];
    private Shader _bgShader;
    private Texture2D _dummyTex;
    private int _locRes, _locTime, _locWarp, _locBass;
    private float _currentWarpMultiplier = 1.0f;

    private const string TunnelFragmentShader = @"#version 330
in vec2 fragTexCoord;
out vec4 finalColor;

uniform vec2 uResolution;
uniform float uTime;
uniform float uWarp;
uniform float uBass;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * noise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void main()
{
    vec2 uv = (fragTexCoord - 0.5) * 2.0;
    uv.x *= uResolution.x / max(1.0, uResolution.y);

    // Camera drift
    vec2 center = vec2(sin(uTime * 0.45) * 0.18, cos(uTime * 0.35) * 0.12);
    vec2 p = uv - center;

    float r = length(p) + 0.001;
    float angle = atan(p.y, p.x);

    // Swirling hyperspace nebula coordinates
    float tunnelZ = 1.2 / (r + 0.15) - uTime * (0.9 + uWarp * 0.4);
    vec2 nebCoord = vec2(angle * 2.2 / 3.14159 + sin(tunnelZ * 0.3), tunnelZ * 0.65);

    float n1 = fbm(nebCoord);
    float n2 = fbm(nebCoord * 1.8 - vec2(uTime * 0.4, uTime * 0.7));

    vec3 deepSpace = vec3(0.01, 0.015, 0.045);
    vec3 cyanGlow = vec3(0.05, 0.55, 0.95) * n1;
    vec3 violetGlow = vec3(0.55, 0.12, 0.85) * n2;

    float radialMask = smoothstep(0.02, 1.15, r) * exp(-r * 0.75);
    vec3 col = deepSpace + (cyanGlow + violetGlow) * radialMask * (0.55 + uBass * 0.45 + uWarp * 0.2);

    // Distant core glow at vanishing point
    float core = exp(-r * (4.2 - uWarp * 0.8)) * (0.35 + uBass * 0.35);
    col += vec3(0.45, 0.80, 1.0) * core;

    finalColor = vec4(col, 1.0);
}
";

    public void Load(SceneContext ctx)
    {
        Image img = Raylib.GenImageColor(2, 2, Color.White);
        _dummyTex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        _bgShader = ShaderHelper.LoadFragmentShader(TunnelFragmentShader);
        _locRes = Raylib.GetShaderLocation(_bgShader, "uResolution");
        _locTime = Raylib.GetShaderLocation(_bgShader, "uTime");
        _locWarp = Raylib.GetShaderLocation(_bgShader, "uWarp");
        _locBass = Raylib.GetShaderLocation(_bgShader, "uBass");

        var rng = new Random(1337);
        for (int i = 0; i < StarCount; i++)
        {
            _stars[i] = CreateStar(rng, randomZ: true);
        }
    }

    public void OnEnter(SceneContext ctx)
    {
        _currentWarpMultiplier = 1.0f;
    }

    private static Star3D CreateStar(Random rng, bool randomZ)
    {
        // Polar distribution with higher density around the flight corridor
        float angle = (float)(rng.NextDouble() * MathF.Tau);
        float radius = 22f + MathF.Pow((float)rng.NextDouble(), 1.35f) * FieldRadius;
        float z = randomZ ? NearZ + (float)rng.NextDouble() * (FarZ - NearZ) : FarZ * (0.88f + 0.12f * (float)rng.NextDouble());

        // Color palette: icy blue, cyan, pure white, warm gold, relativistic violet
        float huePick = (float)rng.NextDouble();
        Color col = huePick switch
        {
            < 0.45f => new Color((byte)200, (byte)235, (byte)255, (byte)255),
            < 0.72f => new Color((byte)100, (byte)210, (byte)255, (byte)255),
            < 0.88f => new Color((byte)255, (byte)220, (byte)165, (byte)255),
            _       => new Color((byte)215, (byte)145, (byte)255, (byte)255),
        };

        return new Star3D
        {
            Position = new Vector3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, z),
            BaseSpeed = 240f + (float)rng.NextDouble() * 220f,
            Color = col,
            Size = 1.0f + (float)rng.NextDouble() * 1.9f
        };
    }

    public void Update(SceneContext ctx)
    {
        // Modulate warp speed with a dramatic surge curve + audio beat boost
        float surgeWave = 0.5f + 0.5f * MathF.Sin(ctx.SceneTime * 0.85f - 0.8f);
        float targetWarp = 0.75f + MathF.Pow(surgeWave, 2.2f) * 2.6f + ctx.BeatPulse * 0.85f;
        _currentWarpMultiplier = _currentWarpMultiplier * 0.90f + targetWarp * 0.10f;

        var rng = Random.Shared;
        for (int i = 0; i < StarCount; i++)
        {
            ref Star3D s = ref _stars[i];
            s.Position.Z -= s.BaseSpeed * _currentWarpMultiplier * ctx.DeltaTime;

            if (s.Position.Z <= NearZ)
            {
                s = CreateStar(rng, randomZ: false);
            }
        }
    }

    public void Render(SceneContext ctx)
    {
        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;

        // 1. Render procedural GLSL nebula warp tunnel backdrop
        ShaderHelper.SetVec2(_bgShader, _locRes, ctx.Resolution);
        ShaderHelper.SetFloat(_bgShader, _locTime, ctx.TotalTime);
        ShaderHelper.SetFloat(_bgShader, _locWarp, _currentWarpMultiplier);
        ShaderHelper.SetFloat(_bgShader, _locBass, ctx.BassEnergy);
        ShaderHelper.DrawFullscreenShaderQuad(_bgShader, _dummyTex, w, h);

        // 2. Project 3D stars with camera banking, pitch/yaw drift, and velocity streaks
        float roll = MathF.Sin(ctx.TotalTime * 0.52f) * 0.48f + MathF.Cos(ctx.TotalTime * 0.23f) * 0.25f;
        float cosR = MathF.Cos(roll);
        float sinR = MathF.Sin(roll);

        float centerX = w * 0.5f + MathF.Sin(ctx.TotalTime * 0.45f) * (w * 0.08f);
        float centerY = h * 0.5f + MathF.Cos(ctx.TotalTime * 0.35f) * (h * 0.06f);
        float fovScale = h * 0.68f;
        float streakLengthZ = 14f + _currentWarpMultiplier * 44f;

        Raylib.BeginBlendMode(BlendMode.Additive);

        for (int i = 0; i < StarCount; i++)
        {
            ref readonly Star3D s = ref _stars[i];
            float zHead = s.Position.Z;
            float zTail = zHead + streakLengthZ * (s.BaseSpeed / 320f);

            // Apply 2D camera roll around Z axis
            float rx = s.Position.X * cosR - s.Position.Y * sinR;
            float ry = s.Position.X * sinR + s.Position.Y * cosR;

            float invZHead = fovScale / zHead;
            float invZTail = fovScale / zTail;

            Vector2 head = new(centerX + rx * invZHead, centerY + ry * invZHead);
            Vector2 tail = new(centerX + rx * invZTail, centerY + ry * invZTail);

            if (head.X < -80 || head.X > w + 80 || head.Y < -80 || head.Y > h + 80)
                continue;

            float depthFade = Math.Clamp(1.0f - (zHead / FarZ), 0.05f, 1.0f);
            float alphaVal = MathF.Pow(depthFade, 1.25f);
            byte alpha = (byte)(alphaVal * 255);

            Color streakColor = new(s.Color.R, s.Color.G, s.Color.B, (byte)(alpha * 0.78f));
            float thickness = Math.Clamp(s.Size * depthFade * 1.7f, 1.0f, 4.2f);

            Raylib.DrawLineEx(tail, head, thickness, streakColor);

            // Bright star head core for nearby stars
            if (zHead < FarZ * 0.55f)
            {
                Color headCol = new((byte)255, (byte)255, (byte)255, alpha);
                Raylib.DrawCircleV(head, thickness * 0.72f, headCol);
            }
        }

        Raylib.EndBlendMode();
    }

    public void Dispose()
    {
        Raylib.UnloadShader(_bgShader);
        Raylib.UnloadTexture(_dummyTex);
    }
}

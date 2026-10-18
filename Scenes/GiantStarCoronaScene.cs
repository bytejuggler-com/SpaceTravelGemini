using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Scenes;

/// <summary>
/// Scene 3: Patrolling a Giant Star with Corona Flames
/// Fullscreen GPU Raymarching / 3D FBM Plasma shader rendering a boiling hypergiant star,
/// convective surface granulation, erupting magnetic corona flames, and foreground solar ejecta sparks.
/// </summary>
public sealed class GiantStarCoronaScene : ISpaceScene
{
    public string Name => "Hypergiant Solar Corona Patrol";
    public string Subtitle => "GPU Raymarched 3D FBM Plasma Surface + Erupting Corona Prominences + Solar Wind";
    public float DefaultDurationSeconds => 14.0f;

    private struct SolarSpark
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Size;
    }

    private const int SparkCount = 220;
    private readonly SolarSpark[] _sparks = new SolarSpark[SparkCount];

    private Shader _coronaShader;
    private Texture2D _dummyTex;
    private int _locRes, _locTime, _locBass, _locBeat;

    private const string CoronaFragmentShader = @"#version 330
in vec2 fragTexCoord;
out vec4 finalColor;

uniform vec2 uResolution;
uniform float uTime;
uniform float uBass;
uniform float uBeat;

float hash(vec3 p) {
    p = fract(p * vec3(443.897, 441.423, 437.195));
    p += dot(p, p.yzx + 19.19);
    return fract((p.x + p.y) * p.z);
}

float noise(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    return mix(
        mix(mix(hash(i + vec3(0,0,0)), hash(i + vec3(1,0,0)), f.x),
            mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
        mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x),
            mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y),
        f.z
    );
}

float fbm(vec3 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++) {
        v += a * noise(p);
        p = p * 2.03 + vec3(0.13, -0.17, 0.21);
        a *= 0.5;
    }
    return v;
}

// Fire color ramp: deep crimson -> blazing orange -> gold -> white-hot plasma
vec3 firePalette(float x) {
    x = clamp(x, 0.0, 1.5);
    return vec3(
        smoothstep(0.0, 0.45, x) * 1.15,
        smoothstep(0.22, 0.85, x) * 1.05,
        pow(max(0.0, x - 0.55), 1.8) * 1.8
    );
}

void main()
{
    vec2 uv = (fragTexCoord - 0.5) * 2.0;
    uv.x *= uResolution.x / max(1.0, uResolution.y);

    // Camera skimming and banking along the giant star's horizon
    float camPanX = sin(uTime * 0.35) * 0.28;
    float camBobY = -0.68 + cos(uTime * 0.45) * 0.12;
    vec2 starCenter = vec2(camPanX, camBobY);

    vec2 p = uv - starCenter;
    float r = length(p);
    float starRadius = 0.92 + uBass * 0.025;
    float angle = atan(p.y, p.x);

    vec3 col = vec3(0.015, 0.008, 0.025);

    // Background stars far behind the solar corona
    float bgStar = pow(hash(vec3(floor((uv + vec2(uTime * 0.02, 0.0)) * 180.0), 1.0)), 26.0);
    col += vec3(0.7, 0.85, 1.0) * bgStar * smoothstep(starRadius, starRadius + 0.4, r);

    if (r < starRadius)
    {
        // 3D sphere normal and rotating spherical coordinates for boiling photosphere
        float z = sqrt(max(0.0, starRadius * starRadius - r * r));
        vec3 n = normalize(vec3(p.x, p.y, z));

        // Rotate star surface over time
        float rot = uTime * 0.22;
        mat2 m = mat2(cos(rot), -sin(rot), sin(rot), cos(rot));
        vec3 surfCoord = n;
        surfCoord.xz = m * surfCoord.xz;

        // Domain-warped 3D FBM for solar granulation & sunspot filaments
        float q = fbm(surfCoord * 4.2 + vec3(0.0, uTime * 0.35, uTime * 0.2));
        float cells = fbm(surfCoord * 8.5 + vec3(q * 2.4 - uTime * 0.4));

        // Limb darkening + rim flare
        float ndotv = n.z;
        float limb = pow(ndotv, 0.42);
        float rimGlow = pow(1.0 - ndotv, 3.2) * (0.65 + uBeat * 0.45);

        float heat = (cells * 0.85 + q * 0.45) * limb + rimGlow + uBeat * 0.18;
        col = firePalette(heat);
    }
    else
    {
        // Erupting Solar Corona Flames & Magnetic Prominence Loops outside the star limb
        float distFromLimb = r - starRadius;

        // Radial flame turbulence flowing outward
        vec3 flameCoord = vec3(
            angle * 5.5,
            distFromLimb * 4.2 - uTime * 1.35,
            uTime * 0.45
        );
        float f1 = fbm(flameCoord);
        float f2 = fbm(flameCoord * 2.1 - vec3(f1 * 2.0, uTime * 1.9, 0.0));
        float flameNoise = f1 * 0.6 + f2 * 0.45;

        // Magnetic loop prominences (periodic arches along the limb)
        float archWave = abs(sin(angle * 7.0 + sin(uTime * 0.6 + angle * 3.0) * 1.4));
        float archHeight = (0.18 + 0.16 * f1 + uBass * 0.09) * pow(archWave, 1.6);
        float prominence = exp(-distFromLimb / max(0.02, archHeight)) * smoothstep(0.15, 0.85, flameNoise);

        // Continuous coronal halo
        float halo = exp(-distFromLimb * (3.6 - uBass * 0.7)) * (0.65 + 0.65 * flameNoise);

        float totalCorona = prominence * 1.15 + halo * 0.85;
        col += firePalette(totalCorona * (1.05 + uBeat * 0.28));

        // Anamorphic horizontal solar lens flare
        float lensFlare = exp(-abs(p.y - starRadius * 0.72) * 18.0) * exp(-abs(p.x) * 1.4) * (0.28 + uBeat * 0.25);
        col += vec3(1.0, 0.75, 0.45) * lensFlare;
    }

    finalColor = vec4(col, 1.0);
}
";

    public void Load(SceneContext ctx)
    {
        Image img = Raylib.GenImageColor(2, 2, Color.White);
        _dummyTex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        _coronaShader = ShaderHelper.LoadFragmentShader(CoronaFragmentShader);
        _locRes = Raylib.GetShaderLocation(_coronaShader, "uResolution");
        _locTime = Raylib.GetShaderLocation(_coronaShader, "uTime");
        _locBass = Raylib.GetShaderLocation(_coronaShader, "uBass");
        _locBeat = Raylib.GetShaderLocation(_coronaShader, "uBeat");

        var rng = new Random(777);
        for (int i = 0; i < SparkCount; i++)
        {
            _sparks[i] = SpawnSpark(rng, ctx.ScreenWidth, ctx.ScreenHeight, randomLife: true);
        }
    }

    public void OnEnter(SceneContext ctx)
    {
    }

    private static SolarSpark SpawnSpark(Random rng, int w, int h, bool randomLife)
    {
        float maxLife = 1.2f + (float)rng.NextDouble() * 2.5f;
        return new SolarSpark
        {
            Pos = new Vector2((float)rng.NextDouble() * w, h * (0.45f + 0.55f * (float)rng.NextDouble())),
            Vel = new Vector2(-120f + (float)rng.NextDouble() * 240f, -90f - (float)rng.NextDouble() * 220f),
            Life = randomLife ? (float)rng.NextDouble() * maxLife : 0f,
            MaxLife = maxLife,
            Size = 1.5f + (float)rng.NextDouble() * 3.2f
        };
    }

    public void Update(SceneContext ctx)
    {
        var rng = Random.Shared;
        for (int i = 0; i < SparkCount; i++)
        {
            ref SolarSpark s = ref _sparks[i];
            s.Life += ctx.DeltaTime;
            if (s.Life >= s.MaxLife)
            {
                s = SpawnSpark(rng, ctx.ScreenWidth, ctx.ScreenHeight, randomLife: false);
                continue;
            }

            // Turbulent upward solar wind drift
            float swirl = MathF.Sin(ctx.TotalTime * 3.5f + i * 0.7f) * 65f;
            s.Pos.X += (s.Vel.X + swirl) * ctx.DeltaTime;
            s.Pos.Y += s.Vel.Y * (1.0f + ctx.BeatPulse * 0.6f) * ctx.DeltaTime;
        }
    }

    public void Render(SceneContext ctx)
    {
        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;

        // 1. Render Fullscreen GPU Raymarched Star & Corona Flames
        ShaderHelper.SetVec2(_coronaShader, _locRes, ctx.Resolution);
        ShaderHelper.SetFloat(_coronaShader, _locTime, ctx.TotalTime);
        ShaderHelper.SetFloat(_coronaShader, _locBass, ctx.BassEnergy);
        ShaderHelper.SetFloat(_coronaShader, _locBeat, ctx.BeatPulse);
        ShaderHelper.DrawFullscreenShaderQuad(_coronaShader, _dummyTex, w, h);

        // 2. Render Foreground Coronal Mass Ejection (CME) Plasma Sparks
        Raylib.BeginBlendMode(BlendMode.Additive);
        for (int i = 0; i < SparkCount; i++)
        {
            ref readonly SolarSpark s = ref _sparks[i];
            float norm = s.Life / s.MaxLife;
            float env = MathF.Sin(norm * MathF.PI);
            byte alpha = (byte)(env * 220);

            Vector2 tail = s.Pos - Vector2.Normalize(s.Vel) * (10f + ctx.BeatPulse * 14f);
            Raylib.DrawLineEx(tail, s.Pos, s.Size, new Color((byte)255, (byte)165, (byte)45, alpha));
            Raylib.DrawCircleV(s.Pos, s.Size * 0.7f, new Color((byte)255, (byte)245, (byte)200, alpha));
        }
        Raylib.EndBlendMode();
    }

    public void Dispose()
    {
        Raylib.UnloadShader(_coronaShader);
        Raylib.UnloadTexture(_dummyTex);
    }
}

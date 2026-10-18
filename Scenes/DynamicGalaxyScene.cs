using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Scenes;

/// <summary>
/// Scene 4: Dynamic Spiral Galaxy
/// Combines a volumetric galactic core & spiral nebula backdrop shader with a 5,500-star
/// differential-rotation 3D particle system and an orbiting/diving 3D camera.
/// </summary>
public sealed class DynamicGalaxyScene : ISpaceScene
{
    public string Name => "Dynamic Spiral Galaxy";
    public string Subtitle => "5,500 Keplerian Orbiting 3D Stars + H-II Nebula Knots + Volumetric Galactic Core";
    public float DefaultDurationSeconds => 14.0f;

    private struct GalacticParticle
    {
        public float Radius;
        public float Angle;
        public float AngularSpeed;
        public float HeightY;
        public float Size;
        public Color Color;
        public bool IsNebulaKnot;
    }

    private const int ParticleCount = 5500;
    private readonly GalacticParticle[] _particles = new GalacticParticle[ParticleCount];

    private Shader _bgShader;
    private Texture2D _dummyTex;
    private int _locRes, _locTime, _locBass;

    private const string GalaxyBgFragmentShader = @"#version 330
in vec2 fragTexCoord;
out vec4 finalColor;

uniform vec2 uResolution;
uniform float uTime;
uniform float uBass;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void main()
{
    vec2 uv = (fragTexCoord - 0.5) * 2.0;
    uv.x *= uResolution.x / max(1.0, uResolution.y);

    float r = length(uv);

    // Deep intergalactic backdrop with subtle core halo
    vec3 col = vec3(0.008, 0.012, 0.032);
    col += vec3(0.18, 0.10, 0.32) * exp(-r * 2.2) * (0.65 + uBass * 0.35);
    col += vec3(0.55, 0.38, 0.18) * exp(-r * 5.5) * (0.7 + uBass * 0.4);

    // Distant background galaxies & stars
    vec2 grid = floor((uv + vec2(uTime * 0.008, 0.0)) * 160.0);
    float star = pow(hash(grid), 28.0);
    col += vec3(0.75, 0.85, 1.0) * star;

    finalColor = vec4(col, 1.0);
}
";

    public void Load(SceneContext ctx)
    {
        Image img = Raylib.GenImageColor(2, 2, Color.White);
        _dummyTex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);

        _bgShader = ShaderHelper.LoadFragmentShader(GalaxyBgFragmentShader);
        _locRes = Raylib.GetShaderLocation(_bgShader, "uResolution");
        _locTime = Raylib.GetShaderLocation(_bgShader, "uTime");
        _locBass = Raylib.GetShaderLocation(_bgShader, "uBass");

        InitGalaxyParticles();
    }

    public void OnEnter(SceneContext ctx)
    {
    }

    private void InitGalaxyParticles()
    {
        var rng = new Random(9001);
        const int numArms = 4;
        const float armWind = 3.6f;
        const float galaxyRadius = 320f;

        for (int i = 0; i < ParticleCount; i++)
        {
            // Exponential concentration toward the galactic bulge
            float u = (float)rng.NextDouble();
            float rNorm = MathF.Pow(u, 1.45f);
            float radius = 4.0f + rNorm * galaxyRadius;

            int arm = i % numArms;
            float baseArmAngle = (arm * MathF.Tau / numArms) + MathF.Log(1.0f + radius * 0.045f) * armWind;

            // Scatter widens outward, plus a halo component
            float scatter = (0.18f + 0.48f * rNorm) * ((float)rng.NextDouble() + (float)rng.NextDouble() - 1.0f);
            if (rng.NextDouble() < 0.15)
            {
                // Spherical halo / globular cluster stars
                scatter = (float)(rng.NextDouble() * MathF.Tau);
            }

            float angle = baseArmAngle + scatter;

            // Differential Keplerian-like rotation speed (inner core rotates faster)
            float angularSpeed = 0.22f + 14.0f / (radius + 18.0f);

            // Galactic disc thickness (thicker in central bulge, thin in outer spiral arms)
            float thickness = 26.0f * MathF.Exp(-rNorm * 2.4f) + 4.5f;
            float heightY = ((float)rng.NextDouble() + (float)rng.NextDouble() - 1.0f) * thickness;

            bool isKnot = (i % 19 == 0) && rNorm > 0.12f && rNorm < 0.85f;

            Color col;
            if (rNorm < 0.18f)
            {
                // Warm golden-white galactic core
                col = new Color((byte)255, (byte)225, (byte)165, (byte)245);
            }
            else if (isKnot)
            {
                // Glowing magenta/pink H-II star-forming nebula region
                col = (i % 2 == 0)
                    ? new Color((byte)255, (byte)75, (byte)185, (byte)68)
                    : new Color((byte)60, (byte)200, (byte)255, (byte)65);
            }
            else if (rNorm < 0.55f)
            {
                // Young blue-white spiral arm giants
                col = new Color((byte)145, (byte)215, (byte)255, (byte)220);
            }
            else
            {
                // Outer cobalt/violet disc stars
                col = new Color((byte)165, (byte)155, (byte)255, (byte)195);
            }

            _particles[i] = new GalacticParticle
            {
                Radius = radius,
                Angle = angle,
                AngularSpeed = angularSpeed,
                HeightY = heightY,
                Size = isKnot ? (11f + (float)rng.NextDouble() * 12f) : (1.1f + (float)rng.NextDouble() * 2.1f),
                Color = col,
                IsNebulaKnot = isKnot
            };
        }
    }

    public void Update(SceneContext ctx)
    {
        float speedMult = 1.0f + ctx.BassEnergy * 0.45f;
        for (int i = 0; i < ParticleCount; i++)
        {
            _particles[i].Angle += _particles[i].AngularSpeed * speedMult * ctx.DeltaTime;
        }
    }

    public void Render(SceneContext ctx)
    {
        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;

        // 1. Render deep intergalactic background shader
        ShaderHelper.SetVec2(_bgShader, _locRes, ctx.Resolution);
        ShaderHelper.SetFloat(_bgShader, _locTime, ctx.TotalTime);
        ShaderHelper.SetFloat(_bgShader, _locBass, ctx.BassEnergy);
        ShaderHelper.DrawFullscreenShaderQuad(_bgShader, _dummyTex, w, h);

        // 2. Compute 3D Camera Orbit & Pitch Sweep around the Galaxy
        float camOrbit = ctx.TotalTime * 0.28f;
        float tiltX = 0.62f + 0.34f * MathF.Sin(ctx.TotalTime * 0.42f); // Tilted 3D perspective
        float rollZ = 0.22f * MathF.Cos(ctx.TotalTime * 0.31f);
        float camDist = 370f - 75f * MathF.Sin(ctx.TotalTime * 0.36f);

        float cosOrbit = MathF.Cos(camOrbit), sinOrbit = MathF.Sin(camOrbit);
        float cosTilt = MathF.Cos(tiltX), sinTilt = MathF.Sin(tiltX);
        float cosRoll = MathF.Cos(rollZ), sinRoll = MathF.Sin(rollZ);

        float centerX = w * 0.5f;
        float centerY = h * 0.5f;
        float fov = h * 0.88f;

        Raylib.BeginBlendMode(BlendMode.Additive);

        // Supermassive Galactic Core Volumetric Glow
        float coreRadius = (95f + ctx.BeatPulse * 28f) * (350f / camDist);
        Raylib.DrawCircleGradient((int)centerX, (int)centerY, coreRadius * 2.1f,
            new Color((byte)255, (byte)210, (byte)135, (byte)155),
            new Color((byte)140, (byte)50, (byte)220, (byte)0));
        Raylib.DrawCircleGradient((int)centerX, (int)centerY, coreRadius * 0.65f,
            new Color((byte)255, (byte)250, (byte)235, (byte)240),
            new Color((byte)255, (byte)180, (byte)90, (byte)0));

        // Project and render 5,500 3D Galactic Stars & H-II Nebula Knots
        for (int i = 0; i < ParticleCount; i++)
        {
            ref readonly GalacticParticle p = ref _particles[i];

            float gx = MathF.Cos(p.Angle) * p.Radius;
            float gz = MathF.Sin(p.Angle) * p.Radius;
            float gy = p.HeightY;

            // Orbit rotation around Y
            float x1 = gx * cosOrbit - gz * sinOrbit;
            float z1 = gx * sinOrbit + gz * cosOrbit;
            float y1 = gy;

            // Pitch tilt around X
            float y2 = y1 * cosTilt - z1 * sinTilt;
            float z2 = y1 * sinTilt + z1 * cosTilt + camDist;

            if (z2 <= 15f) continue;

            // Roll around Z
            float x3 = x1 * cosRoll - y2 * sinRoll;
            float y3 = x1 * sinRoll + y2 * cosRoll;

            float scale = fov / z2;
            Vector2 screenPos = new(centerX + x3 * scale, centerY + y3 * scale);

            if (screenPos.X < -60 || screenPos.X > w + 60 || screenPos.Y < -60 || screenPos.Y > h + 60)
                continue;

            float size = p.Size * scale * 0.42f;

            if (p.IsNebulaKnot)
            {
                float knotRadius = Math.Clamp(size, 4f, 34f) * (1.0f + ctx.BeatPulse * 0.22f);
                Raylib.DrawCircleGradient((int)screenPos.X, (int)screenPos.Y, knotRadius,
                    p.Color, new Color(p.Color.R, p.Color.G, p.Color.B, (byte)0));
            }
            else
            {
                float r = Math.Clamp(size, 0.85f, 3.8f);
                Raylib.DrawCircleV(screenPos, r, p.Color);
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

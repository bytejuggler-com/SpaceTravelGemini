using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Rendering;

/// <summary>
/// Offscreen HDR / Bloom / Chromatic Aberration / Toggleable CRT post-processing pipeline.
/// </summary>
public sealed class PostProcessPipeline : IDisposable
{
    private RenderTexture2D _target;
    private Shader _postShader;

    private int _locResolution;
    private int _locTime;
    private int _locTransition;
    private int _locBeatPulse;
    private int _locBloomEnabled;
    private int _locCrtEnabled;

    private const string FragmentShaderSource = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform sampler2D texture0;
uniform vec2 uResolution;
uniform float uTime;
uniform float uTransition;
uniform float uBeatPulse;
uniform int uBloomEnabled;
uniform int uCrtEnabled;

vec2 crtCurve(vec2 uv)
{
    uv = uv * 2.0 - 1.0;
    vec2 offset = abs(uv.yx) / vec2(6.5, 5.0);
    uv = uv + uv * offset * offset;
    uv = uv * 0.5 + 0.5;
    return uv;
}

void main()
{
    vec2 uv = fragTexCoord;

    if (uCrtEnabled == 1)
    {
        uv = crtCurve(uv);
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        {
            finalColor = vec4(0.01, 0.01, 0.02, 1.0);
            return;
        }
    }

    vec2 centerOffset = uv - vec2(0.5);
    float dist = length(centerOffset);

    // Radial warp & chromatic aberration during scene transitions and heavy bass beats
    float warpStrength = uTransition * 0.14;
    vec2 warpedUv = uv - centerOffset * (warpStrength * dist);

    float chroma = 0.0012 + uBeatPulse * 0.0018 + uTransition * 0.018;
    vec2 dir = centerOffset * chroma;

    float r = texture(texture0, clamp(warpedUv + dir, 0.001, 0.999)).r;
    float g = texture(texture0, clamp(warpedUv, 0.001, 0.999)).g;
    float b = texture(texture0, clamp(warpedUv - dir, 0.001, 0.999)).b;
    vec3 col = vec3(r, g, b);

    // Multi-tap bright-pass bloom
    if (uBloomEnabled == 1)
    {
        vec2 texel = 1.0 / uResolution;
        vec3 bloom = vec3(0.0);
        float totalWeight = 0.0;

        for (int x = -2; x <= 2; x++)
        {
            for (int y = -2; y <= 2; y++)
            {
                vec2 sampleUv = clamp(warpedUv + vec2(float(x), float(y)) * texel * 2.8, 0.001, 0.999);
                vec3 s = texture(texture0, sampleUv).rgb;
                float lum = dot(s, vec3(0.299, 0.587, 0.114));
                float bright = max(0.0, lum - 0.35);
                float w = 1.0 / (1.0 + float(x*x + y*y));
                bloom += s * (bright * 1.4 + 0.15) * w;
                totalWeight += w;
            }
        }
        bloom /= totalWeight;
        col += bloom * (0.55 + uBeatPulse * 0.25);
    }

    // Hyperspace flash during peak scene transition
    if (uTransition > 0.001)
    {
        float flash = pow(uTransition, 2.2);
        col = mix(col, vec3(0.75, 0.92, 1.0), flash * 0.85);
    }

    // Optional Retro CRT Scanlines & Phosphor grille
    if (uCrtEnabled == 1)
    {
        float scanline = sin(uv.y * uResolution.y * 3.14159) * 0.12;
        col -= scanline;
        float grille = 0.96 + 0.04 * sin(uv.x * uResolution.x * 1.5);
        col *= grille;
    }

    // Subtle cinematic vignette
    float vig = smoothstep(0.95, 0.28, dist);
    col *= mix(0.65, 1.0, vig);

    finalColor = vec4(clamp(col, 0.0, 1.0), 1.0);
}
";

    public void Initialize(int width, int height)
    {
        _target = Raylib.LoadRenderTexture(width, height);
        Raylib.SetTextureFilter(_target.Texture, TextureFilter.Bilinear);

        _postShader = ShaderHelper.LoadFragmentShader(FragmentShaderSource);
        _locResolution = Raylib.GetShaderLocation(_postShader, "uResolution");
        _locTime = Raylib.GetShaderLocation(_postShader, "uTime");
        _locTransition = Raylib.GetShaderLocation(_postShader, "uTransition");
        _locBeatPulse = Raylib.GetShaderLocation(_postShader, "uBeatPulse");
        _locBloomEnabled = Raylib.GetShaderLocation(_postShader, "uBloomEnabled");
        _locCrtEnabled = Raylib.GetShaderLocation(_postShader, "uCrtEnabled");
    }

    public void EnsureSize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        if (_target.Texture.Width != width || _target.Texture.Height != height)
        {
            Raylib.UnloadRenderTexture(_target);
            _target = Raylib.LoadRenderTexture(width, height);
            Raylib.SetTextureFilter(_target.Texture, TextureFilter.Bilinear);
        }
    }

    public void BeginCapture()
    {
        Raylib.BeginTextureMode(_target);
        Raylib.ClearBackground(Color.Black);
    }

    public void EndCapture()
    {
        Raylib.EndTextureMode();
    }

    public void Present(SceneContext ctx, float transitionProgress)
    {
        ShaderHelper.SetVec2(_postShader, _locResolution, ctx.Resolution);
        ShaderHelper.SetFloat(_postShader, _locTime, ctx.TotalTime);
        ShaderHelper.SetFloat(_postShader, _locTransition, transitionProgress);
        ShaderHelper.SetFloat(_postShader, _locBeatPulse, ctx.BeatPulse);
        ShaderHelper.SetInt(_postShader, _locBloomEnabled, ctx.BloomEnabled ? 1 : 0);
        ShaderHelper.SetInt(_postShader, _locCrtEnabled, ctx.CrtEnabled ? 1 : 0);

        Raylib.BeginShaderMode(_postShader);
        // Flip Y because OpenGL RenderTexture is bottom-up
        Rectangle src = new(0, 0, _target.Texture.Width, -_target.Texture.Height);
        Rectangle dst = new(0, 0, ctx.ScreenWidth, ctx.ScreenHeight);
        Raylib.DrawTexturePro(_target.Texture, src, dst, Vector2.Zero, 0f, Color.White);
        Raylib.EndShaderMode();
    }

    public void Dispose()
    {
        Raylib.UnloadRenderTexture(_target);
        Raylib.UnloadShader(_postShader);
    }
}

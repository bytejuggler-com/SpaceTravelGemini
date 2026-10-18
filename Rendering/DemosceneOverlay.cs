using System.Numerics;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Rendering;

/// <summary>
/// Classic Amiga / PC Demoscene HUD overlay:
/// - Top telemetry bar with scene selector pills, progress bar, and live audio spectrum
/// - Bottom classic sine-wave copper scroller with greetings & controls
/// </summary>
public sealed class DemosceneOverlay
{
    private const string ScrollText =
        "   *** WELCOME TO STARSHIP ODYSSEY : GPU DEMOSCENE EDITION ***   " +
        "GREETINGS TO ALL CLASSIC AMIGA & PC DEMOSCENE LEGENDS (FUTURE CREW, SANITY, TRSI, FAIRLIGHT, FARBRAUSCH)!   " +
        "CONTROLS: [LEFT/RIGHT] OR [1-4] SWITCH SCENE  |  [SPACE] LOCK SCENE  |  [C] CRT SCANLINES  |  [B] HDR BLOOM  |  [H] HIDE HUD  |  [M] MUTE SYNTH  |  [F11] FULLSCREEN   " +
        "ENJOY THE PROCEDURAL GLSL SHADERS AND REALTIME SPACE SYNTH!   ";

    private float _scrollOffset;

    public void Update(SceneContext ctx)
    {
        _scrollOffset += ctx.DeltaTime * 115f;
        float textPixelWidth = ScrollText.Length * 14f;
        if (_scrollOffset > textPixelWidth)
        {
            _scrollOffset -= textPixelWidth;
        }
    }

    public void Render(SceneContext ctx, SceneSequencer sequencer)
    {
        if (!ctx.HudEnabled) return;

        int w = ctx.ScreenWidth;
        int h = ctx.ScreenHeight;

        // 1. Top Telemetry & Scene Header Bar
        Raylib.DrawRectangleGradientV(0, 0, w, 68, new Color(4, 8, 22, 215), new Color(4, 8, 22, 0));
        Raylib.DrawLine(0, 52, w, 52, new Color(0, 210, 255, 70));

        var scene = sequencer.CurrentScene;
        string sceneTag = $"SCENE 0{sequencer.CurrentIndex + 1}/0{sequencer.Scenes.Count} : {scene.Name.ToUpperInvariant()}";
        Raylib.DrawText(sceneTag, 20, 12, 20, new Color(80, 235, 255, 255));
        Raylib.DrawText(scene.Subtitle, 20, 34, 14, new Color(180, 210, 240, 200));

        // Scene progress bar
        float progress = Math.Clamp(ctx.SceneTime / scene.DefaultDurationSeconds, 0f, 1f);
        int barX = 20;
        int barY = 51;
        int barW = 260;
        Raylib.DrawRectangle(barX, barY, barW, 3, new Color(30, 50, 80, 180));
        Raylib.DrawRectangle(barX, barY, (int)(barW * progress), 3,
            sequencer.AutoAdvance ? new Color(0, 255, 200, 255) : new Color(255, 190, 50, 255));

        // Status Badges (Top Right)
        int rightX = w - 340;
        DrawBadge(rightX, 14, sequencer.AutoAdvance ? "AUTO-SEQ" : "LOCKED", sequencer.AutoAdvance ? new Color(0, 220, 160, 255) : new Color(255, 175, 40, 255));
        DrawBadge(rightX + 86, 14, ctx.BloomEnabled ? "BLOOM:ON" : "BLOOM:OFF", ctx.BloomEnabled ? new Color(80, 200, 255, 255) : new Color(110, 120, 140, 200));
        DrawBadge(rightX + 172, 14, ctx.CrtEnabled ? "CRT:ON" : "CRT:OFF", ctx.CrtEnabled ? new Color(255, 110, 220, 255) : new Color(110, 120, 140, 200));
        Raylib.DrawText($"{Raylib.GetFPS()} FPS", w - 82, 17, 16, new Color(160, 255, 180, 230));

        // Real-time 32-band Audio Spectrum Visualizer (Top Center-Right)
        int specX = w - 520;
        int specBaseY = 44;
        for (int i = 0; i < ctx.Spectrum.Length; i++)
        {
            float val = ctx.Spectrum[i];
            int barHeight = Math.Clamp((int)(val * 28f), 2, 28);
            Color barCol = ColorFromHue(190f + i * 4.5f, 0.85f, 0.95f, 220);
            Raylib.DrawRectangle(specX + i * 5, specBaseY - barHeight, 4, barHeight, barCol);
        }

        // 2. Bottom Amiga-style Copper Bar & Sine-Wave Scroller
        int bottomBarH = 44;
        int bottomY = h - bottomBarH;
        Raylib.DrawRectangleGradientV(0, bottomY - 16, w, bottomBarH + 16, new Color(4, 6, 18, 0), new Color(4, 6, 18, 230));

        // Animated Copper Raster Lines
        float copperPulse = 0.5f + 0.5f * ctx.BeatPulse;
        Raylib.DrawLine(0, bottomY, w, bottomY, new Color((int)(60 + copperPulse * 140), 210, 255, 140));
        Raylib.DrawLine(0, bottomY + 2, w, bottomY + 2, new Color(180, 70, 255, 90));

        // Sine-wave per-character scroller
        int charWidth = 14;
        int firstCharIndex = (int)(_scrollOffset / charWidth);
        float subPixel = _scrollOffset % charWidth;
        int visibleChars = (w / charWidth) + 4;

        for (int i = 0; i < visibleChars; i++)
        {
            int strIdx = (firstCharIndex + i) % ScrollText.Length;
            char c = ScrollText[strIdx];
            if (c == ' ') continue;

            float x = i * charWidth - subPixel;
            float wavePhase = ctx.TotalTime * 4.5f + (firstCharIndex + i) * 0.22f;
            float yOffset = MathF.Sin(wavePhase) * 6.5f;
            float hue = (180f + (firstCharIndex + i) * 3.5f + ctx.TotalTime * 60f) % 360f;
            Color charColor = ColorFromHue(hue, 0.75f, 1.0f, 245);

            string s = c.ToString();
            Raylib.DrawText(s, (int)x + 1, (int)(bottomY + 13 + yOffset) + 1, 20, new Color(0, 0, 0, 180));
            Raylib.DrawText(s, (int)x, (int)(bottomY + 13 + yOffset), 20, charColor);
        }
    }

    private static void DrawBadge(int x, int y, string label, Color color)
    {
        Raylib.DrawRectangleLines(x, y, 78, 22, color);
        Raylib.DrawRectangle(x, y, 78, 22, new Color(color.R, color.G, color.B, (byte)35));
        Raylib.DrawText(label, x + 7, y + 5, 12, color);
    }

    public static Color ColorFromHue(float hueDeg, float sat, float val, byte alpha = 255)
    {
        Color c = Raylib.ColorFromHSV(hueDeg % 360f, sat, val);
        c.A = alpha;
        return c;
    }
}

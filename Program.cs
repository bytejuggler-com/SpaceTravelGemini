using System.Runtime.InteropServices;
using Raylib_cs;
using SpaceTravel.Audio;
using SpaceTravel.Core;
using SpaceTravel.Rendering;
using SpaceTravel.Scenes;

namespace SpaceTravel;

public static partial class Program
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint hWnd);

    private const string LogPath = @"c:\Code\AI\Test\SpaceTravel\debug.log";
    private static void Log(string msg) => File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

    public static unsafe void Main()
    {
        File.WriteAllText(LogPath, "=== SpaceTravel Starting ===\n");
        try
        {
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint | ConfigFlags.AlwaysRunWindow);
            Raylib.InitWindow(1600, 900, "STARSHIP ODYSSEY // GPU Space Travel Demoscene (.NET + Raylib + GLSL)");
            Raylib.SetTargetFPS(60);
            Log($"InitWindow done. Handle={(nint)Raylib.GetWindowHandle()} Hidden={Raylib.IsWindowHidden()}");

            var ctx = new SceneContext
            {
                ScreenWidth = Raylib.GetScreenWidth(),
                ScreenHeight = Raylib.GetScreenHeight()
            };

            var audio = new SynthAudioEngine();
            audio.Initialize();
            Log("Audio initialized.");

            var postProcess = new PostProcessPipeline();
            postProcess.Initialize(ctx.ScreenWidth, ctx.ScreenHeight);
            Log("PostProcess initialized.");

            var sequencer = new SceneSequencer();
            sequencer.RegisterScene(new WarpStarfieldScene(), ctx);
            sequencer.RegisterScene(new HorizontalParallaxScene(), ctx);
            sequencer.RegisterScene(new GiantStarCoronaScene(), ctx);
            sequencer.RegisterScene(new DynamicGalaxyScene(), ctx);
            Log("Scenes initialized.");

            var overlay = new DemosceneOverlay();
            int frameCount = 0;

            while (!Raylib.WindowShouldClose())
            {
                frameCount++;
                ctx.ScreenWidth = Raylib.GetScreenWidth();
                ctx.ScreenHeight = Raylib.GetScreenHeight();
                ctx.DeltaTime = Math.Clamp(Raylib.GetFrameTime(), 0.0001f, 0.1f);
                ctx.TotalTime += ctx.DeltaTime;

                HandleInput(ctx, sequencer);

                audio.Update(ctx);
                sequencer.Update(ctx);
                overlay.Update(ctx);

                postProcess.EnsureSize(ctx.ScreenWidth, ctx.ScreenHeight);

                // 1. Render active scene into offscreen HDR texture
                postProcess.BeginCapture();
                sequencer.Render(ctx);
                postProcess.EndCapture();

                // 2. Present post-processed texture to screen and draw HUD overlay
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                postProcess.Present(ctx, sequencer.TransitionProgress);
                overlay.Render(ctx, sequencer);

                Raylib.EndDrawing();

                if (frameCount % 60 == 1)
                {
                    Log($"Frame {frameCount} | Scene={sequencer.CurrentIndex} | Hidden={Raylib.IsWindowHidden()} | Min={Raylib.IsWindowMinimized()} | Size={ctx.ScreenWidth}x{ctx.ScreenHeight}");
                }
            }

            Log($"Loop exited at frame {frameCount}. WindowShouldClose={Raylib.WindowShouldClose()}");
            sequencer.Dispose();
            postProcess.Dispose();
            audio.Dispose();
            Raylib.CloseWindow();
            Log("Clean exit.");
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
        }
    }

    private static void HandleInput(SceneContext ctx, SceneSequencer sequencer)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.Down))
            sequencer.NextScene(ctx);

        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Up))
            sequencer.PreviousScene(ctx);

        if (Raylib.IsKeyPressed(KeyboardKey.One)) sequencer.TriggerTransitionTo(0, ctx);
        if (Raylib.IsKeyPressed(KeyboardKey.Two)) sequencer.TriggerTransitionTo(1, ctx);
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) sequencer.TriggerTransitionTo(2, ctx);
        if (Raylib.IsKeyPressed(KeyboardKey.Four)) sequencer.TriggerTransitionTo(3, ctx);

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
            sequencer.AutoAdvance = !sequencer.AutoAdvance;

        if (Raylib.IsKeyPressed(KeyboardKey.C))
            ctx.CrtEnabled = !ctx.CrtEnabled;

        if (Raylib.IsKeyPressed(KeyboardKey.B))
            ctx.BloomEnabled = !ctx.BloomEnabled;

        if (Raylib.IsKeyPressed(KeyboardKey.H))
            ctx.HudEnabled = !ctx.HudEnabled;

        if (Raylib.IsKeyPressed(KeyboardKey.M))
            ctx.AudioMuted = !ctx.AudioMuted;

        if (Raylib.IsKeyPressed(KeyboardKey.F11) ||
            (Raylib.IsKeyDown(KeyboardKey.LeftAlt) && Raylib.IsKeyPressed(KeyboardKey.Enter)))
        {
            Raylib.ToggleFullscreen();
        }
    }
}

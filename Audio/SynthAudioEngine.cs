using System.Runtime.InteropServices;
using Raylib_cs;
using SpaceTravel.Core;

namespace SpaceTravel.Audio;

/// <summary>
/// Procedural Space-Synth & Demoscene Tracker Audio Engine.
/// Synthesizes a lush 16-second looping stereo space-synth track in memory (120 BPM)
/// and drives real-time BassEnergy, BeatPulse, and 32-band Spectrum analysis for shaders.
/// </summary>
public sealed class SynthAudioEngine : IDisposable
{
    private Sound _loopSound;
    private bool _audioReady;
    private float _trackTimer;
    private const float Bpm = 120f;
    private const float BeatPeriod = 60f / Bpm; // 0.5s per beat
    private const float LoopDuration = 16.0f;   // 32 beats (8 bars)

    // Root bass notes (Hz) for 4-bar cosmic progression: Cm -> Ab -> Eb -> Bb
    private static readonly float[] BassNotes = { 65.41f, 51.91f, 77.78f, 58.27f };

    // Arpeggio intervals (semitone ratios)
    private static readonly float[] ArpSemitones = { 0, 3, 7, 12, 15, 12, 7, 3 };

    public unsafe void Initialize()
    {
        try
        {
            Raylib.InitAudioDevice();
            if (!Raylib.IsAudioDeviceReady()) return;

            byte[] wavBytes = SynthesizeSpaceTrackWav(44100, LoopDuration);
            Wave wave = Raylib.LoadWaveFromMemory(".wav", wavBytes);
            _loopSound = Raylib.LoadSoundFromWave(wave);
            Raylib.UnloadWave(wave);

            Raylib.SetSoundVolume(_loopSound, 0.55f);
            Raylib.PlaySound(_loopSound);
            _audioReady = true;
        }
        catch
        {
            _audioReady = false;
        }
    }

    public void Update(SceneContext ctx)
    {
        _trackTimer += ctx.DeltaTime;

        if (_audioReady)
        {
            if (ctx.AudioMuted)
            {
                if (Raylib.IsSoundPlaying(_loopSound))
                    Raylib.PauseSound(_loopSound);
            }
            else
            {
                if (!Raylib.IsSoundPlaying(_loopSound))
                    Raylib.PlaySound(_loopSound);
            }
        }

        // Compute musical phase (120 BPM = 2 beats/sec, 8 steps/sec)
        float t = _trackTimer % LoopDuration;
        float beatPos = (t / BeatPeriod);
        float beatFrac = beatPos - MathF.Floor(beatPos); // 0..1 within current beat
        float sixteenthPos = beatPos * 4f;
        float sixteenthFrac = sixteenthPos - MathF.Floor(sixteenthPos);

        // Kick / Bass envelope peaks sharply at start of each beat
        float kickEnv = MathF.Pow(MathF.Max(0f, 1.0f - beatFrac * 2.4f), 2.2f);
        // Sub-bass pump on off-beats
        float subBassEnv = 0.45f + 0.55f * MathF.Sin(beatFrac * MathF.PI);

        // Arp envelope (16th notes)
        float arpEnv = MathF.Pow(MathF.Max(0f, 1.0f - sixteenthFrac * 1.6f), 1.8f);

        // Snare / clap on beats 1 and 3 (0-indexed: beatIdx % 2 == 1)
        int beatIdx = (int)MathF.Floor(beatPos);
        float snareEnv = (beatIdx % 2 == 1) ? MathF.Pow(MathF.Max(0f, 1.0f - beatFrac * 3.0f), 2.0f) : 0.1f;

        ctx.BeatPulse = kickEnv;
        ctx.BassEnergy = Math.Clamp(kickEnv * 0.7f + subBassEnv * 0.35f, 0f, 1f);
        ctx.MidEnergy = Math.Clamp(arpEnv * 0.65f + 0.25f * MathF.Sin(t * 1.5f) + 0.25f, 0f, 1f);
        ctx.HighEnergy = Math.Clamp(snareEnv * 0.7f + arpEnv * 0.3f, 0f, 1f);

        // Populate 32-band visualizer spectrum for HUD & shaders
        int arpStep = ((int)MathF.Floor(sixteenthPos)) % ArpSemitones.Length;
        for (int i = 0; i < ctx.Spectrum.Length; i++)
        {
            float normBand = i / (float)ctx.Spectrum.Length;
            float target;
            if (i < 6)
            {
                // Sub-bass & kick bands
                target = ctx.BassEnergy * (1.0f - i * 0.08f);
            }
            else if (i < 20)
            {
                // Melodic synth & arpeggio bands
                float activePeak = 1.0f - MathF.Min(1.0f, MathF.Abs((i - 6) - arpStep * 1.5f) * 0.35f);
                target = (0.25f + 0.75f * activePeak * arpEnv) * (0.7f + 0.3f * MathF.Sin(t * 3f + i));
            }
            else
            {
                // Hi-hat & shimmer bands
                float shimmer = 0.5f + 0.5f * MathF.Sin(t * 19f + i * 2.1f);
                target = (snareEnv * 0.65f + arpEnv * 0.35f * shimmer) * (1.05f - normBand * 0.4f);
            }

            // Smooth interpolation for display & shader reactivity
            ctx.Spectrum[i] = ctx.Spectrum[i] * 0.65f + Math.Clamp(target, 0.04f, 1.0f) * 0.35f;
        }
    }

    private static byte[] SynthesizeSpaceTrackWav(int sampleRate, float durationSeconds)
    {
        int numSamples = (int)(sampleRate * durationSeconds);
        int numChannels = 2;
        int dataSize = numSamples * numChannels * sizeof(short);

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF WAVE Header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // PCM format
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * numChannels * sizeof(short));
        bw.Write((short)(numChannels * sizeof(short)));
        bw.Write((short)16); // 16-bit
        bw.Write("data"u8);
        bw.Write(dataSize);

        float bassPhase = 0f;
        float arpPhase = 0f;
        float padPhase1 = 0f, padPhase2 = 0f, padPhase3 = 0f;
        uint rng = 0x12345678;

        for (int i = 0; i < numSamples; i++)
        {
            float t = i / (float)sampleRate;
            float beatPos = t / BeatPeriod;
            int barIdx = ((int)(beatPos / 8f)) % BassNotes.Length;
            float rootFreq = BassNotes[barIdx];

            float beatFrac = beatPos - MathF.Floor(beatPos);
            float sixteenthPos = beatPos * 4f;
            int stepIdx = (int)MathF.Floor(sixteenthPos);
            float stepFrac = sixteenthPos - stepIdx;

            // 1. Punchy Synth Kick + Offbeat Analog Bass
            float kickFreq = 42f + 110f * MathF.Exp(-beatFrac * 24f);
            float kickEnv = MathF.Exp(-beatFrac * 7.5f);
            bassPhase += (kickFreq * 0.55f + rootFreq * 0.45f) / sampleRate;
            float kickWave = MathF.Sin(bassPhase * MathF.Tau) * kickEnv * 0.45f;

            // Rolling 16th-note saw/sub bass
            float subFreq = (stepIdx % 2 == 1) ? rootFreq * 2f : rootFreq;
            float subSaw = (2f * ((t * subFreq) - MathF.Floor(t * subFreq)) - 1f);
            float subEnv = MathF.Exp(-stepFrac * 4.5f) * 0.24f;
            float bassSignal = kickWave + MathF.Tanh(subSaw * 1.8f) * subEnv;

            // 2. Demoscene Chiptune / Synthwave Arpeggio
            float semi = ArpSemitones[stepIdx % ArpSemitones.Length];
            float arpFreq = (rootFreq * 4f) * MathF.Pow(2f, semi / 12f);
            arpPhase += arpFreq / sampleRate;
            float pulseWidth = 0.35f + 0.25f * MathF.Sin(t * 1.8f);
            float arpPulse = ((arpPhase - MathF.Floor(arpPhase)) < pulseWidth ? 1f : -1f);
            float arpEnv = MathF.Exp(-stepFrac * 5.2f) * 0.16f;
            float arpSignal = arpPulse * arpEnv;

            // 3. Lush Stereo Cosmic Pad (Root + Minor 3rd + Fifth)
            padPhase1 += (rootFreq * 2f) / sampleRate;
            padPhase2 += (rootFreq * 2f * 1.1892f) / sampleRate;
            padPhase3 += (rootFreq * 2f * 1.4983f) / sampleRate;
            float padL = (MathF.Sin(padPhase1 * MathF.Tau) + MathF.Sin(padPhase2 * MathF.Tau + MathF.Sin(t * 2.1f) * 0.5f)) * 0.11f;
            float padR = (MathF.Sin(padPhase1 * MathF.Tau) + MathF.Sin(padPhase3 * MathF.Tau + MathF.Cos(t * 1.9f) * 0.5f)) * 0.11f;

            // 4. Soft Hi-Hat / Space Shimmer on 16ths
            rng = rng * 1664525u + 1013904223u;
            float whiteNoise = ((rng & 0xFFFF) / 32768f) - 1f;
            float hatDecay = (stepIdx % 4 == 2) ? 14f : 28f;
            float hatSignal = whiteNoise * MathF.Exp(-stepFrac * hatDecay) * 0.06f;

            // Stereo Panning for Arpeggio
            float pan = 0.5f + 0.35f * MathF.Sin(t * 3.0f);
            float left = bassSignal + padL + arpSignal * pan + hatSignal;
            float right = bassSignal + padR + arpSignal * (1f - pan) + hatSignal;

            // Seamless crossfade at loop boundary
            float loopEnv = 1.0f;
            if (t < 0.05f) loopEnv = t / 0.05f;
            else if (t > durationSeconds - 0.05f) loopEnv = (durationSeconds - t) / 0.05f;

            short sL = (short)(Math.Clamp(MathF.Tanh(left * loopEnv), -0.98f, 0.98f) * 32767);
            short sR = (short)(Math.Clamp(MathF.Tanh(right * loopEnv), -0.98f, 0.98f) * 32767);

            bw.Write(sL);
            bw.Write(sR);
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_audioReady)
        {
            Raylib.UnloadSound(_loopSound);
            Raylib.CloseAudioDevice();
            _audioReady = false;
        }
    }
}

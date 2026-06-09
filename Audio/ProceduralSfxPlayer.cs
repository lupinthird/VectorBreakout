using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace VectorBreakout.Audio;

/// <summary>
/// One-shot procedural collision and explosion sounds via DynamicSoundEffectInstance.
/// </summary>
public sealed class ProceduralSfxPlayer : IDisposable
{
    private const int SampleRate = 44100;
    private const int VoiceCount = 10;

    private readonly List<DynamicSoundEffectInstance> _voices = new(VoiceCount);
    private int _nextVoice;

    public ProceduralSfxPlayer()
    {
        for (int i = 0; i < VoiceCount; i++)
        {
            var voice = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Mono);
            _voices.Add(voice);
        }
    }

    public void PlayBrickThump(float intensity)
    {
        Play(GenerateThump(baseHz: 52f, duration: 0.1f, intensity * 0.55f));
    }

    public void PlayWallThump(float intensity)
    {
        Play(GenerateThump(baseHz: 38f, duration: 0.14f, intensity * 0.95f));
    }

    public void PlayPaddleThump(float intensity)
    {
        Play(GenerateThump(baseHz: 62f, duration: 0.08f, intensity * 0.4f));
    }

    public void PlayExplosion(float intensity)
    {
        Play(GenerateExplosion(intensity));
    }

    public void PlayTypewriterClick(float intensity = 0.5f)
    {
        Play(GenerateTypewriterClick(intensity));
    }

    private void Play(byte[] buffer)
    {
        DynamicSoundEffectInstance voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Count;

        if (voice.State == SoundState.Playing)
        {
            voice.Stop();
        }

        voice.SubmitBuffer(buffer);
        voice.Play();
    }

    private static byte[] GenerateThump(float baseHz, float duration, float volume)
    {
        int sampleCount = System.Math.Max(1, (int)(SampleRate * duration));
        var samples = new float[sampleCount];
        float twoPi = MathHelper.TwoPi;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = MathF.Exp(-t * 28f) * (1f - MathF.Exp(-t * 220f));
            float phase = twoPi * baseHz * t;
            float body = MathF.Sin(phase) * 0.75f + MathF.Sin(phase * 0.5f) * 0.35f;
            float click = MathF.Sin(twoPi * 140f * t) * MathF.Exp(-t * 90f) * 0.12f;
            samples[i] = (body + click) * envelope * volume;
        }

        return ToPcm16(samples);
    }

    private static byte[] GenerateTypewriterClick(float volume)
    {
        const float duration = 0.028f;
        int sampleCount = System.Math.Max(1, (int)(SampleRate * duration));
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = MathF.Exp(-t * 120f);
            float tick = MathF.Sin(MathHelper.TwoPi * 920f * t) * 0.55f;
            float knock = MathF.Sin(MathHelper.TwoPi * 180f * t) * MathF.Exp(-t * 85f) * 0.35f;
            samples[i] = (tick + knock) * envelope * volume;
        }

        return ToPcm16(samples);
    }

    private static byte[] GenerateExplosion(float volume)
    {
        const float duration = 1.15f;
        int sampleCount = (int)(SampleRate * duration);
        var samples = new float[sampleCount];
        var rng = new Random(unchecked((int)Environment.TickCount));
        float brown = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            float progress = t / duration;
            float attack = 1f - MathF.Exp(-t * 6f);
            float decay = MathF.Exp(-t * 2.1f) * (1f - progress * 0.15f);
            float envelope = attack * decay;

            float hz = MathHelper.Lerp(42f, 18f, progress);
            float sub = MathF.Sin(MathHelper.TwoPi * hz * t) * 0.42f;
            float sub2 = MathF.Sin(MathHelper.TwoPi * hz * 0.5f * t) * 0.28f;

            brown += ((float)rng.NextDouble() * 2f - 1f) * 0.06f;
            brown *= 0.992f;
            float lowRumble = brown * 0.55f;

            samples[i] = (sub + sub2 + lowRumble) * envelope * volume * 0.95f;
        }

        return ToPcm16(samples);
    }

    private static byte[] ToPcm16(float[] samples)
    {
        var buffer = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = MathHelper.Clamp(samples[i], -1f, 1f);
            short sample = (short)(clamped * short.MaxValue);
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return buffer;
    }

    public void Dispose()
    {
        foreach (DynamicSoundEffectInstance voice in _voices)
        {
            voice.Stop();
            voice.Dispose();
        }

        _voices.Clear();
    }
}

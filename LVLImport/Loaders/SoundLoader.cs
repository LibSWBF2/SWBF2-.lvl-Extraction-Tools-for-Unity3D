using System;
using System.Collections.Generic;
using UnityEngine;
using LibSWBF2.Wrappers;

public class SoundLoader
{
    static Dictionary<string, AudioClip> SoundDB = new Dictionary<string, AudioClip>();

    public static void ResetDB()
    {
        SoundDB.Clear();
    }

    public static AudioClip LoadSound(string soundName)
    {
        if (SoundDB.TryGetValue(soundName, out AudioClip foundClip))
        {
            return foundClip;
        }

        RuntimeEnvironment runtime = GameRuntime.GetEnvironment();
        Sound sound = runtime.Find<Sound>(soundName);
        if (sound == null)
        {
            return null;
        }

        if (!sound.GetData(out uint sampleRate, out uint sampleCount, out byte blockAlign, out byte[] data))
        {
            Debug.LogWarningFormat("Couldn't retrdieve sound data of sound '{0}'!", soundName);
            return null;
        }
        
        Debug.Assert(blockAlign == sizeof(ushort));
        Debug.Assert(sampleCount * blockAlign == data.Length);

        float[] pcm = new float[sampleCount];

        AudioClip clip = AudioClip.Create(soundName, (int)sampleCount, 1, (int)sampleRate, false);
        for (int i = 0; i < sampleCount; ++i)
        {
            pcm[i] = (BitConverter.ToInt16(data, i * sizeof(ushort)) / 32768.0f);
        }
        
        if (!clip.SetData(pcm, 0))
        {
            Debug.LogErrorFormat("Couldn't set sound data of sound '{0}'!", soundName);
        }

        SoundDB.Add(soundName, clip);
        return clip;
    }
}
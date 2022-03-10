using System;
using System.Collections.Generic;
using UnityEngine;

using LibSWBF2.Wrappers;
using LibSWBF2.Enums;
using LibSWBF2.Utils;

public class SoundLoader : Loader
{
    public static SoundLoader Instance { get; private set; } = null;


    Dictionary<uint, AudioClip> SoundDB = new Dictionary<uint, AudioClip>();
    Dictionary<uint, uint> NameHashToClipMapping = new Dictionary<uint, uint>();


    static SoundLoader()
    {
        Instance = new SoundLoader();
    }


    public void ResetDB()
    {
        // For now, we don't clear the nametohash mapping when Resetting
        SoundDB.Clear();
    }


    public void InitializeSoundProperties(Level level)
    {
        if (level == null || !level.IsValid())
        {
            return;
        }

        foreach (Config sndCfg in level.GetConfigs(EConfigType.Sound))
        {
            foreach (Field soundField in sndCfg.GetFields("SoundProperties"))
            {
                uint name = soundField.Scope.GetUInt("Name");

                Field pitch = soundField.Scope.GetField("Pitch");

                Field sampleList;
                try {
                    sampleList = soundField.Scope.GetField("SampleList");
                }
                catch
                {
                    continue;
                }
                if (sampleList == null)
                {
                    continue;
                }

                Field[] samples = sampleList.Scope.GetFields("Sample");
                if (samples.Length > 0)
                {
                    NameHashToClipMapping[name] = samples[0].GetUInt(0);
                }
            }
        }
    }


    public AudioClip LoadSound(string soundName)
    {
        AudioClip clip = LoadSound(HashUtils.GetFNV(soundName), soundName);
        if (clip == null)
        {
            Debug.LogWarningFormat("Failed to find sound clip: {0}", soundName);
        }
        return clip;
    }

    public AudioClip LoadSound(uint soundName, string soundNameString = null)
    {
        uint clipNameHash;
        AudioClip foundClip;


        if (NameHashToClipMapping.TryGetValue(soundName, out clipNameHash))
        {
            if (SoundDB.TryGetValue(clipNameHash, out foundClip))
            {
                return foundClip;
            }
        }
        else if (SoundDB.TryGetValue(soundName, out foundClip))
        {
            return foundClip;
        }
        else
        {
            //Debug.LogWarningFormat("No sound mapping exists for {0}, attempting to get clip by name...", soundName);
            clipNameHash = soundName;
        }

        Sound sound = container.Get<Sound>(clipNameHash);
        if (sound == null)
        {
            Debug.LogWarningFormat("failed to find sound queried with: {0} (hash key: 0x{1:X})", 
                                    soundNameString == null ? soundNameString : HashUtils.FNVToString(soundName, false), 
                                    clipNameHash);
            return null;
        }

        if (!sound.GetData(out uint sampleRate, out uint sampleCount, out byte blockAlign, out byte[] data))
        {
            Debug.LogWarningFormat("Couldn't retrieve sound data of sound '{0}'! (hash key: 0x{1:X})", 
                                    soundNameString == null ? soundNameString : HashUtils.FNVToString(soundName, false), 
                                    clipNameHash);
            return null;
        }
        
        Debug.Assert(blockAlign == sizeof(ushort));
        Debug.Assert(sampleCount * blockAlign == data.Length);

        float[] pcm = new float[sampleCount];

        AudioClip clip = AudioClip.Create(soundName.ToString(), (int)sampleCount, 1, (int)sampleRate, false);
        for (int i = 0; i < sampleCount; ++i)
        {
            pcm[i] = (BitConverter.ToInt16(data, i * sizeof(ushort)) / 32768.0f);
        }
        
        if (!clip.SetData(pcm, 0))
        {
            Debug.LogErrorFormat("Couldn't set sound data of sound '{0}'! (hash key: 0x{1:X})", 
                                soundNameString == null ? soundNameString : HashUtils.FNVToString(soundName, false), 
                                clipNameHash);
        }

        SoundDB.Add(clipNameHash, clip);

        return clip;
    }
}
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public class AnimationLoader : Loader {

    private static Dictionary<uint, AnimationClip> animDatabase = new Dictionary<uint, AnimationClip>();


    private static string[] ComponentPaths = {  "localRotation.x",
                                                "localRotation.y",
                                                "localRotation.z",
                                                "localRotation.w",
                                                "localPosition.x",
                                                "localPosition.y",
                                                "localPosition.z"  };

    private static float[] ComponentMultipliers = {  -1.0f,
                                                      1.0f,
                                                     1.0f,
                                                     -1.0f,
                                                      -1.0f,
                                                      1.0f,
                                                     1.0f  };   


    private static Dictionary<uint, string> crcsToCommonNames;

    static AnimationLoader()
    {
        crcsToCommonNames = new Dictionary<uint, string>();

        string[] CommonNames = {
            "death01", "idle", "idle_leftup", "idle_rightup",
            "idle_to_leftfoot", "idle_to_leftfoot_leftup",
            "idle_to_leftfoot_rightup", "leftfoot_to_idle",
            "leftfoot_to_idle_leftup", "leftfoot_to_idle_rightup",
            "rightfoot_to_idle", "rightfoot_to_idle_leftup",
            "rightfoot_to_idle_rightup", "turnl", "turnl_leftup",
            "turnl_rightup", "turnr", "turnr_leftup", "turnr_rightup",
            "walk_leftfoot_rightfoot", "walk_leftfoot_rightfoot_leftup",
            "walk_leftfoot_rightfoot_rightup", "walk_rightfoot_leftfoot",
            "walk_rightfoot_leftfoot_leftup", "walk_rightfoot_leftfoot_rightup",
            "grab", "idle", "9pose", "takeoff"
        }; 

        foreach (string name in CommonNames)
        {
            crcsToCommonNames[HashUtils.GetCRC(name)] = name;
        }
    } 

    public static string TryFindAnimationName(uint nameCrc)
    {
        if (crcsToCommonNames.ContainsKey(nameCrc))
        {
            return crcsToCommonNames[nameCrc];
        }
        return String.Format("0x{0:x}", nameCrc);
    }


    public static void ResetDB()
    {
        animDatabase.Clear();
    }

    private static void WalkSkeletonAndCreateCurves(ref AnimationClip clip, AnimationBank animBank,
    										Transform bone, string curPath, uint animHash)
    {
    	uint boneHash = HashUtils.GetCRC(bone.name);
		
    	string relPath = curPath + bone.name;


    	animBank.GetAnimationMetadata(animHash, out int frameCap, out int numBones);

    	for (int i = 0; i < 7; i++)
    	{
            float mult = ComponentMultipliers[i];

			if (animBank.GetCurve(animHash, boneHash, (uint) i,
	                    out ushort[] inds, out float[] values))
			{
				Keyframe[] frames = new Keyframe[values.Length];
				for (int j = 0; j < values.Length; j++)
				{
					int index = (int) inds[j];
					frames[j] = new Keyframe(index < frameCap ? index / 30.0f : frameCap / 30.0f, mult * values[j]);
				}
				var curve = new AnimationCurve(frames);
				clip.SetCurve(relPath, typeof(Transform), ComponentPaths[i], curve);
			}
		}

		for (int i = 0; i < bone.childCount; i++)
		{
			WalkSkeletonAndCreateCurves(ref clip, animBank, bone.GetChild(i), relPath + "/", animHash);
		}
    }

    
    public static List<AnimationClip> LoadAnimationBank(string animBankName, Transform tran)
    {
        List<AnimationClip> clips = new List<AnimationClip>();

        var bank = CentralLoader.GetAnimationBank(animBankName);

        if (bank == null)
        {
            return clips;
        }

        var animCRCs = bank.GetAnimationCRCs();
        foreach (uint animCRC in animCRCs)
        {
            var clip = LoadAnimationClip(animBankName, animCRC, tran);
            clip.name = TryFindAnimationName(animCRC);
            clips.Add(clip);
        }

        return clips;
    }    


    public static AnimationClip LoadAnimationClip(string animBankName, string animationName, Transform objectTransform)
    {   
        //uint animBankCRC = HashUtils.GetCRC(animBankName);
        uint animCRC = HashUtils.GetCRC(animationName);
        return LoadAnimationClip(animBankName, animCRC, objectTransform);
    }


    public static AnimationClip LoadAnimationClip(string animBankName, uint animationName, Transform objectTransform)
    {
    	uint animID = HashUtils.GetCRC(animBankName) * animationName;//HashUtils.GetCRC(animBankName + "/" + animationName);

    	if (animDatabase.ContainsKey(animID))
    	{
    		return animDatabase[animID];
    	}

    	var animBank = container.FindWrapper<AnimationBank>(animBankName);

    	if (animBank == null)
    	{
    		Debug.LogError(String.Format("AnimationBank {0} failed to load!", animBankName));
    		return null;
    	}

    	uint animCRC = animationName; //HashUtils.GetCRC(animationName);

    	if (objectTransform != null && animBank.GetAnimationMetadata(animCRC, out int numFrames, out int numBones))
    	{
    		var clip = new AnimationClip();
    		clip.legacy = true;

    		for (int i = 0; i < objectTransform.childCount; i++)
    		{
	    		WalkSkeletonAndCreateCurves(ref clip, animBank, objectTransform.GetChild(i), "", animCRC);
    		}

    		animDatabase[animID] = clip;

    		return clip;
    	}
    	else 
    	{
    		Debug.LogError(String.Format("AnimationBank {0} does contain the animation: {1}!", animBankName, animationName));
    		return null;
    	}
    }
}
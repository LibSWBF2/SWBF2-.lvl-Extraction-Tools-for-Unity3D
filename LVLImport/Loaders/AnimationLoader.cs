

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




public class AnimationLoader : ScriptableObject {

    //private static Dictionary<uint, AnimationClip> animDatabase = new Dictionary<uint, AnimationClip>();


    private static string[] ComponentPaths = {  "localRotation.x",
											    "localRotation.y",
											    "localRotation.z",
											    "localRotation.w",
    										    "localPosition.x",
    										    "localPosition.y",
    										    "localPosition.z"  };


    private static void WalkSkeletonAndCreateCurves(ref AnimationClip clip, AnimationSet animSet,
    										Transform bone, string curPath, uint animHash)
    {
    	uint boneHash = HashUtils.GetCRC(bone.name);
		
    	string relPath = curPath + bone.name;

    	Debug.Log("Setting up path: " + relPath);

    	animSet.GetAnimationMetadata(animHash, out int frameCap, out int numBones);

    	for (int i = 0; i < 7; i++)
    	{
			if (animSet.GetCurve(animHash, boneHash, (uint) i,
	                    out ushort[] inds, out float[] values))
			{
				Keyframe[] frames = new Keyframe[values.Length];
				for (int j = 0; j < values.Length; j++)
				{
					int index = (int) inds[j];
					frames[j] = new Keyframe(index < frameCap ? index : frameCap, values[j]);
				}
				var curve = new AnimationCurve(frames);
				clip.SetCurve(relPath, typeof(Transform), ComponentPaths[i], curve);
			}
		}

		for (int i = 0; i < bone.childCount; i++)
		{
			WalkSkeletonAndCreateCurves(ref clip, animSet, bone.GetChild(i), relPath + "/", animHash);
		}
    }




    public static void LoadAndAttachAnimation(Level level, string animationName, string animSetName)
    {
    	var animSet = level.GetAnimationSet(animSetName);

    	if (animSet == null)
    	{
    		Debug.Log("Couldn't find animation set!");
    		return;
    	}

    	GameObject rancor = GameObject.Find("tat3_bldg_rancor");

    	if (rancor == null)
    	{
    		Debug.Log("Couldn't find the rancor!");
    		return;
    	}

    	uint animCRC = HashUtils.GetCRC(animationName);

    	if (animSet.GetAnimationMetadata(animCRC, out int numFrames, out int numBones))
    	{
    		var anim = rancor.GetComponent<Animation>();
    		var clip = new AnimationClip();
    		clip.legacy = true;

    		for (int i = 0; i < rancor.transform.childCount; i++)
    		{
	    		WalkSkeletonAndCreateCurves(ref clip, animSet, rancor.transform.GetChild(i), "", animCRC);
    		}

    		anim.AddClip(clip, clip.name);
        	//anim.Play(clip.name);
    	}
    }
}



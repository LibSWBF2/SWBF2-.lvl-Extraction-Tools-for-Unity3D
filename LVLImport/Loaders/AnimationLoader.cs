using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class AnimationLoader : ScriptableObject {

    private static Dictionary<uint, AnimationClip> animDatabase = new Dictionary<uint, AnimationClip>();


    public static GetAnimationClip(uint setNameCRC, uint animNameCRC)
    {
        uint combinedCRC = setNameCRC * animNameCRC;
    }



    public static GetAnimationClip(string setName, string animName)
    {

    }









}

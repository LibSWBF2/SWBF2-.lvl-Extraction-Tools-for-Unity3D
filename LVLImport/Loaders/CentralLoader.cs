using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEditor;
//using UnityEngine.ScriptableObject;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class CentralLoader : UnityEngine.ScriptableObject {

    private static LibSWBF2.Wrappers.Container container = null;

    public static bool SetContainer(Container lvlContainer)
    {
    	container = lvlContainer;

        ModelLoader.ResetDB();
        AnimationLoader.ResetDB();
        TextureLoader.ResetDB();
        ClassLoader.ResetDB();

    	return true;
    }

    public static LibSWBF2.Wrappers.Texture GetTexture(string name)
    {
    	if (container == null) return null;
    	return container.FindWrapper<LibSWBF2.Wrappers.Texture>(name);
    }

    public static Model GetModel(string name)
    {
	   	if (container == null) return null;
	   	return container.FindWrapper<Model>(name);
    }

    public static EntityClass GetEntityClass(string name)
    {
	   	if (container == null) return null;
    	return container.FindWrapper<EntityClass>(name);
    }

    public static AnimationBank GetAnimationBank(string name)
    {
    	if (container == null) return null;
    	return container.FindWrapper<AnimationBank>(name);
    }
}


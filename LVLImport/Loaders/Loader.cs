using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

using LibSWBF2.Logging;
using LibSWBF2.Wrappers;


public class Loader : ScriptableObject {

    protected static Container container = null;

    public static bool SetContainer(Container lvlContainer)
    {
    	container = lvlContainer;

        ModelLoader.ResetDB();
        AnimationLoader.ResetDB();
        TextureLoader.ResetDB();
        ClassLoader.ResetDB();

    	return true;
    }
}


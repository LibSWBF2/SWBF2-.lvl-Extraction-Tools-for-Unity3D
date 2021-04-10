#if !LVLIMPORT_NO_EDITOR
using UnityEditor;
#endif
using LibSWBF2.Wrappers;


public abstract class Loader {

    // Used by different loader classes
    protected static Container container = null;

#if !LVLIMPORT_NO_EDITOR
    protected bool SaveAssets = false;
    protected string SaveDirectory = null;
#endif

#if !LVLIMPORT_NO_EDITOR
    public bool SetSave(string prefix, string folderName)
    {
        if (folderName == null || folderName == "" || prefix == null || !prefix.StartsWith("Assets/"))
        {
            SaveAssets = false;
            return false;
        }

        if (!prefix.EndsWith("/"))
        {
            prefix = prefix + "/";
        }

        prefix = prefix + folderName;

        string[] pathLevels = prefix.Split('/');
        string pathAccum = "Assets";

        for (int i = 1; i < pathLevels.Length; i++)
        {
            string curPath = pathAccum + "/" + pathLevels[i];
            if (!AssetDatabase.IsValidFolder(curPath))
            {
                AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(pathAccum, pathLevels[i]));
            }
            pathAccum = curPath;
        }

        SaveDirectory = pathAccum;
        SaveAssets = true;

        return true;
    }
#endif

    public static void ResetAllLoaders()
    {
#if !LVLIMPORT_NO_EDITOR
        TextureLoader.Instance.SaveAssets = false;
        MaterialLoader.Instance.SaveAssets = false;
        ModelLoader.Instance.SaveAssets = false;
        AnimationLoader.Instance.SaveAssets = false;
        ClassLoader.Instance.SaveAssets = false;
        WorldLoader.Instance.SaveAssets = false;
#endif
        TextureLoader.Instance.ResetDB();
        MaterialLoader.Instance.ResetDB();
        ModelLoader.Instance.ResetDB();
        AnimationLoader.Instance.ResetDB();
        ClassLoader.Instance.ResetDB();
        WorldLoader.Instance.Reset();
        SoundLoader.ResetDB();
    }



    public static bool SetGlobalContainer(Container lvlContainer)
    {
    	container = lvlContainer;
    	return true;
    }

    public static bool FreeGlobalContainer()
    {
        if (container == null) return false;

        container.Delete();
        container = null;
        return true;
    }
}


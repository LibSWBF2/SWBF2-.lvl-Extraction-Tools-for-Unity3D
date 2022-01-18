# Unity SWBF2 .lvl Extraction Toolset

Extract assets contained within ```.lvl``` files to Unity.  This project began as a fork of [Ben1138's modtools importer](https://github.com/Ben1138/Unity-SWBF2-Import), but diverged and conflicted so significantly that it made sense to separate it.  It is one of the many projects under the [LibSWBF2](https://github.com/Ben1138/LibSWBF2) umbrella.

Tested on MacOS Catalina, Ubuntu 18.04, and Windows 10 with Unity 2021.2.7.

## Features
- Import multiple .lvl files and automatically resolve references between them
- Handles: <br> 
worlds with skydomes, mesh or heightmapped terrain, regions, and props, <br>
![alt text](./GitRepoAssets/worlds.gif "worlds") <br>
static and skinned models, animations, <br>
![alt text](./GitRepoAssets/rancor_grab.gif "rancor_grab") <br>
collision meshes and primitives. <br>
![alt text](./GitRepoAssets/coll.gif "coll") <br>
- Objects can also be imported free from association with a specific world
- Animation, Prefab, Model, Material, and Texture assets can be saved with each import


## Installation
1. This repo itself should only be cloned if you plan on _contributing_, it does not contain the required native plugin builds (see bottom).  If you wish to simply use the tools, **please download a ZIP release!**
2. Unzip and place the ```LVLImport``` folder into your ```Assets/``` directory.  If the ```SWBF2``` menu entry doesn't appear, check the error log.

## Usage
1. Click on ```SWBF2 --> Import .lvl```.
2. Add/Remove ```.lvl``` files, dependency order does not matter.
3. ```Import Worlds``` to import the worlds and their dependencies into your project/active scene.  ```Import Objects``` will import all the objects (ODFs) contained within the loaded lvl files and space them out under a root object in the project/active scene.  Check ```Save World``` if you wish to save terrain and skydome assets.  Check ```Save Objects``` if you wish to save/instantiate the converted GameObjects as prefabs.  The other options are self explanatory.
4. If you are including this project as part of a build, you must add **LVLIMPORT_NO_EDITOR** to _Project Settings > Player > Other Settings > Script Compilation > Scripting Define Symbols_ or your project will not compile.  This is because the importer makes use of the editor-only AssetDatabase API when saving assets.  Adding this symbol will exclude such uses from being included in the build and editor.

## Issues

The following are some pesky errors I haven't found consistent fixes for yet.  So far all of them have reported by Windows users only and occur immediately when starting an import with any number of LVL files.  If you have and/or solved these issues or unlisted ones, please drop a note in the libSWBF2 [Discord](https://discord.com/invite/nNUapcU) and be sure to include **your OS**, **Unity version**, a screenshot of the **error log/stacktrace**, [**scripting runtime**](https://docs.unity3d.com/Manual/dotnetProfileSupport.html), and what solution worked if you managed to fix it! 

1. **dllnotfound** exception - All this error means is that Unity had some problem finding or loading the _native dll_ (libLibSWBF2.dylib/so or LibSWBF2.dll).  Unfortuneately this error is quite common and the best solution is yet unknown.  _It is less common on Unity >= 2021_.

2. **array index out of bounds** exception - This is usually due to some incompatibility between Unity and the _managed dll_ (LibSWBF2.NET.dll).  This issue is rare and as of now there is no known fix.  The fixes listed for the previous issue might work...    

## TODO:
- More finely grained extraction, eg, per-model, ODF in each ```.lvl``` file
- Sounds
- Particle effects
- Lightmap UV generation
- Full ODF inheritance + properties for root classes like ```hover```, ```solider```, ```prop```, etc
- Special handling for soldier classes

## General Notes:
- This importer relies on native plugins (libLibSWBF2.so/.dylib on Linux/MacOS and LibSWBF2.dll on Windows), which are not included in this repo
- Munged script data is the focus of [this project](https://github.com/Ben1138/SWBF2UnityRuntime)
- LibSWBF2 contains the first working animation unmunger, and thus this importer can import animations correctly, apply them to GameObjects, and save them as clips.  However, SWBF2 animations identify bones simply by name, while Unity requires a full transform path, so some anims may fail to be properly attached.  Moreover, some animation dependencies are hardcoded into the root classes, eg ```soldier```.  These are not handled yet, so unless the imported object's ODF explicitly defines an ```AnimationName``` property, the required animations will not be imported.
- Many objects depend on common assets defined in ```ingame.lvl```


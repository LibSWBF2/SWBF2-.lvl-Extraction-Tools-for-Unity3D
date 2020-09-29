# Unity SWBF2-.lvl Importer

Import .lvl files into Unity3D.  This project began as a fork of Ben1138's modtools importer, but diverged and conflicted so significantly 
that it made sense to separate it.  It is one of the many projects under the LibSWBF2 umbrella.

Tested on MacOS Catalina, Ubuntu 18.04, and Windows 10.

## Features
- Import multiple .lvl files and automatically resolve references between them
- Import entities with models, collision, ODF wrappers, and materials
  - By default, materials are created with very low-effort, but the importer  
- Import worlds with prop instances
- Import terrain heightmaps, holes, and splatmaps with layer textures
- Import lights and global lighting data
- Does not save GameObjects/Meshes/Textures/Materials as proper prefabs/assets in the import stage.  If someone knows a way to do this without skyrocketing
load times, feel free to contribute, but for now, use the API/normal UI to save assets on a case-by-case basis.  


## How to install
1. Simply put the ```SWBF2Import``` folder into your ```Assets/``` directory
2. You should see a ```SWBF2``` Menu Entry on the top:
<br /><br />
![](Screenshots/menu.jpg)
<br /><br />

## How to use
1. Click on ```SWBF2 --> Import Menu ```.
2. Add/Delete .lvl files, ordering w.r.t. to dependencies does not matter
3. Inspect world and object lists, import individual worlds/objects or all at once
4. Importing all entities will instantiate each entity once in the scene (spaced out according to bounding boxes)
5. Importing all worlds will create terrains, lighting, and prop GameObjects for each world, regardless of potential overlap 
![](Screenshots/importer.jpg)

## TODO:
- Import terrain as mesh: SWBF2 terrains, which are meshes, can be represented with heightmaps in most cases, but holes create irregular terrain geometry that cannot.  The next release will probably include a custom terrain-as-a-mesh implementation, which will use the heightmap for physics, but will consume the splatmap and layer textures in a custom shader.

- LUA bytecode API: Importing the bytecode contained in .lvl script chunks and using it to drive a game/UI would be great.  Or at the very least, to get a list of all calls to ReadDataFile() and relevant mission setup calls.

- Fix light rotations

- Integrate skeletons

## Important Notes:
- The importer relies on native plugins (libSWBF2.so/.dylib on Linux/MacOS and LibSWBF2.dll on Windows), which incurs far more uncertainty than the 
modtools importer, which only used a single managed plugin.  Hard crashes will be more prevalent and problems more opaque. 

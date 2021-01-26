# Unity SWBF2 .lvl Extraction Toolset

Extract assets contained within ```.lvl``` files to Unity.  This project began as a fork of [Ben1138's modtools importer](https://github.com/Ben1138/Unity-SWBF2-Import), but diverged and conflicted so significantly that it made sense to separate it.  It is one of the many projects under the [LibSWBF2](https://github.com/Ben1138/LibSWBF2) umbrella.

Tested on MacOS Catalina, Ubuntu 18.04, and Windows 10.

## Features
- Import multiple .lvl files and automatically resolve references between them
- Import entities with models, collision, ODF wrappers, and best-effort materials
- Import worlds with prop instances, terrain, and skydomes
- Import lights and global lighting data
- Import animations and ~80% of skinned models correctly
- Import some ODF classes as Scriptable Objects


## Installation
1. Place the ```LVLImport``` folder into your ```Assets/``` directory.  If the ```SWBF2``` menu entry doesn't appear, check the error log.

## Usage
1. Click on ```SWBF2 --> Import .lvl```.
2. Add/Remove ```.lvl``` files, dependency order does not matter.
3. Import the contained worlds.  The objects belonging to each world will be placed under separate roots, so removing worlds is straightforward. 

## TODO:
- Import terrain as mesh: SWBF2 terrains, which are meshes, can be represented with heightmaps in most cases, but cuts/holes create irregular terrain geometry that Unity's terrain component cannot easily handle.  A special shader is needed.
- LUA bytecode API: Importing the bytecode contained in .lvl script chunks and using it to drive a game/UI would be great.  Or at the very least, to get a list of all calls to ReadDataFile() and relevant mission setup calls.
- Materials overhaul
- More finely grained extraction, eg, per-model, ODF in each ```.lvl``` file


## Important Notes:
- The importer relies on native plugins (libLibSWBF2.so/.dylib on Linux/MacOS and LibSWBF2.dll on Windows), so expect infrequent CTDs.
- Much of the work to be done is on the libSWBF2 side.  For now, the importer requires a native and managed plugin built from [this branch](https://github.com/WHSnyder/LibSWBF2/tree/anim_reader)

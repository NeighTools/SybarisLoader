# Sybaris Loader for [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)

> ⚠️ **THIS IS A LEGACY SOFTWARE FOR TESTING PURPOSES ONLY** ⚠️
>
> This software exists as an example of how to use [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop)
> to preload managed code during Unity startup.
>
> Use for testing purposes only for **developers**! 
> For a stable version, please use [BepInEx](https://github.com/bbepis/BepInEx) when a suitable loader exists.

This is a simple loader that loads and applies Sybaris-compatible patchers without editing the assembly files.  
The loader is works as a seamless replacement Sybaris 2.1.

This tool uses [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) as its backbone and is packaged with it.

## Installation

1. **Remove `opengl32.dll` and `Sybaris\Sybaris.Loader.dll`** if you have them
2. Extract the contents of the archive into the game's root directory
3. Configure the loader `Sybaris\SybariLoader.json` as you want.
4. Launch the game

## Installing patchers

This is Sybaris 2.1 compatible loader. Thus put all your patchers into `Sybaris` folder.

## More information

More information can be found on the project's [wiki](https://github.com/NeighTools/SybarisLoader/wiki).

## Building

You can build with Visual Studio 2015/2017 with .NET 3.5 installed. All dependencies are handled by NuGet.
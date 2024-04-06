This folder contains necessary files from game (`Celeste 1.4.0.0 [Everest: 1.4449.0-azure-ddba3]`) to compile this project. 

The files in `stripped` folder are stripped game libs that only have method declarations, for being able to compile without having the game installed and preventing uploading actual files to the repo. These are made with [BepInEx.AssemblyPublicizer](https://github.com/BepInEx/BepInEx.AssemblyPublicizer).

However you can still put the actual game libs in this folder for easier development, for example you can reference libs from this folder, so you can play with Celeste XNA while compiling with FNA.

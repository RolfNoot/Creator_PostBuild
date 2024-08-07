# Creator_PostBuild
Tool to adapt build files to implement a PSoC Creator project

## Description
The Creator_PostBuild tool enables smooth integration of PSoC Creator projects into Visual Studio Code.

This tool is designed to run before the build phase in VS Code. It is automatically initiated by the OTX-Maestro extension during the Clean/Configuration process and scans for build files needed for CMake (CMakeLists.txt) and Meson (meson.build) in the parent VS Code project folder. The tool reads the PSoCCreatorExportIDE(CortexM4/CortexM0p).xml file generated by PSoC Creator and inserts all the necessary compiler/linker options, as well as header, source, and library files, using comment tags into the build file used by the CMake/Meson/Ninja build system configured within Visual Studio Code.

The following tags are used for the insertion of the references into the build file:

<b>Creator_PostBuild version check (mandatory)</b>
```
# Creator_PostBuild_Minumum_Version: '2.10' - used for checking Creator_PostBuild minimum version, do not remove
```
<b>Creator_PostBuild version insertion:</b>
```
# Creator_PostBuild_Version_Line - automatic insert of Creator_PostBuild version. Do not edit the line below
creatorPostBuildVersion = ...
```
<b>Creator Generated DateTime insertion:</b>
```
# Creator_PostBuild_DateTime_Line - automatic insert of Creator Generated DateTime. Do not edit the line below
creatorGeneratedDateTime = ...
```
<b>Creator Project main directory insertion:</b>
```
Creator_PostBuild_Directory_Line - automatic insert of Creator Project main directory. Do not edit the line below
creatorDirectory = ...
```
<b>Device Part insertion:</b>
```
# Creator_PostBuild_devicePart_Line - automatic insert of device part by Creator_PostBuild. Do not edit the line below
devicePart = ...
```
<b>Linker File insertion:</b>
```
# Creator_PostBuild_linkerFile_Line - automatic insert of linker file by Creator_PostBuild. Do not edit the line below
linkerFile = ...
```
<b>SVD File insertion:</b>
```
# Creator_PostBuild_SVDfile_Line - automatic insert of SVD file by Creator_PostBuild. Do not edit the line below
SVDfile = ...
```
<b>Pre & PostBuild commands insertion:</b>
```
# Creator_PostBuild_prePostBuild_Lines - automatic insert of pre- and postbuild commands by Creator_PostBuild. Do not edit the two line below
preBuildCommands = ...
postBuildCommands = ...
```
<b>Assembler options insertion:</b>
```
# Creator_PostBuild_AssemblerOptions_Start - automatic insert of assembler options by Creator_PostBuild. Do not edit below this line
...
# Creator_PostBuild_AssemblerOptions_End - automatic insert of assembler options by Creator_PostBuild. Do not edit above this line
```
<b>Compiler options insertion:</b>
```
# Creator_PostBuild_CompilerOptions_Start - automatic insert of compiler options by Creator_PostBuild. Do not edit below this line
...
# Creator_PostBuild_CompilerOptions_End - automatic insert of compiler options by Creator_PostBuild. Do not edit above this line
```
<b>Linker options insertion:</b>
```
# Creator_PostBuild_LinkerOptions_Start - automatic insert of linker options by Creator_PostBuild. Do not edit below this line
..
# Creator_PostBuild_LinkerOptions_End - automatic insert of linker options by Creator_PostBuild. Do not edit above this line
```
<b>Include header folders insertion:</b>
```
# Creator_PostBuild__IncludeFolders_Start - automatic insert of include header folders by Creator_PostBuild. Do not edit below this line
...
# Creator_PostBuild__IncludeFolders_End - automatic insert of include header folders by Creator_PostBuild. Do not edit above this line
```
<b>Source files insertion (*.c / *.s)</b>
```
# Creator_PostBuild_SourceFiles_Start - automatic insert of source files by Creator_PostBuild. Do not edit below this line
...
# Creator_PostBuild_SourceFiles_End - automatic insert of source files by Creator_PostBuild. Do not edit above this line
```
<b>Library source files insertion (*.a)</b>
```
# Creator_PostBuild_LibSources_Start - automatic insert of library source files by Creator_PostBuild. Do not edit below this line
...
# Creator_PostBuild_LibSources_End - automatic insert of library source files by Creator_PostBuild. Do not edit above this line
```

To facilitate dual-core file parsing, an option has been added for inserting core-specific items. PSoC Creator generates two export files for each core in PSoC6: PSoCCreatorExportIDECortexM0p and PSoCCreatorExportIDECortexM4. By appending the specific core identifier (CortexM0p for the CM0+ core and CortexM4 for the CM4 core) to the ID option, dedicated parsing can be performed for each core's specific items.

For example:
# Creator_PostBuild_CompilerOptions_Start (ID:CortexM0p) - automatic insert of compiler options by Creator_PostBuild. Do not edit below this line

This will insert the compiler options specifically for the CM0+ core. This method can be applied to any of the designated tags to ensure proper configuration for each core.


# Change Log

### 1.2.0

- **Added PSoC6 Dual Core Support:** Added support for Dual Core PSoC6. 
- **Added CMake Support:** Added CMake functionality. In addition to parse the meson.build file, the tool now looks for CMakeLists.txt and will parse it if exists.

### 1.1.3

- **Fixed PSoC5 Default Library Addition:** Resolved an issue where the default library for PSoC5 was not being added correctly.
- **Absolute Linker Path Option:** Added a new option to specify the absolute path for the linker, providing greater flexibility and control.
- **Library Include Folder Duplicate Removal:** Implemented automatic removal of duplicate library include folders, ensuring a cleaner and more efficient build process.
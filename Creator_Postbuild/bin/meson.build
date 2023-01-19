project('meson_test', 'c',
	version : '0.1',
	default_options : ['warning_level=3']
)

[host_machine]
system     = 'none'
cpu_family = 'arm'
cpu        = 'cortex-m4'
endian     = 'little'

#==============================================================================#
# initialize some globals
srcs          = []
incdirs       = []
c_args        = []
link_args     = []
link_deps     = []

#==============================================================================#
# set directories

if build_machine.system() == 'windows'
	PROJECTNAME =  meson.current_source_dir().split('\\').get(-1)
	ONETHINX_PACK_LOC = run_command('cmd', '/C', 'echo %ONETHINX_PACK_LOC%', check: true).stdout().strip()
	ONETHINX_TOOLS_LOC = run_command('cmd', '/C', 'echo %ONETHINX_TOOLS_LOC%', check: true).stdout().strip()
else
	PROJECTNAME =  meson.current_source_dir().split('/').get(-1)
	ONETHINX_PACK_LOC = run_command('sh', '-c', 'echo $ONETHINX_PACK_LOC', check: true).stdout().strip()
	ONETHINX_TOOLS_LOC = run_command('sh', '-c', 'echo $ONETHINX_TOOLS_LOC', check: true).stdout().strip()
endif

CONFIG_DIR = ONETHINX_PACK_LOC + '/config'

MESON_SOURCE_LOC = meson.current_source_dir()
CREATOR_DIR = 'Onethinx_Creator.cydsn'

# Creator_PostBuildVersion_Line - automatic insert of Creator_PostBuild version. Do not edit the line below
creatorPostBuildVersion = '2.10'

# Creator_DateTime_Line - automatic insert of Creator_PostBuild version. Do not edit the line below
creatorGeneratedDateTime = '2023-01-19 11:43:02'

# Creator_PostBuild_devicePart_Line - automatic insert of device part by Creator_PostBuild. Do not edit the line below
devicePart = 'CY8C4248LQI-BL583'

# Creator_PostBuild_linkerFile_Line - automatic insert of linker file by Creator_PostBuild. Do not edit the line below
linkerFile = '.\Generated_Source\PSoC4\cm0gcc.ld'

# Creator_PostBuild_SVDfile_Line - automatic insert of SVD file by Creator_PostBuild. Do not edit the line below
SVDfile = 'PSoC_Creator.svd'

# Creator_PostBuild_prePostBuild_Lines - automatic insert of pre- and postbuild commands by Creator_PostBuild. Do not edit the two line below
preBuildCommands = ''
postBuildCommands = ''

#==============================================================================#
# import tools

objcopy  		= find_program('arm-none-eabi-objcopy').path()
objdump  		= find_program('arm-none-eabi-objdump').path()
size     		= find_program('arm-none-eabi-size').path()
gdb      		= find_program('arm-none-eabi-gdb').path()
cymcuelftool 	= find_program(ONETHINX_TOOLS_LOC + '/cymcuelftool-1.0/bin/cymcuelftool').path()
readelf  		= find_program('arm-none-eabi-readelf').path()
memcalc 		= find_program(CONFIG_DIR + '/scripts/memcalc').path()

#==============================================================================#
# setup compile & link arguments

assembler_args +=
[
# Creator_PostBuild_AssemblerOptions_Start - automatic insert of assembler options by Creator_PostBuild. Do not edit below this line
	'-mcpu=cortex-m0',
	'-mthumb',
	'-g',
	'-W',
# Creator_PostBuild_AssemblerOptions_End - automatic insert of assembler options by Creator_PostBuild. Do not edit above this line
]

compiler_args     += [
# Creator_PostBuild_CompilerOptions_Start - automatic insert of compiler options by Creator_PostBuild. Do not edit below this line
	'-mcpu=cortex-m0',
	'-mthumb',
	'-g',
	'-DDEBUG',
	'-Wall',
	'-ffunction-sections',
	'-ffat-lto-objects',
	'-O0',
# Creator_PostBuild_CompilerOptions_End - automatic insert of compiler options by Creator_PostBuild. Do not edit above this line
]

linker_args     = [
# Creator_PostBuild_LinkerOptions_Start - automatic insert of linker options by Creator_PostBuild. Do not edit below this line
	'-mcpu=cortex-m0',
	'-mthumb',
	'-LGenerated_Source\PSoC4',
	'-TGenerated_Source\PSoC4\cm0gcc.ld',
	'-specs=nano.specs',
	'-Wl,--gc-sections',
	'-g',
	'-ffunction-sections',
	'-O0',
	'-ffat-lto-objects',
# Creator_PostBuild_LinkerOptions_End - automatic insert of linker options by Creator_PostBuild. Do not edit above this line
]



incdirs       = [
# OTX_Extension_HeaderFiles_Start - automatic insert of source files by CoreStripper. Do not edit below this line
	'source/OnethinxCore',
	'source',
# OTX_Extension_HeaderFiles_End - automatic insert of source files by CoreStripper. Do not edit above this line
# Creator_PostBuild__IncludeFolders_Start - automatic insert of include header folders by Creator_PostBuild. Do not edit below this line
	'Debug/Generated_Source/PSoC4',
# Creator_PostBuild__IncludeFolders_End - automatic insert of include header folders by Creator_PostBuild. Do not edit above this line
]

sourcefiles       = [
# OTX_Extension_SourceFiles_Start - automatic insert of source files by CoreStripper. Do not edit below this line
	'Debug/Generated_Source/PSoC4/cyfitter_cfg.c',
	'Debug/Generated_Source/PSoC4/cymetadata.c',
	'Debug/Generated_Source/PSoC4/BLE.c',
	'Debug/Generated_Source/PSoC4/BLE_gatt.c',
	'Debug/Generated_Source/PSoC4/BLE_eventHandler.c',
	'Debug/Generated_Source/PSoC4/BLE_HAL_INT.c',
	'Debug/Generated_Source/PSoC4/BLE_HAL_PVT.c',
	'Debug/Generated_Source/PSoC4/BLE_custom.c',
	'Debug/Generated_Source/PSoC4/Conn_LED.c',
	'Debug/Generated_Source/PSoC4/Conn_LED_PM.c',
	'Debug/Generated_Source/PSoC4/UART.c',
	'Debug/Generated_Source/PSoC4/UART_SPI_UART.c',
	'Debug/Generated_Source/PSoC4/UART_SPI_UART_INT.c',
	'Debug/Generated_Source/PSoC4/UART_PM.c',
	'Debug/Generated_Source/PSoC4/UART_UART.c',
	'Debug/Generated_Source/PSoC4/UART_BOOT.c',
	'Debug/Generated_Source/PSoC4/UART_UART_BOOT.c',
	'Debug/Generated_Source/PSoC4/Adv_LED.c',
	'Debug/Generated_Source/PSoC4/Adv_LED_PM.c',
	'Debug/Generated_Source/PSoC4/BLE_bless_isr.c',
	'Debug/Generated_Source/PSoC4/UART_SCBCLK.c',
	'Debug/Generated_Source/PSoC4/UART_tx.c',
	'Debug/Generated_Source/PSoC4/UART_tx_PM.c',
	'Debug/Generated_Source/PSoC4/UART_SCB_IRQ.c',
	'Debug/Generated_Source/PSoC4/UART_rx.c',
	'Debug/Generated_Source/PSoC4/UART_rx_PM.c',
	'Debug/Generated_Source/PSoC4/Cm0Start.c',
	'Debug/Generated_Source/PSoC4/CyFlash.c',
	'Debug/Generated_Source/PSoC4/CyLib.c',
	'Debug/Generated_Source/PSoC4/cyPm.c',
	'Debug/Generated_Source/PSoC4/cyutils.c',
	'Debug/Generated_Source/PSoC4/CyDMA.c',
	'Debug/Generated_Source/PSoC4/CyLFClk.c',
	'Debug/Generated_Source/PSoC4/cy_em_eeprom.c',
	'Debug/Generated_Source/PSoC4/CyBootAsmGnu.s',
# OTX_Extension_SourceFiles_End - automatic insert of source files by CoreStripper. Do not edit above this line
# Creator_PostBuild_SourceFiles_Start - automatic insert of source files by Creator_PostBuild. Do not edit below this line
	'Debug/Generated_Source/PSoC4/cyfitter_cfg.c',
	'Debug/Generated_Source/PSoC4/cymetadata.c',
	'Debug/Generated_Source/PSoC4/BLE.c',
	'Debug/Generated_Source/PSoC4/BLE_gatt.c',
	'Debug/Generated_Source/PSoC4/BLE_eventHandler.c',
	'Debug/Generated_Source/PSoC4/BLE_HAL_INT.c',
	'Debug/Generated_Source/PSoC4/BLE_HAL_PVT.c',
	'Debug/Generated_Source/PSoC4/BLE_custom.c',
	'Debug/Generated_Source/PSoC4/Conn_LED.c',
	'Debug/Generated_Source/PSoC4/Conn_LED_PM.c',
	'Debug/Generated_Source/PSoC4/UART.c',
	'Debug/Generated_Source/PSoC4/UART_SPI_UART.c',
	'Debug/Generated_Source/PSoC4/UART_SPI_UART_INT.c',
	'Debug/Generated_Source/PSoC4/UART_PM.c',
	'Debug/Generated_Source/PSoC4/UART_UART.c',
	'Debug/Generated_Source/PSoC4/UART_BOOT.c',
	'Debug/Generated_Source/PSoC4/UART_UART_BOOT.c',
	'Debug/Generated_Source/PSoC4/Adv_LED.c',
	'Debug/Generated_Source/PSoC4/Adv_LED_PM.c',
	'Debug/Generated_Source/PSoC4/BLE_bless_isr.c',
	'Debug/Generated_Source/PSoC4/UART_SCBCLK.c',
	'Debug/Generated_Source/PSoC4/UART_tx.c',
	'Debug/Generated_Source/PSoC4/UART_tx_PM.c',
	'Debug/Generated_Source/PSoC4/UART_SCB_IRQ.c',
	'Debug/Generated_Source/PSoC4/UART_rx.c',
	'Debug/Generated_Source/PSoC4/UART_rx_PM.c',
	'Debug/Generated_Source/PSoC4/Cm0Start.c',
	'Debug/Generated_Source/PSoC4/CyFlash.c',
	'Debug/Generated_Source/PSoC4/CyLib.c',
	'Debug/Generated_Source/PSoC4/cyPm.c',
	'Debug/Generated_Source/PSoC4/cyutils.c',
	'Debug/Generated_Source/PSoC4/CyDMA.c',
	'Debug/Generated_Source/PSoC4/CyLFClk.c',
	'Debug/Generated_Source/PSoC4/cy_em_eeprom.c',
	'Debug/Generated_Source/PSoC4/CyBootAsmGnu.s',
# Creator_PostBuild_SourceFiles_End - automatic insert of source files by Creator_PostBuild. Do not edit above this line
]

#==============================================================================#
# include libraries

cc = meson.get_compiler('c')

# example: include library.a from directory
#link_deps += declare_dependency( dependencies : cc.find_library('library', dirs : ['directory/libdir']) )
link_deps += [
# Creator_PostBuild_LibSources_Start - automatic insert of include source files by Creator_PostBuild. Do not edit below this line
	declare_dependency( dependencies : cc.find_library('gccCyBLEStack_BLE_SOC_PERIPHERAL', dirs : [CREATOR_DIR + '/./Export/ARM_GCC_Generic/BLE_v3_66']) ),
# Creator_PostBuild_LibSources_End - automatic insert of include source files by Creator_PostBuild. Do not edit above this line
]

# example: include object.o from directory
#link_deps += declare_dependency( link_args : ['directory/libdir/object.o'] )

#==============================================================================#
# run prebuild script 

#prebuild = custom_target('prebuild', output : 'buildversion.h', command : [MESON_SOURCE_LOC+'/config/prebuild.bash']) #use prebuild.bash / prebuild.bat file
#link_deps += declare_dependency( sources : [prebuild])

#==============================================================================#
# build executable

main = executable(
            PROJECTNAME,          [sourcefiles],
            name_suffix         : 'elf',
            c_args              : [compiler_args],
            link_args           : [linker_args],
            dependencies        : link_deps,
            include_directories : [incdirs] )

#==============================================================================#
# run post build

signed = custom_target(
                        PROJECTNAME + '_signed.elf',
    output           : [PROJECTNAME + '_signed.elf'],
    build_by_default : true,
    command          : [cymcuelftool, '--sign',  PROJECTNAME + '.elf', '--output', PROJECTNAME + '_signed.elf', '--hex', PROJECTNAME + '.hex'],
    depends          : [main])
	
mainout = custom_target(
                        PROJECTNAME + '.memcalc.txt',
    output           :  [PROJECTNAME + '.memcalc.txt'],
    build_by_default : true,
	console			 : true,
    command          : [memcalc, readelf, PROJECTNAME + '.elf', '0x0e0000', '0x38000', '0x10000000', '0x08000000', PROJECTNAME + '.memcalc.txt'],  #Flash 0x0e0000   SRAM 0x38000   Flash start 0x10000000   SRAM start 0x08000000
   # command          : [memcalc, PROJECTNAME + '.readelf', '0x0e0000', '0x38000', '0x10000000', '0x08000000', PROJECTNAME + '.memcalc2.txt'],  #Flash 0x0e0000   SRAM 0x38000   Flash start 0x10000000   SRAM start 0x08000000
    depends          : [main])

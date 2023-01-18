using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Creator_PostBuild
{
    class Program
    {
        const string version = "2.10";

        class parseResult_c
        {
            public object output;
            public string message;
            public int error;
            public bool writeOutput;
            public parseResult_c(object output, string message, int error, bool writeOutput)
            {
                this.output = output;
                this.message = message;
                this.error = error;
                this.writeOutput = writeOutput;
            }
            public parseResult_c(string message, int error, bool writeOutput)
            {
                this.message = message;
                this.error = error;
                this.writeOutput = writeOutput;
            }
        }

        static void Main(string[] args)
        {
            if (args.Contains("-v") || args.Contains("-version"))
            {
                Console.WriteLine("version: " + version);
                return;
            }
            Console.WriteLine(
                "*****************************************************\r\n" +
                "*            Creator PostBuild tool " + version + "            *\r\n" +
                "*   The source code of this tool can be found at    *\r\n" +
                "*   https://github.com/RolfNoot/CreatorPostBuild01  *\r\n" +
                "*                (c) Rolf / Onethinx                *\r\n" +
                "*****************************************************\r\n"
            );
            if (args.Length == 0)
            {
                Console.WriteLine("Error, arguments needed! Run 'Creator_PostBuild.exe -h' for help");
                Environment.Exit(1);
            }
            if (args.Contains("-h") || args.Contains("-help"))
            {
                Console.WriteLine(
                    "Run this tool the PSoC Creator project directory\r\n" +
                    "OPTIONS:\r\n" +
                    "-v2       : mandatory, v2 for this version. Returns error if v2 not matches.\r\n" +
                    "-stripOTX : parses the cyfitter_cfg.c to cyfitter_otx_cfg.c\r\n" +
                    "            and removes the linker file from the linker options.\r\n" +
                    "-keepMain : keep the references to the main project sourcefiles."
                 );
                return;
            }
            if (!args.Contains("-v2"))
            {
                Console.WriteLine("Error, arguments not met!\r\nAdapt buildfile for use with this version and fix arguments.");
                Environment.Exit(1);
            }
            bool targetOTX = false;
            bool keepMain = false;
            if (args.Contains("-stripOTX")) targetOTX = true;
            if (args.Contains("-keepMain")) keepMain = true;

            string fileExportIDE = Environment.CurrentDirectory + @"\Export\PSoCCreatorExportIDE.xml";
            if (!File.Exists(fileExportIDE)) fileExportIDE = Environment.CurrentDirectory + @"\Export\PSoCCreatorExportIDECortexM4.xml";
            string fileCyFitter = Environment.CurrentDirectory + @"\Generated_Source\PSoC6\cyfitter_cfg.c";
            string fileCyFitterNew = Environment.CurrentDirectory + @"\Generated_Source\PSoC6\cyfitter_cfg_otx.c";
            // string filePlatformDebug = Environment.CurrentDirectory + @"\platform_debug.mk";
            string fileMesonBuild = Environment.CurrentDirectory + @"\..\meson.build";

            Console.WriteLine("\r\nTarget file: " + fileExportIDE);
            parseResult_c parseXMLfileResult = parseXMLfile(fileExportIDE, keepMain);
            xmlResult xmlOut = parseXMLfileResult.output as xmlResult;
            if (parseXMLfileResult.message != "") Console.WriteLine(parseXMLfileResult.message);
            if (parseXMLfileResult.writeOutput)
            {
                File.WriteAllLines("sourceFiles.txt", xmlOut.sourceFiles);
                File.WriteAllLines("headerIncludeDirs.txt", xmlOut.includeFolders);
                File.WriteAllLines("librarySources.txt", xmlOut.libraryFiles);
            }
            if (parseXMLfileResult.error != 0) Environment.Exit(parseXMLfileResult.error);

            if (targetOTX)      // Parse of CyFitter File into new file for OTX if needed
            {
                Console.WriteLine("\r\nTarget file: " + fileCyFitter);
                parseResult_c parseCyFitterResult = parseCyFitterCfg(fileCyFitter, xmlOut);
                if (parseCyFitterResult.message != "") Console.WriteLine(parseCyFitterResult.message);
                if (parseCyFitterResult.writeOutput) File.WriteAllLines(fileCyFitterNew, parseCyFitterResult.output as List<string>);
                if (parseCyFitterResult.error != 0) Environment.Exit(parseCyFitterResult.error);
            }

            Console.WriteLine("\r\nTarget file: " + fileMesonBuild);
            parseResult_c parseMesonBuildResult = parseMesonBuild(fileMesonBuild, xmlOut, targetOTX);
            if (parseMesonBuildResult.message != "") Console.WriteLine(parseMesonBuildResult.message);
            if (parseMesonBuildResult.writeOutput) File.WriteAllLines(fileMesonBuild, parseMesonBuildResult.output as List<string>);
            if (parseMesonBuildResult.error != 0) Environment.Exit(parseMesonBuildResult.error);

        }

        class xmlResult
        {
            public string devicePart = "";
            public string assemblerOptions = "";
            public string compilerOptions = "";
            public string linkerOptions = "";
            public string linkerFile = "";
            public string prebuild = "";
            public string postbuild = "";
            public string SVDfile = "";
            public List<string> sourceFiles = new List<string>();
            public List<string> includeFolders = new List<string>();
            public List<string> libraryFiles = new List<string>();
        }
        private static parseResult_c parseXMLfile(string filenname, bool keepMain)
        {
            if (!File.Exists(filenname)) return new parseResult_c("Error, can not find Export XML file!\r\nPlease set Project >> Export to IDE >> Makefile", 1, false);
            parseResult_c parseResult = new parseResult_c("Done!", 0, true);
            xmlResult xmlOut = new xmlResult();
            parseResult.output = xmlOut;
            PSoCCreatorIdeExport xmlSource;
            PSoCCreatorIdeExportToolchain[] xmlToolchains;
            PSoCCreatorIdeExportToolchain xmlGCCtool;
            PSoCCreatorIdeExportProject xmlProject;

            try // to get data from XML, error when fails
            {
                XmlSerializer ser = new XmlSerializer(typeof(PSoCCreatorIdeExport));

                using (XmlReader reader = XmlReader.Create(filenname))
                {
                    xmlSource = (PSoCCreatorIdeExport)ser.Deserialize(reader);
                }
                xmlToolchains = xmlSource.Toolchains;
                xmlGCCtool = xmlToolchains.ToList().Find(x => x.Name == "ARM GCC Generic");
                xmlProject = xmlSource.Project;
            }
            catch (Exception e) { return new parseResult_c("Error: " + e.Message, 1, false); }

            try // to get device part, warning when fails
            {
                xmlOut.devicePart = xmlSource.Device.Part;
            }
            catch (Exception e) { parseResult.message = String.Format("Warning, device part not found!\r\n{0}\r\n{1}", e.Message, parseResult.message); }

            try // to get assembler options, warning when fails
            {
                var xmlAssembler = xmlGCCtool.Tool.ToList().Find(x => x.Name == "assembler");
                xmlOut.assemblerOptions = xmlAssembler.Options;
            }
            catch (Exception e) { parseResult.message = String.Format("Warning, assembler options not found!\r\n{0}\r\n{1}", e.Message, parseResult.message); }

            try // to get compiler options, warning when fails
            {
                var xmlCompiler = xmlGCCtool.Tool.ToList().Find(x => x.Name == "compiler");
                xmlOut.compilerOptions = xmlCompiler.Options;
            }
            catch (Exception e) { parseResult.message = String.Format("Warning, compiler options not found!\r\n{0}\r\n{1}", e.Message, parseResult.message); }

            try // to get linker options, warning when fails
            {
                var xmlLinker = xmlGCCtool.Tool.ToList().Find(x => x.Name == "linker");
                xmlOut.linkerOptions = xmlLinker.Options;
            }
            catch (Exception e) { parseResult.message = String.Format("Warning, linker options not found!\r\n{0}\r\n{1}", e.Message, parseResult.message); }

            try // to get prebuild command, ignore when fails
            {
                var xmlPrebuild = xmlGCCtool.Tool.ToList().Find(x => x.Name == "prebuild");
                xmlOut.prebuild = xmlPrebuild.Command;
            }
            catch { }

            try // to get postbuild command, ignore when fails
            {
                var xmlPostbuild = xmlGCCtool.Tool.ToList().Find(x => x.Name == "postbuild");
                xmlOut.postbuild = xmlPostbuild.Command;
            }
            catch { }

            try // to get SVD file, ignore when fails
            {
                xmlOut.SVDfile = xmlProject.CMSIS_SVD_File;
            }
            catch { }

            try // to get linker file, warning when fails
            {
                var xmlLinkerFile = xmlProject.LinkerFiles.ToList().Find(x => x.Toolchain == "ARM GCC Generic");
                xmlOut.linkerFile = xmlLinkerFile.Value;
            }
            catch (Exception e) { parseResult.message = String.Format("Warning,linker file not found!\r\n{0}\r\n{1}", e.Message, parseResult.message); }

            try // to get source files, error when fails
            {
                var r = xmlProject.Folders;
                var xmlStrictFolders = r.Where(x => x.BuildType == "STRICT");
                List<PSoCCreatorIdeExportProjectFolderFilesFile> sourceFiles = new List<PSoCCreatorIdeExportProjectFolderFilesFile>();
                foreach (var xmlSources in xmlStrictFolders.Skip(keepMain ? 0 : 1))
                {
                    sourceFiles.AddRange(xmlSources.Files.File.Where(x => x.BuildType == "BUILD" && x.Toolchain == ""));
                    sourceFiles.AddRange(xmlSources.Files.File.Where(x => x.BuildType == "BUILD" && x.Toolchain == "ARM GCC Generic"));
                }
                foreach (var fileObj in sourceFiles)
                {
                    string file = (string)fileObj.Value;
                    string fileExtension = Path.GetExtension(file).ToLower();
                    switch (fileExtension)
                    {
                        case ".h":
                            string path = Path.GetDirectoryName(file);
                            if (!xmlOut.includeFolders.Contains(path)) xmlOut.includeFolders.Add(path);
                            break;
                        case ".c":
                        case ".s":
                            xmlOut.sourceFiles.Add(file);
                            break;
                        case ".a":
                            xmlOut.libraryFiles.Add(file);
                            break;
                    }
                }
            }
            catch (Exception e) { return new parseResult_c("Error: " + e.Message, 1, false); }

            return parseResult;
        }
        //private static parseResult_c parseXMLfile2(string cFileName, out string devicePart)
        //{
        //    devicePart = null;
        //    if (!File.Exists(cFileName)) return new parseResult_c("Error, can not find target file!", 1, false);
        //    string[] cFile = File.ReadAllLines(cFileName);
        //    parseResult_c parseResult = new parseResult_c("Done!", 0, true);
        //    int idx;
        //    foreach (string cline in cFile)
        //    {
        //        if ((idx = cline.IndexOf("Device Part=\"")) >= 0)
        //        {
        //            string line = cline.Substring(idx + 13);
        //            if ((idx = line.IndexOf('\"')) > 0) devicePart = line.Substring(0, idx);
        //        }

        //        else if ((idx = cline.IndexOf("<File BuildType=\"BUILD\"")) >= 0)
        //        {
        //            if ((cline.IndexOf("Toolchain=\"\"", idx) >= 0 || cline.IndexOf("Toolchain=\"ARM GCC Generic\"", idx) >= 0) && (idx = cline.IndexOf("\">")) >= 0)
        //            {
        //                string line = cline.Substring(idx + 2);
        //                if ((idx = line.IndexOf("</File>")) < 1) return new parseResult_c("PARSE ERROR in XML file.\r\n", 1, false);
        //                line = line.Substring(0, idx).Replace('\\', '/');
        //                if (line == "main.c") continue;
        //                if (line.ToLower().EndsWith(".c") || line.ToLower().EndsWith(".s")) parseResult.lines1.Add(line);
        //                else if (line.ToLower().EndsWith(".h"))
        //                {
        //                    if ((idx = line.LastIndexOf('/')) > 0)
        //                    {
        //                        line = line.Substring(0, idx + 1);
        //                        if (!parseResult.lines2.Contains(line))
        //                            parseResult.lines2.Add(line);
        //                    }
        //                }
        //                else if (line.ToLower().EndsWith(".a")) parseResult.lines3.Add(line);
        //            }
        //        }
        //    }
        //    return parseResult;
        //}

        private static parseResult_c parseCyFitterCfg(string cFileName, xmlResult xmlOut)
        {
            if (!File.Exists(cFileName)) return new parseResult_c("Error, can not find target file!", 1, false);
            string[] cFile = File.ReadAllLines(cFileName);
            List<string> parseOut = new List<string>();
            parseResult_c parseResult = new parseResult_c(parseOut, "Done!", 0, true);

            bool copying = true;
            int stopOverCnt = 0;
            int startOverCnt = 0;
            string stopInfo = "";
            string startInfo = "";

            for (int cnt = 0; cnt < cFile.Length; cnt++)
            {
                string cline = cFile[cnt];
                // if (cline.Contains("Onethinx")) return new parseResult_c("File already parsed, no work to be done :-)", 0, false);
                if ((stopOverCnt > 0) && (--stopOverCnt == 0))
                {
                    parseOut.Add(stopInfo);
                    copying = false;
                }
                if ((startOverCnt > 0) && (--startOverCnt == 0))
                {
                    parseOut.Add(startInfo);
                    copying = true;
                }
                if (cline == @"* This file is automatically generated by PSoC Creator.")
                {
                    parseOut.Add(@"* This file was automatically generated by PSoC Creator and is updated");
                    parseOut.Add(@"* for the Onethinx Core module by Onethinx Creator PostBuild " + version);
                    continue;
                }
                else if (cline == @"	status = Cy_SysClk_WcoEnable(500000u);")
                {
                    string newline = @"	status = CY_RET_SUCCESS;          // Removed enabling of WCO as it is already enabled by the Onethinx Core: status = Cy_SysClk_WcoEnable(500000u);";
                    parseOut.Add(newline);
                    Console.WriteLine("Changed line " + cnt.ToString() + " to: " + newline);
                    continue;
                }
                else if (cline.StartsWith(@"	Cy_SysLib_SetWaitStates")) { if (!copying) parseOut.Add(cline); }
                //if (cline == @"static void ClockInit(void)")
                //{
                //    Console.WriteLine("Found ClockInit() at line " + cnt + ", skipping code...");
                //    stopInfo = @"	/* Removed Non UDB config code by OnethinxTool */";
                //    stopOverCnt = 2;
                //}
                //else 
                //if (cline == @"	/* Configure peripheral clock dividers */")
                //{
                //    Console.WriteLine("Found peripheral clock setting at line " + cnt + ", adding code...");
                //    copying = true;
                //}
                else if (cline == @"void Cy_SystemInit(void)")
                {
                    Console.WriteLine("Found Cy_SystemInit() at line " + cnt + ", skipping code...");
                    //cline = @"void UDBInit(void)";
                    stopInfo =
                        @"	CyDelay(1500); /* Failsafe guard: wrong clocksettings may brick the Onethinx module. Remove this delay in the release version. */" + "\r\n\r\n" +
                        @"	/* Removed Onethinx Core conflicting code by Onethinx Creator PostBuild */" + "\r\n";

                    stopOverCnt = 2;
                }
                //else if (cline == @"   /* Clock */")
                //else if ((!copying) && (cline == @"	/* Perform Trigger Mux configuration */"))
                else if (cline == @"	/* PMIC Control */")
                {
                    startInfo = @"	/* Resume non-conflicting code by Onethinx Creator PostBuild */";
                    startOverCnt = 3;
                }
                if (copying) parseOut.Add(cline);
                else parseOut.Add(@"	//" + cline);
            }

            if (copying == false) return new parseResult_c("PARSE ERROR, try clean/rebuild.\r\n", 1, false);

            return parseResult;
        }

        //private static parseResult_c parsePlatformDebugMake(string cFileName)
        //{
        //    if (!File.Exists(cFileName)) return new parseResult_c("Error, can not find target file!\r\nPlease set Project >> Build Settings >> Target IDEs >> Makefile >> Generate", 1, false);
        //    string[] cFile = File.ReadAllLines(cFileName);
        //    parseResult_c parseResult = new parseResult_c("Done!", 0, true);
        //    for (int cnt = 0; cnt < cFile.Length; cnt++)
        //    {
        //        string cline = cFile[cnt];
        //        if (cline.StartsWith(@"ASFLAGS_CortexM4"))
        //        {
        //            int idx;
        //            while ((idx = cline.IndexOf(" -I")) > 0)
        //            {
        //                cline = cline.Substring(idx + 3);
        //                string dir = ((idx = cline.IndexOf(" -")) > 0)? cline.Substring(0, idx) : cline;
        //                parseResult.lines1.Add(dir);
        //            }
        //            break;
        //        }
        //    }
        //    return parseResult;
        //}

        private static parseResult_c parseMesonBuild(string cFileName, xmlResult xmlOut, bool stripOTX)
        {
            if (!File.Exists(cFileName)) return new parseResult_c("Not found. OK", 0, false);
            string[] cFile = File.ReadAllLines(cFileName);
            List<string> parseOut = new List<string>();
            parseResult_c parseResult = new parseResult_c(parseOut, "Done!", 0, true);
            string currentDirName = new DirectoryInfo(Environment.CurrentDirectory).Name + "/";
            bool copying = true; int linesStripped = 0;
            for (int cnt = 0; cnt < cFile.Length; cnt++)
            {
                string cline = cFile[cnt];
                if (cline.Contains("_SourceFiles_End") ||
                    cline.Contains("_IncludeHeaderDirs_End") ||
                    cline.Contains("_IncludeFolders_End") ||
                    cline.Contains("_LibSources_End") || 
                    cline.Contains("_AssemblerOptions_End") ||
                    cline.Contains("_CompilerOptions_End") ||
                    cline.Contains("_LinkerOptions_End")
                    ) copying = true;
                if (copying) parseOut.Add(cline);
                if (linesStripped > 0 && --linesStripped == 0) copying = true;
                if (cline.Contains("_SourceFiles_Start"))
                {
                    foreach (string line in xmlOut.sourceFiles)
                    {
                        string file = currentDirName + line.Replace('\\', '/');
                        parseOut.Add("\t'" + file + "',");
                    }
                    copying = false;
                }
                else if (cline.Contains("_IncludeHeaderDirs_Start") || cline.Contains("_IncludeFolders_Start"))
                {
                    foreach (string line in xmlOut.includeFolders)
                    {
                        string folder = currentDirName + ((line == ".") ? "" : line.Replace('\\', '/'));
                        parseOut.Add("\t'" + folder + "',");
                    }
                    copying = false;
                }
                else if (cline.Contains("_LibSources_Start"))
                {
                    foreach (string line in xmlOut.libraryFiles)
                    {
                        // parseOut.Add("\t'" + currentDirName + ((line == ".") ? "" : line) + "',");
                        if (!File.Exists(line))
                        {
                            return new parseResult_c("Error: Library " + line + " not found!\r\nPlease enable 'Project >> Export to IDE >> Makefile' setting", 1, false);
                        }
                        string lib = Path.GetFileNameWithoutExtension(line);
                        string libFolder = Path.GetDirectoryName(line).Replace('\\', '/');
                        parseOut.Add(String.Format("\tdeclare_dependency( dependencies : cc.find_library('{0}', dirs : [CREATOR_DIR + '/{1}']) ),", lib, libFolder));
                    }
                    copying = false;
                }
                else if (cline.Contains("_AssemblerOptions_Start"))
                {
                    foreach (string line in fixAndStripOptions(xmlOut.assemblerOptions))
                    {
                        parseOut.Add("\t'" + line + "',");
                    }
                    copying = false;
                }
                else if (cline.Contains("_CompilerOptions_Start"))
                {
                    foreach (string line in fixAndStripOptions(xmlOut.compilerOptions))
                    {
                        parseOut.Add("\t'" + line + "',");
                    }
                    copying = false;
                }
                else if (cline.Contains("_LinkerOptions_Start"))
                {
                    foreach (string line in fixAndStripOptions(xmlOut.linkerOptions))
                    {
                        if (!stripOTX || (!line.StartsWith("-L") && !line.StartsWith("-T")))      // if OTX found, do not add -L and -T options
                            parseOut.Add("\t'" + line + "',");
                    }
                    copying = false;
                }
                else if (cline.Contains("_devicePart_Line"))
                {
                    parseOut.Add("devicePart = '" + xmlOut.devicePart + "'");
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("_linkerFile"))
                {
                    parseOut.Add("linkerFile = '" + xmlOut.linkerFile + "'");
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("_SVDfile_Line"))
                {
                    parseOut.Add("SVDfile = '" + xmlOut.SVDfile + "'");
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("_prePostBuild_Lines"))
                {
                    parseOut.Add("preBuildCommands = '" + xmlOut.prebuild + "'");
                    parseOut.Add("postBuildCommands = '" + xmlOut.postbuild + "'");
                    copying = false; linesStripped = 2;
                }
                else if (cline.Contains("Creator_PostBuildVersion_Line")) 
                {
                    parseOut.Add("creatorPostBuildVersion = '" + version + "'");
                    copying = false; linesStripped = 1;
                }
            }

            if (copying == false) return new parseResult_c("PARSE ERROR, check meson.build insert syntax.\r\n", 1, false);

            return parseResult;
        }

        static List<string> fixAndStripOptions(string options)
        {
            List<string> optionsOut = new List<string>();
            //options = options.Replace(" -D ", " -D").Replace(" -L ", " -L").Replace(" -L ", " -L");"(\s-[A-Z])\s"
            options = Regex.Replace(options, @"(\s-[A-Z])\s", "$1");
            string[] optionsArray = Regex.Split(options, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            foreach (string line in optionsArray)
            {
                if (!line.StartsWith("-I") && !line.Contains("${")) // Only maintain options not referring files/locations
                    optionsOut.Add(line);
            }
            return optionsOut;
        }
    }
}

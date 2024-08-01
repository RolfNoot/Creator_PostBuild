using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.Globalization;

namespace Creator_PostBuild
{
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

    public class xmlResult
    {
        public string fromFile = "";
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

    public enum buildMethod
    {
        meson,
        cmake
    }

    public static class ListExtensions
    {
        public static void AddOption(this List<string> parseOut, string option, buildMethod method)
        {
            switch (method)
            {
                case buildMethod.meson:
                    parseOut.Add("\t'" + option + "',");
                    break;
                case buildMethod.cmake:
                    parseOut.Add("\t\"" + option + "\"");
                    break;
                default:
                    parseOut.Add(option);
                    break;
            }
        }
        public static void AddPath(this List<string> parseOut, string path, buildMethod method)
        {
            switch (method)
            {
                case buildMethod.meson:
                    parseOut.Add("\t'" + path + "',");
                    break;
                case buildMethod.cmake:
                    parseOut.Add("\t" + path + "");
                    break;
                default:
                    parseOut.Add(path);
                    break;
            }
        }
        public static void AddLibrary(this List<string> parseOut, string libFile, buildMethod method)
        {
            string libName = Path.GetFileNameWithoutExtension(libFile);
            string libFolder = Path.GetDirectoryName(libFile).Replace('\\', '/');
            libFile = libFile.Replace('\\', '/');
            switch (method)
            {
                case buildMethod.meson:
                    parseOut.Add(String.Format("\tdeclare_dependency( dependencies : cc.find_library('{0}', dirs : [CREATOR_DIR + '/{1}']) ),", libName, libFolder));
                    break;
                case buildMethod.cmake:
                    parseOut.Add($"add_library({libName} STATIC IMPORTED)");
                    parseOut.Add($"set_target_properties({libName} PROPERTIES IMPORTED_LOCATION \"${{CREATOR_DIR}}/{libFile}\")");
                    parseOut.Add($"list(APPEND libnames {libName})");
                    break;
                default:
                    parseOut.Add(libFile);
                    break;
            }
        }
        public static void AddVariable(this List<string> parseOut, string variableName, string variableValue, buildMethod method)
        {
            switch (method)
            {
                case buildMethod.meson:
                    parseOut.Add($"{variableName} = '{variableValue}'");
                    break;
                case buildMethod.cmake:
                    parseOut.Add($"set({variableName} \"{variableValue}\")");
                    break;
                default:
                    parseOut.Add($"{variableName} = '{variableValue}'");
                    break;
            }
        }
    }

    public class buildFile_t
    {
        public string fileName;
        public buildMethod method;
    }

    public static class XmlResultExtensions
    {
        public static void ProcessMatchingXmlResults(
            this IEnumerable<xmlResult> xmlResults,
            string cline, string defaultID,
            Func<xmlResult, IEnumerable<string>> lineSelector,
            Action<string> processLine)
        {
            // Extract ID from cline
            Match match = Regex.Match(cline, @"\(ID:(.*?)\)");
            string ID = match.Success ? match.Groups[1].Value : defaultID;

            foreach (var xmlResult in xmlResults.Where(x => x.fromFile == ID))
            {
                foreach (var line in lineSelector(xmlResult))
                {
                    processLine(line);
                }
            }
        }

        public static xmlResult GetMatchingXmlResults(
            this IEnumerable<xmlResult> xmlResults,
            string cline,
            string defaultID)
        {
            // Extract ID from cline
            Match match = Regex.Match(cline, @"\(ID:(.*?)\)");
            string ID = match.Success ? match.Groups[1].Value : defaultID;

            // Find and return the matching xmlResult, or null if no match is found
            return xmlResults.SingleOrDefault(x => x.fromFile == ID);
        }
    }

    class Program
    {
        const string version = "2.20";
        static string absLinkPath = "";

        static string NormalizePath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }

        static void PathFileAdd(List<string> refList, string path)
        {
            path = (path == ".") ? "" : path;
            path = path.Replace('\\', '/');
            if (!refList.Contains(path)) refList.Add(path);
        }

        public struct options
        {
            public static bool targetOTX = false;
            public static bool keepMain = false;
            public static bool dualCore = false;
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
                "*   https://github.com/RolfNoot/Creator_PostBuild   *\r\n" +
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
                    "-v2           : mandatory, v2 for this version. Returns error if v2 not matches.\r\n" +
                    "-targetOTX    : parses cyfitter_cfg.c to cyfitter_otx_cfg.c\r\n" +
                    "                removes the linker file from the linker options.\r\n" +
                    "                parses cy_ble_clk.h to remove SFLASH->RADIO_LDO_TRIMS.\r\n" +
                    "-keepMain     : keep the references to the main project sourcefiles.\r\n" +
                    "-absLinkPath  : provide an absolute linker path (absLinkPath=\"C:\\this path\").\r\n" +
                    "-absLinkPathV : provide a qouted absolute linker path for a meson variable (absLinkPathV=CREATOR_DIR)."
                 );
                return;
            }
            if (!args.Contains("-v2"))
            {
                Console.WriteLine("Error, arguments not met!\r\nAdapt buildfile for use with this version and fix arguments.");
                Environment.Exit(1);
            }


            foreach (var arg in args)
            {
                if (arg.Contains("-targetOTX")) options.targetOTX = true;
                if (arg.Contains("-keepMain")) options.keepMain = true;
                if (arg.Contains("-absLinkPath="))
                {
                    absLinkPath = arg.Replace("-absLinkPath=", "");
                }
                if (arg.Contains("-absLinkPathV="))
                {
                    absLinkPath = arg.Replace("-absLinkPathV=", "'+") + "+'/";
                }
            }

            List<buildFile_t> buildFiles = new List<buildFile_t>();
            string fileMesonBuild = NormalizePath(Environment.CurrentDirectory + @"\..\meson.build");
            string fileCmakeBuild = NormalizePath(Environment.CurrentDirectory + @"\..\CMakeLists.txt");
            if (File.Exists(fileMesonBuild)) buildFiles.Add(new buildFile_t() { fileName = fileMesonBuild, method = buildMethod.meson });
            if (File.Exists(fileCmakeBuild)) buildFiles.Add(new buildFile_t() { fileName = fileCmakeBuild, method = buildMethod.cmake });

            // string fileMesonBuildM0 = NormalizePath(Environment.CurrentDirectory + @"\..\meson_cm0.build");
            //  string fileMesonBuildM4 = NormalizePath(Environment.CurrentDirectory + @"\..\meson_cm4.build");
            //   options.dualCore = File.Exists(fileMesonBuildM0) && File.Exists(fileMesonBuildM4);

            string fileExportIDEbase = NormalizePath(Environment.CurrentDirectory + @"\Export");
            //if (!File.Exists(fileExportIDE)) fileExportIDE = NormalizePath(Environment.CurrentDirectory + @"\Export\PSoCCreatorExportIDECortexM4.xml");

            // Get all XML files in the directory
            string[] exportIDEfiles = Directory.GetFiles(fileExportIDEbase, "PSoCCreatorExportIDE*.xml", SearchOption.AllDirectories);


            string fileCyFitter = NormalizePath(Environment.CurrentDirectory + @"\Generated_Source\PSoC6\cyfitter_cfg.c");
            string fileCyFitterNew = NormalizePath(Environment.CurrentDirectory + @"\Generated_Source\PSoC6\cyfitter_cfg_otx.c");
            string fileCyBLEclk = NormalizePath(Environment.CurrentDirectory + @"\Generated_Source\PSoC6\pdl\middleware\ble\cy_ble_clk.c");


            parseFiles(exportIDEfiles, fileCyFitter, fileCyFitterNew, fileCyBLEclk, buildFiles);
        }

        private static void parseFiles(string[] exportIDEfiles, string fileCyFitter, string fileCyFitterNew, string fileCyBLEclk, List<buildFile_t> buildFiles)
        {
            List<xmlResult> xmlOuts = new List<xmlResult>();
            foreach (string fileExportIDE in exportIDEfiles)
            {
                Console.WriteLine("\r\nTarget file: " + fileExportIDE);
                parseResult_c parseXMLfileResult = parseXMLfile(fileExportIDE, options.keepMain);
                xmlResult xmlOut = parseXMLfileResult.output as xmlResult;
                xmlOut.fromFile = Path.GetFileNameWithoutExtension(fileExportIDE).Replace("PSoCCreatorExportIDE", "");

                if (parseXMLfileResult.message != "") Console.WriteLine(parseXMLfileResult.message);
                if (parseXMLfileResult.error != 0) Environment.Exit(parseXMLfileResult.error);
                MergeIncludeFoldersFromOptions(xmlOut, xmlOut.compilerOptions);                             // add folders specified by the compiler options (-I)
                if (!options.targetOTX || xmlOut.fromFile == "CortexM4")                                    // for targetOTX only add CortexM4
                    xmlOuts.Add(xmlOut);

                //{
                //    File.WriteAllLines("sourceFiles.txt", xmlOut.sourceFiles);
                //    File.WriteAllLines("headerIncludeDirs.txt", xmlOut.includeFolders);
                //    File.WriteAllLines("librarySources.txt", xmlOut.libraryFiles);
                //}

            }
            if (xmlOuts.Count < 1)
            {
                Console.WriteLine("Error, can not find Export XML file!\r\nPlease set Project >> Export to IDE >> Makefile");
                Environment.Exit(1);
            }

            if (options.targetOTX)      // Parse of CyFitter File into new file for OTX if needed
            {
                if (fileCyFitter != "")
                {
                    Console.WriteLine("\r\nTarget file: " + fileCyFitter);
                    parseResult_c parseCyFitterResult = parseCyFitterCfg(fileCyFitter, xmlOuts[0]);
                    if (parseCyFitterResult.message != "") Console.WriteLine(parseCyFitterResult.message);
                    if (parseCyFitterResult.writeOutput) File.WriteAllLines(fileCyFitterNew, parseCyFitterResult.output as List<string>);
                    if (parseCyFitterResult.error != 0) Environment.Exit(parseCyFitterResult.error);
                }

                if (fileCyBLEclk != "")
                {
                    Console.WriteLine("\r\nTarget file: " + fileCyBLEclk);
                    parseResult_c parseCyBLEclkResult = parseCyBLEclk(fileCyBLEclk);
                    if (parseCyBLEclkResult.message != "") Console.WriteLine(parseCyBLEclkResult.message);
                    if (parseCyBLEclkResult.writeOutput) File.WriteAllLines(fileCyBLEclk, parseCyBLEclkResult.output as List<string>);
                    if (parseCyBLEclkResult.error != 0) Environment.Exit(parseCyBLEclkResult.error);
                }
            }

            foreach (buildFile_t buildFile in buildFiles)
            {
                Console.WriteLine("\r\nTarget file: " + buildFile.fileName);
                parseResult_c parseMesonBuildResult = parseBuildFile(buildFile.fileName, xmlOuts, buildFile.method);
                if (parseMesonBuildResult.message != "") Console.WriteLine(parseMesonBuildResult.message);
                if (parseMesonBuildResult.writeOutput) File.WriteAllLines(buildFile.fileName, parseMesonBuildResult.output as List<string>);
                if (parseMesonBuildResult.error != 0) Environment.Exit(parseMesonBuildResult.error);
            }

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
                    if (xmlSources.Files.File != null)
                    {
                        sourceFiles.AddRange(xmlSources.Files.File.Where(x => x.BuildType == "BUILD" && x.Toolchain == ""));
                        sourceFiles.AddRange(xmlSources.Files.File.Where(x => x.BuildType == "BUILD" && x.Toolchain.Contains("ARM GCC Generic")));
                    }
                }
                foreach (var fileObj in sourceFiles)
                {
                    string file = (string)fileObj.Value;
                    string fileExtension = Path.GetExtension(file).ToLower();
                    switch (fileExtension)
                    {
                        case ".h":
                            string path = Path.GetDirectoryName(file);
                            //if (!xmlOut.includeFolders.Contains(path)) xmlOut.includeFolders.Add(path);
                            PathFileAdd(xmlOut.includeFolders, path);
                            break;
                        case ".c":
                        case ".s":
                            //xmlOut.sourceFiles.Add(file);
                            PathFileAdd(xmlOut.sourceFiles, file);
                            break;
                        case ".a":
                            //xmlOut.libraryFiles.Add(file);
                            PathFileAdd(xmlOut.libraryFiles, file);
                            break;
                    }
                }
            }
            catch (Exception e) { return new parseResult_c("Error: " + e.Message, 1, false); }

            return parseResult;
        }
        private static void MergeIncludeFoldersFromOptions(xmlResult xmlOut, string options)
        {
            foreach (string line in fixAndStripOptions(options, false))
            {
                if (line.StartsWith("-I"))
                {
                    //string folder = line.Substring(2).Replace('/', '\\').Trim(new char[] { ' ', '\\' });
                    //if (!xmlOut.includeFolders.Contains(folder))
                    //    xmlOut.includeFolders.Add(folder);
                    string path = line.Substring(2).Trim(new char[] { ' ', '/', '\\' });
                    PathFileAdd(xmlOut.includeFolders, path);
                }
            }
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

        private static parseResult_c parseCyBLEclk(string cFileName)
        {
            if (!File.Exists(cFileName)) return new parseResult_c("Target file not found.", 0, false);
            string[] cFile = File.ReadAllLines(cFileName);
            List<string> parseOut = new List<string>();
            parseResult_c parseResult = new parseResult_c(parseOut, "Done!", 0, true);

            for (int cnt = 0; cnt < cFile.Length; cnt++)
            {
                string cline = cFile[cnt];
                // if (cline.Contains("Onethinx")) return new parseResult_c("File already parsed, no work to be done :-)", 0, false);
                if (cline.Contains(@"SFLASH->RADIO_LDO_TRIMS != 0U") && !cline.Contains("OTX"))
                {
                    Console.WriteLine("Found SFLASH->RADIO_LDO_TRIMS at line " + cnt + ", changing code...");
                    cline = cline.Replace(@"SFLASH->RADIO_LDO_TRIMS != 0U", "false") + "        // Changed from (SFLASH->RADIO_LDO_TRIMS != 0U) to (false) by Creator PostBuild for OTX-18";
                }
                parseOut.Add(cline);
            }
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

        private static parseResult_c parseBuildFile(string cFileName, List<xmlResult> xmlOuts, buildMethod method)
        {
            if (!File.Exists(cFileName)) return new parseResult_c("Not found. OK", 0, false);
            string fileName = Path.GetFileName(cFileName);
            string[] cFile = File.ReadAllLines(cFileName);
            List<string> parseOut = new List<string>();
            parseResult_c parseResult = new parseResult_c(parseOut, "Done!", 0, true);
            string currentDirName = new DirectoryInfo(Environment.CurrentDirectory).Name + "/";
            bool copying = true; int linesStripped = 0;
            string minimumVersion = "";
            string defaultID = "";
            if (!xmlOuts.Any(x => x.fromFile == defaultID))
            {
                defaultID = "CortexM4";     // For PSoC6 set CortexM4 as default
                if (!xmlOuts.Any(x => x.fromFile == defaultID))
                    return new parseResult_c("Error: XML " + defaultID + " not found!\r\nPlease enable 'Project >> Export to IDE >> Makefile' setting", 1, false);
            }
            for (int cnt = 0; cnt < cFile.Length; cnt++)
            {
                string cline = cFile[cnt];
                if (cline.Contains("Creator_PostBuild_Minumum_Version")) minimumVersion = cline;
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
                if (cline.Contains("Creator_PostBuild_SourceFiles_Start"))
                {
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => x.sourceFiles, line =>
                    {
                        string file = currentDirName + line.Replace('\\', '/');
                        //parseOut.Add("\t'" + file + "',")
                        parseOut.AddPath(file, method);
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_IncludeFolders_Start"))
                {
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => x.includeFolders, line =>
                    {
                        string folder = currentDirName + ((line == ".") ? "" : line.Replace('\\', '/'));
                        parseOut.AddPath(folder, method);
                        //parseOut.Add("\t'" + line + "',");
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_LibSources_Start"))
                {
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => x.libraryFiles, line =>
                    {
                        // parseOut.Add("\t'" + currentDirName + ((line == ".") ? "" : line) + "',");
                        string libFile = NormalizePath(line);
                        if (!File.Exists(libFile))
                        {
                            libFile = Path.GetFileName(libFile);       // File may exist in main folder
                            if (!File.Exists(libFile)) return;
                               //  return new parseResult_c("Error: Library " + libFile + " not found!\r\nPlease enable 'Project >> Export to IDE >> Makefile' setting", 1, false);
                        }
                       // string lib = Path.GetFileNameWithoutExtension(libFile);
                       // string libFolder = Path.GetDirectoryName(libFile).Replace('\\', '/');
                        parseOut.AddLibrary(libFile, method);
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_AssemblerOptions_Start"))
                {
                    //foreach (string line in fixAndStripOptions(xmlOut.assemblerOptions, true))
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => fixAndStripOptions(x.assemblerOptions, true), line =>
                    {
                        parseOut.AddOption(line, method);
                        //parseOut.Add("\t'" + line + "',");
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_CompilerOptions_Start"))
                {
                    //foreach (string line in fixAndStripOptions(xmlOut.compilerOptions, true))
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => fixAndStripOptions(x.compilerOptions, true), line =>
                    {
                        //parseOut.Add("\t'" + line + "',");
                        parseOut.AddOption(line, method);
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_LinkerOptions_Start"))
                {
                    //foreach (string line in fixAndStripOptions(xmlOut.linkerOptions, true))
                    xmlOuts.ProcessMatchingXmlResults(cline, defaultID, x => fixAndStripOptions(x.linkerOptions, true), line =>
                    {
                        //if (!targetOTX || (!line.StartsWith("-L") && !line.StartsWith("-T")))      // if OTX found, do not add -L and -T options
                        //    parseOut.Add("\t'" + line + "',");
                        if (line.StartsWith("-L") || line.StartsWith("-T"))     // check for folder references
                        {
                            if (!options.targetOTX) //parseOut.Add("\t'" + line.Insert(2, absLinkPath).Replace('\\', '/') + "',");       // Reformat and add when not using OTX target
                                parseOut.AddOption(line.Insert(2, absLinkPath).Replace('\\', '/'), method);       // Reformat and add when not using OTX target
                        }
                        else parseOut.AddOption(line, method);
                    });
                    copying = false;
                }
                else if (cline.Contains("Creator_PostBuild_devicePart_Line"))
                {
                    //parseOut.Add("devicePart = '" + defaultXmlOut.devicePart + "'");
                    var xmlOut = xmlOuts.GetMatchingXmlResults(cline, defaultID);
                    parseOut.AddVariable("devicePart", xmlOut == null ? "" : xmlOut.devicePart, method);
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("Creator_PostBuild_linkerFile"))
                {
                    var xmlOut = xmlOuts.GetMatchingXmlResults(cline, defaultID);
                    parseOut.AddVariable("linkerFile", xmlOut == null ? "" : xmlOut.linkerFile.Replace('\\', '/'), method);
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("Creator_PostBuild_SVDfile_Line"))
                {
                    var xmlOut = xmlOuts.GetMatchingXmlResults(cline, defaultID);
                    parseOut.AddVariable("SVDfile", xmlOut == null ? "" : xmlOut.SVDfile, method);
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("Creator_PostBuild_prePostBuild_Lines"))
                {
                    var xmlOut = xmlOuts.GetMatchingXmlResults(cline, defaultID);
                    parseOut.AddVariable("preBuildCommands", xmlOut == null ? "" : xmlOut.prebuild, method);
                    parseOut.AddVariable("postBuildCommands", xmlOut == null ? "" : xmlOut.postbuild, method);
                    copying = false; linesStripped = 2;
                }
                else if (cline.Contains("Creator_PostBuild_Version_Line"))
                {
                    parseOut.AddVariable("creatorPostBuildVersion", version, method);
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("Creator_PostBuild_DateTime_Line"))
                {
                    parseOut.AddVariable("creatorGeneratedDateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), method);
                    copying = false; linesStripped = 1;
                }
                else if (cline.Contains("Creator_PostBuild_Directory_Line"))
                {
                    parseOut.AddVariable("creatorDirectory", new DirectoryInfo(Environment.CurrentDirectory).Name, method);
                    copying = false; linesStripped = 1;
                }
            }

            if (copying == false) return new parseResult_c($"PARSE ERROR, check {fileName} insert syntax.\r\n", 1, false);
            if (minimumVersion == "") return new parseResult_c($"Error: Creator_PostBuild_Minumum_Version not found!\r\nUse updated {fileName} template.\r\n", 1, false);
            var result = Regex.Matches(minimumVersion, "['\"](\\d*\\.\\d*)['\"]");
            try
            {
                if (float.Parse(result[0].Groups[1].Value) > float.Parse(version))
                    return new parseResult_c($"Error: Creator_PostBuild Minumum Version not met. Update {fileName}.\r\n", 1, false);
            }
            catch
            {
                return new parseResult_c($"Error: Creator_PostBuild_Minumum_Version insert error, check {fileName} syntax.\r\n", 1, false);
            }

            return parseResult;
        }

        static List<string> fixAndStripOptions(string options, bool stripIncludes)
        {
            List<string> optionsOut = new List<string>();
            options = Regex.Replace(options, @"(\s-[A-Z])\s", "$1");
            string[] optionsArray = Regex.Split(options, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");     // Split options by whitespace, unless quoted
            foreach (string line in optionsArray)
            {
                if ((!line.StartsWith("-I") || !stripIncludes) && !line.Contains("${")) // Only maintain options not including PSoC Creator referred files/locations
                    optionsOut.Add(line);
            }
            return optionsOut;
        }
    }
}

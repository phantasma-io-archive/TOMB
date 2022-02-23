using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Tomb.CodeGen;
using System;
using System.IO;
using System.Text;

namespace Phantasma.Tomb
{
    class Program
    {
        static void ExportLibraryInfo()
        {
            var sb = new StringBuilder();
            foreach (var libraryName in Contract.AvailableLibraries)
            {
                var library = Contract.LoadLibrary(libraryName, null, libraryName == Module.FormatLibraryName ? ModuleKind.Description : ModuleKind.Contract);

                sb.AppendLine("### "+libraryName);
                sb.AppendLine("| Method | Return type | Description|");
                sb.AppendLine("| ------------- | ------------- |------------- |");
                foreach (var method in library.methods.Values)
                {
                    var parameters = new StringBuilder();
                    int index = -1;
                    foreach (var entry in method.Parameters)
                    {
                        index++;

                        if (parameters.Length > 0)
                        {
                            parameters.Append(", ");
                        }

                        parameters.Append(entry.Name + ":" + entry.Type + "");
                    }

                    sb.AppendLine($"| {libraryName}.{method.Name}({parameters}) | {method.ReturnType} | TODO|");
                }
                sb.AppendLine("");
            }

            File.WriteAllText("libs.txt", sb.ToString());
        }

        static void ExportModule(Module module)
        {
            if (module.asm != null)
            {
                File.WriteAllText(module.Name + ".asm", module.asm);
            }

            if (module.script != null)
            {
                var extension = module.Kind == ModuleKind.Script ? ".tx" : ".pvm";
                File.WriteAllBytes(module.Name + extension, module.script);

                var hex = Base16.Encode(module.script);
                File.WriteAllText(module.Name + extension + ".hex", hex);
            }

            if (module.debugInfo != null)
            {
                File.WriteAllText(module.Name + ".debug", module.debugInfo.ToJSON());
            }

            if (module.abi != null)
            {
                var abiBytes = module.abi.ToByteArray();
                File.WriteAllBytes(module.Name + ".abi", abiBytes);

                var hex = Base16.Encode(abiBytes);
                File.WriteAllText(module.Name + ".abi.hex", hex);
            }
        }

        static public void ShowWarning(string warning)
        {
            var temp = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warning);
            Console.ForegroundColor = temp;
        }

        static void Main(string[] args)
        {
            string sourceFilePath = null;

            int targetProtocolVersion = DomainSettings.LatestKnownProtocol;

            for (int i=0; i<args.Length; i++)
            {
                if (i == args.Length - 1)
                {
                    sourceFilePath = args[i];
                    break;
                }

                var tmp = args[i].Split(':', 2);

                var tag = tmp[0];
                string value = tmp.Length == 2 ? tmp[1] : null;

                switch (tag)
                {
                    case "protocol":
                        int version = -1;
                        if (int.TryParse(value, out version) && version > 0)
                        {
                            targetProtocolVersion = version;
                        }
                        else
                        {
                            ShowWarning("Invalid protocol version: " + value);
                        }
                        break; 

                    default:
                        ShowWarning("Unknown option: " + tag);
                        break;
                }
            }

#if DEBUG
            ExportLibraryInfo();
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                sourceFilePath = @"..\..\..\builtins.tomb";
            }
#else
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                sourceFilePath = @"my_contract.tomb";
            }
#endif

            if (!File.Exists(sourceFilePath))
            {
                Console.WriteLine("File not found:" + sourceFilePath);
                return;
            }

            var sourceCode = File.ReadAllText(sourceFilePath);

            Console.WriteLine("Compiling " + sourceFilePath);
            Console.WriteLine("Target protocol version: " + targetProtocolVersion);

            var compiler = new Compiler(targetProtocolVersion);
            var modules = compiler.Process(sourceCode);

            foreach (var module in modules)
            {
                ExportModule(module);

                foreach (var subModule in module.SubModules)
                {
                    ExportModule(subModule);
                }
            }

            Console.WriteLine("Success!");
        }
    }
}

using Phantasma.Numerics;
using System;
using System.IO;
using System.Text;

namespace Phantasma.Tomb.Compiler
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

        static void Main(string[] args)
        {
            ExportLibraryInfo();

            var sourceFilePath = args.Length > 0 ? args[0] : "katacomb.txt";

            if (!File.Exists(sourceFilePath))
            {
                Console.WriteLine("File not found:" + sourceFilePath);
                return;
            }

            var sourceCode = File.ReadAllText(sourceFilePath);

            Console.WriteLine("Compiling " + sourceFilePath);
            var compiler = new Compiler();
            var modules = compiler.Process(sourceCode);

            foreach (var module in modules)
            {
                /*if (module.Hidden)
                {
                    continue;
                }*/

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

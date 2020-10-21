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
                var library = Contract.LoadLibrary(libraryName, null, libraryName == "Output");

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

                        if (library.IsGeneric && index == 0)
                        {
                            continue;
                        }

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
                if (module.Hidden)
                {
                    continue;
                }

                if (module.asm != null)
                {
                    File.WriteAllText(module.Name + ".asm", module.asm);
                }

                if (module.script != null)
                {
                    File.WriteAllBytes(module.Name + ".script", module.script);
                }

                if (module.debugInfo != null)
                {
                    File.WriteAllText(module.Name + ".debug", module.debugInfo.ToJSON());
                }

                if (module.abi != null)
                {
                    File.WriteAllBytes(module.Name + ".abi", module.abi.ToByteArray());
                }

            }

            Console.WriteLine("Sucess!");
        }
    }
}

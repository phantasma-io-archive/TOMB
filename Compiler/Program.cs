using Phantasma.Domain;
using Phantasma.VM;
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
                var library = Contract.LoadLibrary(libraryName, null);
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

                        parameters.Append(entry.Name + ":" + entry.Kind + "");
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

            var sourceFile = args.Length > 0 ? args[0] : "startup.txt"; // "katacomb.txt";

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine("File not found:" + sourceFile);
                return;
            }

            var sourceCode = File.ReadAllText(sourceFile);

            Console.WriteLine("Parsing " + sourceFile);
            var parser = new Parser();
            var modules = parser.Parse(sourceCode);
            Console.WriteLine($"Found {modules.Length} compilation modules");

            foreach (var module in modules)
            {
                Console.WriteLine("Compiling " + module.Name);
                string asm;
                ContractInterface abi;
                byte[] script;
                DebugInfo debugInfo;
                module.Compile(sourceFile, out script, out asm, out abi, out debugInfo);


                File.WriteAllText(module.Name + ".asm", asm);
                File.WriteAllBytes(module.Name + ".script", script);

                if (debugInfo != null)
                {
                    File.WriteAllText(module.Name + ".debug", debugInfo.ToJSON());
                }

                if (abi != null)
                {
                    File.WriteAllBytes(module.Name + ".abi", abi.ToByteArray());
                }

            }

            Console.WriteLine("Sucess!");
        }
    }
}

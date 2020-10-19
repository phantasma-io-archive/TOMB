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

            var sourceFile = args.Length > 0 ? args[0] : "katacomb.txt";

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine("File not found:" + sourceFile);
                return;
            }

            var sourceCode = File.ReadAllText(sourceFile);

            Console.WriteLine("Parsing " + sourceFile);
            var parser = new Parser();
            var module = parser.Parse(sourceCode);

            Console.WriteLine("Compiling " + sourceFile);
            string asm;
            ContractInterface abi;
            byte[] script;
            DebugInfo debugInfo;
            module.Compile(sourceFile, out script, out asm, out abi, out debugInfo);

            var contractName = Path.GetFileNameWithoutExtension(sourceFile);

            File.WriteAllText(contractName + ".asm", asm);
            File.WriteAllBytes(contractName + ".script", script);

            if (debugInfo != null)
            {
                File.WriteAllText(contractName + ".debug", debugInfo.ToJSON());
            }

            if (abi != null)
            {
                File.WriteAllBytes(contractName + ".abi", abi.ToByteArray());
            }

            Console.WriteLine("Sucess!");
        }
    }
}

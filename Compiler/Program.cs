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
                sb.AppendLine("| Method | Description|");
                sb.AppendLine("| ------------- | ------------- |");
                foreach (var method in library.methods.Values)
                {
                    var parameters = new StringBuilder();
                    foreach (var entry in method.Parameters)
                    {
                        if (parameters.Length > 0)
                        {
                            parameters.Append(", ");
                        }
                        parameters.Append(entry.Name + ":" + entry.Kind + "");
                    }

                    sb.AppendLine($"| {libraryName}.{method.Name}({parameters}) | TODO|");
                }
                sb.AppendLine("");
            }

            File.WriteAllText("libs.txt", sb.ToString());
        }

        static void Main(string[] args)
        {
            ExportLibraryInfo();

            var sourceFile = "katacomb.txt";
            Console.WriteLine("Opening " + sourceFile);
            var sourceCode = File.ReadAllText(sourceFile);

            Console.WriteLine("Parsing " + sourceFile);
            var parser = new Parser();
            var contract = parser.Parse(sourceCode);

            Console.WriteLine("Compiling " + sourceFile);
            string asm;
            ContractInterface abi;
            byte[] script;
            DebugInfo debugInfo;
            contract.Compile(sourceFile, out script, out asm, out abi, out debugInfo);

            var contractName = Path.GetFileNameWithoutExtension(sourceFile);

            File.WriteAllText(contractName + ".asm", asm);
            File.WriteAllBytes(contractName + ".abi", abi.ToByteArray());
            File.WriteAllBytes(contractName + ".script", script);
            File.WriteAllText(contractName + ".debug", debugInfo.ToJSON());

            Console.WriteLine("Done, press any key");
            Console.ReadKey();
        }
    }
}

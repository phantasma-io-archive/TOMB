using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Tomb.Compiler
{
    class Program
    {
        static void Main(string[] args)
        {
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

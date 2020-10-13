using Phantasma.Domain;
using System;
using System.IO;

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
            contract.Compile(out asm, out abi);


            var contractName = Path.GetFileNameWithoutExtension(sourceFile);

            File.WriteAllText(contractName + ".asm", asm);

            File.WriteAllBytes(contractName + ".abi", abi.ToByteArray());
            


            Console.WriteLine("Done, press any key");
            Console.ReadKey();
        }
    }
}

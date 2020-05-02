using System;
using System.IO;

namespace Phantasma.Tomb.Compiler
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourceFile = "code2.txt";
            Console.WriteLine("Opening " + sourceFile);
            var sourceCode = File.ReadAllText(sourceFile);

            Console.WriteLine("Parsing " + sourceFile);
            var parser = new Parser();
            var contract = parser.Parse(sourceCode);

            Console.WriteLine("Compiling " + sourceFile);
            var asm = contract.Compile();
            File.WriteAllText("asm.txt", asm);

            Console.WriteLine("Done, press any key");
            Console.ReadKey();
        }
    }
}

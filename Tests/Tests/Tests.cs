using NUnit.Framework;
using Phantasma.CodeGen.Assembler;
using Phantasma.Tomb.Compiler;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestCounter()
        {
            var sourceCode =
            "contract test{" +
            "global counter: number;" +
            "constructor()	{" +
            "counter:= 0;}" +
            "method increment(){" +
            "if (counter < 0){" +
            "throw 'invalid state';}" +
            "counter += 1;" +
            "}}";

            var parser = new Parser();
            var contract = parser.Parse(sourceCode);

            var asm = contract.Compile();

            var lines = asm.Split('\n');

            var script = AssemblerUtils.BuildScript(lines);

            Assert.Pass();
        }
    }
}
using System;

namespace Phantasma.Tomb.Compiler
{
    public class CompilerException : Exception
    {
        public CompilerException(string msg) : base($"line {Parser.Instance.CurrentLine}: {msg}")
        {

        }

        public CompilerException(Node node, string msg) : base($"line {node.LineNumber}: {msg}")
        {

        }
    }
}

using Phantasma.Tomb.AST;
using System.Text;

namespace Phantasma.Tomb.CodeGen
{
    public class CodeGenerator
    {
        private StringBuilder _sb = new StringBuilder();

        public static Scope currentScope = null;
        public static int currentSourceLine = 0;

        public int LineCount;

        public static string Tabs(int n)
        {
            return new string('\t', n);
        }

        public void AppendLine(Node node, string line = "")
        {
            if (node.LineNumber <= 0)
            {
                throw new CompilerException("line number failed for " + node.GetType().Name);
            }

            while (currentSourceLine <= node.LineNumber)
            {
                if (currentSourceLine > 0)
                {
                    var lineContent = Compiler.Instance.GetLine(currentSourceLine);
                    _sb.AppendLine($"// Line {currentSourceLine}:" + lineContent);
                    LineCount++;
                }
                currentSourceLine++;
            }

            line = Tabs(currentScope.Level) + line;
            _sb.AppendLine(line);
            LineCount++;
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}

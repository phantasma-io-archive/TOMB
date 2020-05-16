using System.Text;

namespace Phantasma.Tomb.Compiler
{
    public class CodeGenerator
    {
        private StringBuilder _sb = new StringBuilder();

        public static Scope currentScope = null;
        public static int currentLine = 0;

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

            while (currentLine <= node.LineNumber)
            {
                if (currentLine > 0)
                {
                    var lineContent = Parser.Instance.GetLine(currentLine);
                    _sb.Append($"// Line {currentLine}:" + lineContent);
                }
                currentLine++;
            }

            line = Tabs(currentScope.Level) + line;
            _sb.AppendLine(line);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }
}

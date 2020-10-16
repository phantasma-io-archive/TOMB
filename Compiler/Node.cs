using System;

namespace Phantasma.Tomb.Compiler
{
    public abstract class Node
    {
        public int LineNumber;
        public int Column;
        public string NodeID;

        public Node()
        {
            if (Parser.Instance != null)
            {
                this.LineNumber = Parser.Instance.CurrentLine;
                this.Column = Parser.Instance.CurrentColumn;
                this.NodeID = this.GetType().Name.ToLower() + Parser.Instance.AllocateLabel();
            }
            else
            {
                this.LineNumber = -1;
                this.Column = -1;
                this.NodeID = this.GetType().Name.ToLower();
            }
        }

        public abstract bool IsNodeUsed(Node node);

        public abstract void Visit(Action<Node> callback);
    }
}

namespace Phantasma.Tomb.Compiler
{
    public abstract class Node
    {
        public int LineNumber;
        public int Column;
        public string NodeID;

        public Node()
        {
            this.LineNumber = Parser.Instance.CurrentLine;
            this.Column = Parser.Instance.CurrentColumn;
            this.NodeID = this.GetType().Name.ToLower() + Parser.Instance.AllocateLabel();
        }

        public abstract bool IsNodeUsed(Node node);
    }
}

using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.VM;
using System;
using System.Linq;

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

    public abstract class Module: Node
    {
        public readonly string Name;
        public Scope Scope { get; }

        public Module(string name)
        {
            this.Name = name;
            this.Scope = new Scope(this);
        }

        public abstract ContractInterface GenerateCode(CodeGenerator output);


        protected virtual void ProcessABI(ContractInterface abi, DebugInfo debugInfo)
        {
            // do nothing
        }

        public void Compile(string fileName, out byte[] script, out string asm, out ContractInterface abi, out DebugInfo debugInfo)
        {
            var sb = new CodeGenerator();
            abi = this.GenerateCode(sb);

            Parser.Instance.VerifyRegisters();

            asm = sb.ToString();

            var lines = asm.Split('\n');
            script = AssemblerUtils.BuildScript(lines, fileName, out debugInfo);

            lines = AssemblerUtils.CommentOffsets(lines, debugInfo).ToArray();

            ProcessABI(abi, debugInfo);

            asm = string.Join('\n', lines);
        }
    }
}

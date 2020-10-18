using Phantasma.Blockchain.Contracts;
using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Phantasma.Tomb.Compiler
{
    public class Script: Module
    {
        public StatementBlock body;

        public Script(string name) : base(name)
        {

        }

        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            return body.IsNodeUsed(node);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            body.Visit(callback);
        }


        public override ContractInterface GenerateCode(CodeGenerator output)
        {
            this.Scope.Enter(output);
            this.body.GenerateCode(output);

            output.AppendLine(this, "RET");
            this.Scope.Leave(output);

            return new ContractInterface(Enumerable.Empty<ContractMethod>());
        }

    }
}

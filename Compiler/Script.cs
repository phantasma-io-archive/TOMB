using Phantasma.Blockchain.Contracts;
using Phantasma.CodeGen.Assembler;
using Phantasma.CodeGen.Core.Nodes;
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
        public StatementBlock main;

        public MethodParameter[] Parameters { get; internal set; }

        public Script(string name) : base(name)
        {

        }

        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var lib in Libraries.Values)
            {
                if (lib.IsNodeUsed(node))
                {
                    return true;
                }
            }


            return main.IsNodeUsed(node);
        }

        public override void Visit(Action<Node> callback)
        {
            foreach (var lib in Libraries.Values)
            {
                lib.Visit(callback);
            }

            callback(this);
            main.Visit(callback);
        }


        public override ContractInterface GenerateCode(CodeGenerator output)
        {
            this.Scope.Enter(output);

            var paramRegs = new Dictionary<MethodParameter, Register>();

            foreach (var parameter in this.Parameters)
            {
                var reg = Parser.Instance.AllocRegister(output, this, parameter.Name);
                output.AppendLine(this, $"POP {reg}");
                paramRegs[parameter] = reg;
            }

            this.main.GenerateCode(output);

            foreach (var reg in paramRegs.Values)
            {
                var temp = reg;
                Parser.Instance.DeallocRegister(ref temp);
            }

            output.AppendLine(this, "RET");

            this.Scope.Leave(output);

            return new ContractInterface(Enumerable.Empty<ContractMethod>());
        }

    }
}

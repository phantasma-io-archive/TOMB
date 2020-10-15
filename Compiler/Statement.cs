using System.Buffers;
using System.Collections.Generic;

namespace Phantasma.Tomb.Compiler
{
    public abstract class Statement: Node
    {
        public abstract void GenerateCode(CodeGenerator output);

    }

    public class StatementBlock : Node
    {
        public readonly List<Statement> Commands = new List<Statement>();

        public Scope ParentScope { get; }

        public StatementBlock(Scope scope) : base()
        {
            this.ParentScope = scope;
        }

        public void GenerateCode(CodeGenerator output)
        {
            foreach (var cmd in Commands)
            {
                cmd.GenerateCode(output);
            }
        }
        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var cmd in Commands)
            {
                if (cmd.IsNodeUsed(node))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class AssignStatement : Statement
    {
        public VarDeclaration variable;
        public Expression expression;

        public AssignStatement() : base()
        {

        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || variable.IsNodeUsed(node) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (variable.Register == null)
            {
                variable.Register = Parser.Instance.AllocRegister(output, variable, variable.Name);
            }

            var srcReg = expression.GenerateCode(output);
            output.AppendLine(this, $"COPY {srcReg} {variable.Register}");
            Parser.Instance.DeallocRegister(srcReg);
        }
    }

    public class ReturnStatement : Statement
    {
        public Expression expression;

        public MethodInterface method;

        public ReturnStatement(MethodInterface method, Expression expression) : base()
        {
            this.expression = expression;
            this.method = method;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || (expression != null && expression.IsNodeUsed(node));
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (expression != null)
            {
                if (this.method.ReturnType == VarKind.None)
                {
                    throw new System.Exception("unexpect return expression for void method: " + method.Name);
                }

                var reg = expression.GenerateCode(output);
                output.AppendLine(this, $"PUSH {reg}");
                Parser.Instance.DeallocRegister(reg);
            }
            else
            if (this.method.ReturnType != VarKind.None)
            {
                throw new System.Exception("expected return expression for non-void method: " + method.Name);
            }

            output.AppendLine(this, "RET");
        }
    }

    public class ThrowStatement : Statement
    {
        public readonly string message;

        public ThrowStatement(string msg) : base()
        {
            this.message = msg;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this, $"THROW {message}");
        }
    }

    public class IfStatement : Statement
    {
        public Expression condition;
        public StatementBlock body;
        public StatementBlock @else;
        public Scope Scope { get; }

        //private int label;

        public IfStatement(Scope parentScope) : base()
        {
            this.Scope = new Scope(parentScope, this.NodeID);
            //this.label = Parser.Instance.AllocateLabel();
        }

        public override bool IsNodeUsed(Node node)
        {
            if (@else != null && @else.IsNodeUsed(node))
            {
                return true;
            }

            return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = condition.GenerateCode(output);

            this.Scope.Enter(output);
            if (@else != null)
            {
                output.AppendLine(this, $"JMPNOT {reg} @else_{this.NodeID}");
                body.GenerateCode(output);
                output.AppendLine(this, $"JMP @then_{this.NodeID}");
                output.AppendLine(this, $"@else_{this.NodeID}: NOP");
                @else.GenerateCode(output);
            }
            else
            {
                output.AppendLine(this, $"JMPNOT {reg} @then_{this.NodeID}");
                body.GenerateCode(output);
            }
            output.AppendLine(this, $"@then_{this.NodeID}: NOP");
            this.Scope.Leave(output);

            Parser.Instance.DeallocRegister(reg);

        }
    }

    public class MethodCallStatement : Statement
    {
        public MethodExpression expression;

        public MethodCallStatement() : base()
        {

        }
        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = expression.GenerateCode(output);
            Parser.Instance.DeallocRegister(reg);
        }
    }

}

using System;
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

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            foreach (var cmd in Commands)
            {
                cmd.Visit(callback);
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

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            variable.Visit(callback);
            expression.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || variable.IsNodeUsed(node) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (variable.Register == null)
            {
                variable.Register = Compiler.Instance.AllocRegister(output, variable, variable.Name);
            }

            var srcReg = expression.GenerateCode(output);
            output.AppendLine(this, $"COPY {srcReg} {variable.Register}");
            Compiler.Instance.DeallocRegister(ref srcReg);
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

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            expression?.Visit(callback);
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
                    throw new System.Exception($"unexpect return expression for void method: {method.Name}");
                }

                if (this.method.ReturnType != this.expression.ResultType && this.method.ReturnType != VarKind.Any)
                {
                    throw new System.Exception($"expected return expression of type {this.method.ReturnType} for {method.Name}, got {this.expression.ResultType} instead");
                }

                var reg = expression.GenerateCode(output);
                output.AppendLine(this, $"PUSH {reg}");
                Compiler.Instance.DeallocRegister(ref reg);
            }
            else
            if (this.method.ReturnType != VarKind.None)
            {
                throw new System.Exception($"expected return expression for non-void method: {method.Name}");
            }

            output.AppendLine(this, "RET");
        }
    }

    public class EmitStatement : Statement
    {
        public EventDeclaration eventDecl;

        public Expression valueExpr;
        public Expression addressExpr;

        public EmitStatement(EventDeclaration evt, Expression addrExpr, Expression valueExpr) : base()
        {
            this.addressExpr = addrExpr;
            this.valueExpr = valueExpr;
            this.eventDecl = evt;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            eventDecl.Visit(callback);
            addressExpr.Visit(callback);
            valueExpr.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || eventDecl.IsNodeUsed(node) || addressExpr.IsNodeUsed(node) || valueExpr.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = valueExpr.GenerateCode(output);
            output.AppendLine(this, $"PUSH {reg}");
            Compiler.Instance.DeallocRegister(ref reg);

            reg = addressExpr.GenerateCode(output);
            output.AppendLine(this, $"PUSH {reg}");

            output.AppendLine(this, $"LOAD {reg} {eventDecl.value}");
            output.AppendLine(this, $"PUSH {reg}");

            output.AppendLine(this, $"LOAD {reg} \"Runtime.Notify\"");
            output.AppendLine(this, $"EXTCALL {reg}");

            Compiler.Instance.DeallocRegister(ref reg);
        }
    }

    public class ThrowStatement : Statement
    {
        public readonly string message;

        public ThrowStatement(string msg) : base()
        {
            this.message = msg;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
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

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            condition.Visit(callback);
            body.Visit(callback);
            @else?.Visit(callback);
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

            Compiler.Instance.DeallocRegister(ref reg);

        }
    }

    public class WhileStatement : Statement
    {
        public Expression condition;
        public StatementBlock body;
        public Scope Scope { get; }

        //private int label;

        public WhileStatement(Scope parentScope) : base()
        {
            this.Scope = new Scope(parentScope, this.NodeID);
            //this.label = Parser.Instance.AllocateLabel();
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            condition.Visit(callback);
            body.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this, $"@loop_{this.NodeID}: NOP");

            var reg = condition.GenerateCode(output);

            this.Scope.Enter(output);

            output.AppendLine(this, $"JMPNOT {reg} @break_{this.NodeID}");
            body.GenerateCode(output);

            output.AppendLine(this, $"JMP @loop_{this.NodeID}");
            output.AppendLine(this, $"@break_{this.NodeID}: NOP");

            this.Scope.Leave(output);

            Compiler.Instance.DeallocRegister(ref reg);

        }
    }

    public class DoWhileStatement : Statement
    {
        public Expression condition;
        public StatementBlock body;
        public Scope Scope { get; }

        //private int label;

        public DoWhileStatement(Scope parentScope) : base()
        {
            this.Scope = new Scope(parentScope, this.NodeID);
            //this.label = Parser.Instance.AllocateLabel();
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            condition.Visit(callback);
            body.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this, $"@loop_{this.NodeID}: NOP");

            this.Scope.Enter(output);

            body.GenerateCode(output);

            var reg = condition.GenerateCode(output);
            output.AppendLine(this, $"JMPIF {reg} @loop_{this.NodeID}");

            this.Scope.Leave(output);

            Compiler.Instance.DeallocRegister(ref reg);

        }
    }

    public class MethodCallStatement : Statement
    {
        public MethodExpression expression;

        public MethodCallStatement() : base()
        {

        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            expression.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = expression.GenerateCode(output);
            Compiler.Instance.DeallocRegister(ref reg);
        }
    }

    public class AsmBlockStatement : Statement
    {
        public string[] lines;

        public AsmBlockStatement(string[] lines) : base()
        {
            this.lines = lines;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            foreach (var line in lines)
            {
                output.AppendLine(this, line);
            }
        }
    }

}


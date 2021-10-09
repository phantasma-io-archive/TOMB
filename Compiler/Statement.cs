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
        public Expression valueExpression;
        public Expression indexExpression;

        public AssignStatement() : base()
        {

        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            variable.Visit(callback);
            valueExpression.Visit(callback);
            indexExpression?.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || variable.IsNodeUsed(node) || valueExpression.IsNodeUsed(node) || (indexExpression != null && indexExpression.IsNodeUsed(node));
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (variable.Register == null)
            {
                variable.Register = Compiler.Instance.AllocRegister(output, variable, variable.Name);
            }

            var srcReg = valueExpression.GenerateCode(output);
            
            if (indexExpression != null)
            {
                var idxReg = indexExpression.GenerateCode(output);

                output.AppendLine(this, $"PUT {srcReg} {variable.Register} {idxReg}");

                Compiler.Instance.DeallocRegister(ref idxReg);
            }
            else
            {
                output.AppendLine(this, $"COPY {srcReg} {variable.Register}");
            }

            Compiler.Instance.DeallocRegister(ref srcReg);
        }
    }

    public class ReturnStatement : Statement
    {
        public Expression expression;

        public MethodDeclaration method;

        public ReturnStatement(MethodDeclaration method, Expression expression) : base()
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
            var returnType = this.method.@interface.ReturnType;

            var simpleReturn = (this.method.ParentScope.Module is Script);

            if (expression != null)
            {
                if (returnType.Kind == VarKind.None)
                {
                    throw new System.Exception($"unexpect return expression for void method: {method.Name}");
                }

                this.expression = Expression.AutoCast(expression, returnType);

                var reg = this.expression.GenerateCode(output);
                output.AppendLine(this, $"PUSH {reg}");
                Compiler.Instance.DeallocRegister(ref reg);
            }
            else
            if (returnType.Kind != VarKind.None)
            {
                throw new System.Exception($"expected return expression for non-void method: {method.Name}");
            }

            if (simpleReturn)
            {
                output.AppendLine(this, "RET");
            }
            else
            {
                output.AppendLine(this, "JMP @" + this.method.GetExitLabel());
            }
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
        public readonly Expression expr;

        public ThrowStatement(Expression expr) : base()
        {
            this.expr = expr;
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
            var reg = expr.GenerateCode(output);
            output.AppendLine(this, $"THROW {reg}");
            Compiler.Instance.DeallocRegister(ref reg);
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

    public class CaseStatement: Statement
    {
        public LiteralExpression value;
        public StatementBlock body;

        internal Register variable;
        internal string endLabel;

        public CaseStatement(LiteralExpression value, StatementBlock body) : base()
        {
            this.value = value;
            this.body = body;
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = value.GenerateCode(output);

            output.AppendLine(this, $"EQUAL {variable} {reg} {reg}");

            output.AppendLine(this, $"JMPNOT {reg} @skip_{this.NodeID}");
            body.GenerateCode(output);
            output.AppendLine(this, $"JMP {endLabel}");
            output.AppendLine(this, $"@skip_{this.NodeID}: NOP");

            Compiler.Instance.DeallocRegister(ref reg);
        }

        public override bool IsNodeUsed(Node node)
        {
            return value.IsNodeUsed(node) || body.IsNodeUsed(node);
        }

        public override void Visit(Action<Node> callback)
        {
            value.Visit(callback);
            body.Visit(callback);
        }
    }

    public class SwitchStatement : Statement
    {
        public VarExpression variable;
        public StatementBlock @default;
        public List<CaseStatement> cases = new List<CaseStatement>();
        public Scope Scope { get; }

        //private int label;

        public SwitchStatement(Scope parentScope) : base()
        {
            this.Scope = new Scope(parentScope, this.NodeID);
            //this.label = Parser.Instance.AllocateLabel();
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            variable.Visit(callback);
            foreach (var entry in cases) 
            {
                entry.Visit(callback);
            }
            
            @default?.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            foreach (var entry in cases)
            {
                if (entry.IsNodeUsed(node))
                {
                    return true;
                }
            }

            if (@default != null && @default.IsNodeUsed(node))
            {
                return true;
            }

            return (node == this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = variable.GenerateCode(output);
            var endLabel = $"@end_case_{this.NodeID}";

            this.Scope.Enter(output);

            foreach (var entry in cases)
            {
                entry.variable = reg;
                entry.endLabel = endLabel;
                entry.GenerateCode(output);
            }

            if (@default != null)
            {
                @default.GenerateCode(output);
            }

            output.AppendLine(this, $"{endLabel}: NOP");
            this.Scope.Leave(output);

            Compiler.Instance.DeallocRegister(ref reg);

        }
    }

    public abstract class LoopStatement: Statement
    {
    }

    public class WhileStatement : LoopStatement
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
            Compiler.Instance.PushLoop(this);

            output.AppendLine(this, $"@loop_start_{this.NodeID}: NOP");

            var reg = condition.GenerateCode(output);

            this.Scope.Enter(output);

            output.AppendLine(this, $"JMPNOT {reg} @loop_end_{this.NodeID}");
            body.GenerateCode(output);

            output.AppendLine(this, $"JMP @loop_start_{this.NodeID}");
            output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

            this.Scope.Leave(output);

            Compiler.Instance.DeallocRegister(ref reg);
            Compiler.Instance.PopLoop(this);
        }
    }

    public class DoWhileStatement : LoopStatement
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
            Compiler.Instance.PushLoop(this);

            output.AppendLine(this, $"@loop_start_{this.NodeID}: NOP");

            this.Scope.Enter(output);

            body.GenerateCode(output);

            var reg = condition.GenerateCode(output);
            output.AppendLine(this, $"JMPIF {reg} @loop_start_{this.NodeID}");

            output.AppendLine(this, $"@loop_end_{this.NodeID}: NOP");

            this.Scope.Leave(output);

            Compiler.Instance.DeallocRegister(ref reg);
            Compiler.Instance.PopLoop(this);
        }
    }

    public class BreakStatement : Statement
    {
        public readonly Scope scope;

        public BreakStatement(Scope scope) : base()
        {
            this.scope = scope;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (Compiler.Instance.CurrentLoop == null)
            {
                if (this.scope.Method != null && this.scope.Method.@interface.Kind == MethodKind.Trigger)
                {
                    throw new CompilerException("trigger break not implemented");
                }

                throw new CompilerException("not inside a loop");
            }

            output.AppendLine(this, $"JMP @loop_end_{ Compiler.Instance.CurrentLoop.NodeID}");
        }
    }

    public class ContinueStatement : Statement
    {
        public readonly Scope scope;

        public ContinueStatement(Scope scope) : base()
        {
            this.scope = scope;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (Compiler.Instance.CurrentLoop == null)
            {
                if (this.scope.Method != null && this.scope.Method.@interface.Kind == MethodKind.Trigger)
                {
                    throw new CompilerException("trigger continuenot implemented");
                }

                throw new CompilerException("not inside a loop");
            }

            output.AppendLine(this, $"JMP @loop_start_{ Compiler.Instance.CurrentLoop.NodeID}");
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


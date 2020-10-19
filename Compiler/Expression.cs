using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Phantasma.Tomb.Compiler
{
    public abstract class Expression :Node
    {
        public abstract VarKind ResultType { get; }
        public Scope ParentScope { get; }

        public Expression(Scope parentScope) : base()
        {
            this.ParentScope = parentScope;
        }

        public abstract Register GenerateCode(CodeGenerator output);
    }

    public class NegationExpression : Expression
    {
        public Expression expr;
        public override VarKind ResultType => VarKind.Bool;

        public NegationExpression(Scope parentScope, Expression expr) : base(parentScope)
        {
            this.expr = expr;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = expr.GenerateCode(output);
            output.AppendLine(this, $"NOT {reg} {reg}");
            return reg;
        }


        public override void Visit(Action<Node> callback)
        {
            callback(this);
            expr.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || expr.IsNodeUsed(node);
        }
    }

    public class BinaryExpression : Expression
    {
        private OperatorKind op;
        private Expression left;
        private Expression right;

        public override VarKind ResultType => op.IsLogicalOperator() ? VarKind.Bool : left.ResultType;

        public BinaryExpression(Scope parentScope, OperatorKind op, Expression leftSide, Expression rightSide) : base(parentScope)
        {
            if (op == OperatorKind.Unknown)
            {
                throw new CompilerException("implementation failure");
            }

            this.op = op;
            this.left = leftSide;
            this.right = rightSide;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || left.IsNodeUsed(node) || right.IsNodeUsed(node);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            left.Visit(callback);
            right.Visit(callback);
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var regLeft = left.GenerateCode(output);
            var regRight = right.GenerateCode(output);
            var regResult = Parser.Instance.AllocRegister(output, this);

            Opcode opcode;
            switch (this.op)
            {
                case OperatorKind.Addition: opcode = Opcode.ADD; break;
                case OperatorKind.Subtraction: opcode = Opcode.SUB; break;
                case OperatorKind.Multiplication: opcode = Opcode.MUL; break;
                case OperatorKind.Division: opcode = Opcode.DIV; break;
                case OperatorKind.Modulus: opcode = Opcode.MOD; break;

                case OperatorKind.Equal: opcode = Opcode.EQUAL; break;
                case OperatorKind.Less: opcode = Opcode.LT; break;
                case OperatorKind.LessOrEqual: opcode = Opcode.LTE; break;
                case OperatorKind.Greater: opcode = Opcode.GT; break;
                case OperatorKind.GreaterOrEqual: opcode = Opcode.GTE; break;

                case OperatorKind.ShiftRight: opcode = Opcode.SHR; break;
                case OperatorKind.ShiftLeft: opcode = Opcode.SHL; break;

                case OperatorKind.Or: opcode = Opcode.OR; break;
                case OperatorKind.And: opcode = Opcode.AND; break;
                case OperatorKind.Xor: opcode = Opcode.XOR; break;

                default:
                    throw new CompilerException("not implemented vmopcode for " + op);
            }

            output.AppendLine(this, $"{opcode} {regLeft} {regRight} {regResult}");

            Parser.Instance.DeallocRegister(ref regRight);
            Parser.Instance.DeallocRegister(ref regLeft);

            return regResult;
        }

        public override string ToString()
        {
            return $"{left} {op} {right}";
        }
    }

    public class MethodExpression : Expression
    {
        public MethodInterface method;
        public List<Expression> arguments = new List<Expression>();

        public override VarKind ResultType => method.ReturnType;

        public MethodExpression(Scope parentScope) : base(parentScope)
        {

        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            foreach (var arg in arguments)
            {
                arg.Visit(callback);
            }
        }

        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var arg in arguments)
            {
                if (arg.IsNodeUsed(node))
                {
                    return true;
                }
            }

            return false;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            Register reg;

            if (this.method.PreCallback != null)
            {
                reg = this.method.PreCallback(output, ParentScope, this);
            }
            else
            {
                reg = Parser.Instance.AllocRegister(output, this, this.NodeID);
            }

            bool isCallLibrary = method.Library.Name == "Call";

            string customAlias = null;

            if (method.Implementation != MethodImplementationType.Custom)
            {
                for (int i = arguments.Count - 1; i >= 0; i--)
                {
                    var arg = arguments[i];

                    Register argReg;

                    if (isCallLibrary)
                    {
                        if (i == 0)
                        {
                            var literal = arg as LiteralExpression;
                            if (literal != null && literal.kind == VarKind.String)
                            {
                                customAlias = literal.value;
                            }
                            else
                            {
                                throw new Exception("Expected string literal as first argument");
                            }
                            argReg = null;
                        }
                        else
                        {
                            argReg = arg.GenerateCode(output);
                        }
                    }
                    else
                    {
                        var parameter = this.method.Parameters[i];
                        if (parameter.Callback != null)
                        {
                            argReg = parameter.Callback(output, ParentScope, arg);
                        }
                        else
                        {
                            argReg = arg.GenerateCode(output);
                        }
                    }

                    if (argReg != null)
                    {
                        output.AppendLine(arg, $"PUSH {argReg}");
                        Parser.Instance.DeallocRegister(ref argReg);
                    }
                }
            }

            switch (this.method.Implementation)
            {
                case MethodImplementationType.ExtCall:
                    {
                        var extCall = customAlias != null ? customAlias : $"\"{this.method.Alias}\"";
                        output.AppendLine(this, $"LOAD {reg} {extCall}");
                        output.AppendLine(this, $"EXTCALL {reg}");
                        break;
                    }

                case MethodImplementationType.Contract:
                    {
                        if (customAlias == null)
                        {
                            output.AppendLine(this, $"LOAD {reg} \"{this.method.Alias}\"");
                            output.AppendLine(this, $"PUSH {reg}");
                        }

                        var contractCall = customAlias != null ? customAlias : $"\"{this.method.Alias}\"";
                        output.AppendLine(this, $"LOAD {reg} {contractCall}");
                        output.AppendLine(this, $"CTX {reg} {reg}");
                        output.AppendLine(this, $"SWITCH {reg}");
                        break;
                    }

                case MethodImplementationType.Custom:

                    if (this.method.PreCallback == null && this.method.PostCallback == null)
                    {
                        output.AppendLine(this, $"THROW \"{this.method.Library.Name}.{this.method.Name} not implemented\"");
                    }

                    break;
            }

            if (this.method.ReturnType != VarKind.None && this.method.Implementation != MethodImplementationType.Custom) 
            {
                output.AppendLine(this, $"POP {reg}");
            }

            if (this.method.PostCallback != null)
            {
                reg = this.method.PostCallback(output, ParentScope, this, reg);
            }

            return reg;
        }
    }

    public class LiteralExpression : Expression
    {
        public string value;
        public VarKind kind;

        public LiteralExpression(Scope parentScope, string value, VarKind kind) : base(parentScope)
        {
            this.value = value;
            this.kind = kind;
        }

        public override string ToString()
        {
            return "literal: " + value;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Parser.Instance.AllocRegister(output, this, this.NodeID);

            output.AppendLine(this, $"LOAD {reg} {this.value}");

            this.CallNecessaryConstructors(output, kind, reg);

            return reg;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override VarKind ResultType => kind;
    }

    public class MacroExpression : Expression
    {
        public string value;

        public MacroExpression(Scope parentScope, string value) : base(parentScope)
        {
            this.value = value;
        }

        public override string ToString()
        {
            return "macro: " + value;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            throw new System.Exception($"macro {value} was not unfolded!");
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public LiteralExpression Unfold(Scope scope)
        {
            switch (value)
            {
                case "THIS_ADDRESS":
                    {
                        var addr = SmartContract.GetAddressForName(scope.Root.Name);
                        var hex = Base16.Encode(addr.ToByteArray());
                        return new LiteralExpression(scope, "0x"+hex, VarKind.Address);
                    }

                default:
                    throw new CompilerException($"unknown compile time macro: {value}");
            }
        }

        public override VarKind ResultType => VarKind.Unknown;
    }

    public class VarExpression : Expression
    {
        public VarDeclaration decl;

        public VarExpression(Scope parentScope, VarDeclaration declaration) : base(parentScope)
        {
            this.decl = declaration;
        }

        public override string ToString()
        {
            return decl.ToString();
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            if (decl.Register == null)
            {
                throw new CompilerException(this, $"var not initialized:" + decl.Name);
            }

            var reg = Parser.Instance.AllocRegister(output, this);
            output.AppendLine(this, $"COPY {decl.Register} {reg}");
            return reg;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            decl.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || node == decl;
        }

        public override VarKind ResultType => decl.Kind;
    }

    public class ConstExpression : Expression
    {
        public ConstDeclaration decl;

        public ConstExpression(Scope parentScope, ConstDeclaration declaration) : base(parentScope)
        {
            this.decl = declaration;
        }

        public override string ToString()
        {
            return decl.ToString();
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Parser.Instance.AllocRegister(output, this, decl.Name);
            output.AppendLine(this, $"LOAD {reg} {decl.Value}");
            this.CallNecessaryConstructors(output, decl.Kind, reg);
            return reg;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            decl.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || node == decl;
        }

        public override VarKind ResultType => decl.Kind;
    }

}

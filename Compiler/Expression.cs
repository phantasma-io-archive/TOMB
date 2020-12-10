using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Phantasma.Tomb.Compiler
{
    public abstract class Expression : Node
    {
        public abstract VarType ResultType { get; }
        public Scope ParentScope { get; }

        public Expression(Scope parentScope) : base()
        {
            this.ParentScope = parentScope;
        }

        public virtual string AsStringLiteral()
        {
            throw new CompilerException(this, $"{this.GetType()} can't be converted to string");
        }

        public abstract Register GenerateCode(CodeGenerator output);

        public static Expression AutoCast(Expression expr, VarType expectedType)
        {
            if (expr.ResultType == expectedType || expectedType.Kind == VarKind.Any)
            {
                return expr;
            }

            switch (expr.ResultType.Kind) 
            {
                case VarKind.Decimal:
                    switch (expectedType.Kind)
                    {
                        case VarKind.Decimal:
                        case VarKind.Number:
                                return new CastExpression(expr.ParentScope, expectedType, expr);
                    }
                    break;

                case VarKind.Any:
                    return new CastExpression(expr.ParentScope, expectedType, expr);
            }

            throw new CompilerException($"expected {expectedType} expression, got {expr.ResultType} instead");
        }
    }

    public class NegationExpression : Expression
    {
        public Expression expr;
        public override VarType ResultType => VarType.Find(VarKind.Bool);

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

    public class CastExpression : Expression
    {
        public Expression expr;

        public override VarType ResultType { get; }

        public CastExpression(Scope parentScope, VarType resultType, Expression expr) : base(parentScope)
        {
            this.expr = expr;
            this.ResultType = resultType;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = expr.GenerateCode(output);

            switch (expr.ResultType.Kind)
            {
                case VarKind.Decimal:
                    {
                        switch (this.ResultType.Kind)
                        {
                            case VarKind.Number:
                                return reg;

                            case VarKind.Decimal:
                                {
                                    var srcDecimals = ((DecimalVarType)expr.ResultType).decimals;
                                    var dstDecimals = ((DecimalVarType)this.ResultType).decimals;

                                    if (srcDecimals == dstDecimals)
                                    {
                                        return reg;
                                    }
                                    else
                                    if (srcDecimals < dstDecimals)
                                    {
                                        var diff = (dstDecimals - srcDecimals);
                                        var mult = (int)Math.Pow(10, diff);
                                        output.AppendLine(this, $"LOAD r0 {mult}");
                                        output.AppendLine(this, $"MUL {reg} r0 {reg}");
                                        return reg;
                                    }
                                    else
                                    {
                                        throw new CompilerException($"Decimal precision failure: {expr.ResultType} => {this.ResultType}");
                                    }
                                }

                            default:
                                throw new CompilerException($"Unsupported cast: {expr.ResultType} => {this.ResultType}");
                        }
                    }
            }

            var vmType = MethodInterface.ConvertType(ResultType);
            output.AppendLine(this, $"CAST {reg} {reg} #{vmType}");
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

        public override VarType ResultType => op.IsLogicalOperator() ? VarType.Find(VarKind.Bool) : left.ResultType;

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

        public override string AsStringLiteral()
        {
            if (ResultType.Kind == VarKind.String && op == OperatorKind.Addition)
            {
                return left.AsStringLiteral() + right.AsStringLiteral();
            }

            return base.AsStringLiteral();
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
            if (this.op == OperatorKind.Addition && left.ResultType.Kind == VarKind.String && right.ResultType.Kind != VarKind.String)
            {
                this.right = new CastExpression(this.ParentScope, VarType.Find(VarKind.String), right);
            }

            var regLeft = left.GenerateCode(output);
            var regRight = right.GenerateCode(output);
            var regResult = Compiler.Instance.AllocRegister(output, this);

            Opcode opcode;
            switch (this.op)
            {
                case OperatorKind.Addition: opcode = Opcode.ADD; break;
                case OperatorKind.Subtraction: opcode = Opcode.SUB; break;
                case OperatorKind.Multiplication: opcode = Opcode.MUL; break;
                case OperatorKind.Division: opcode = Opcode.DIV; break;
                case OperatorKind.Modulus: opcode = Opcode.MOD; break;
                case OperatorKind.Power: opcode = Opcode.POW; break;

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

            Compiler.Instance.DeallocRegister(ref regRight);
            Compiler.Instance.DeallocRegister(ref regLeft);

            return regResult;
        }

        public override string ToString()
        {
            return $"{left} {op} {right}";
        }
    }

    public class StructFieldExpression : Expression
    {
        public VarDeclaration varDecl;
        public string fieldName;

        public override VarType ResultType { get; }

        public StructFieldExpression(Scope parentScope, VarDeclaration varDecl, string fieldName) : base(parentScope)
        {
            var structInfo = ((StructVarType)varDecl.Type);

            VarType fieldType = null;

            foreach (var field in structInfo.decl.fields)
            {
                if (field.name == fieldName)
                {
                    fieldType = field.type;
                }
            }

            if (fieldType == null)
            {
                throw new CompilerException($"Struct {varDecl.Type} does not contain field: {fieldName}");
            }

            this.varDecl = varDecl;
            this.fieldName = fieldName;
            this.ResultType = fieldType;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            varDecl.Visit(callback);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this || node == varDecl);
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Compiler.Instance.AllocRegister(output, this/*, $"{varDecl.Name}.{fieldName}"*/);

            var tempReg = Compiler.Instance.AllocRegister(output, this);

            output.AppendLine(this, $"COPY {varDecl.Register} {reg}");
            output.AppendLine(this, $"LOAD {tempReg} \"{fieldName}\"");
            output.AppendLine(this, $"GET {reg} {reg} {tempReg}");

            Compiler.Instance.DeallocRegister(ref tempReg);

            return reg;
        }
    }

    public class MethodExpression : Expression
    {
        public MethodInterface method;
        public List<Expression> arguments = new List<Expression>();

        public List<VarType> generics = new List<VarType>();

        public override VarType ResultType => method.ReturnType;

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

        public void PatchGenerics()
        {
            int requiredGenerics = 0;

            this.method = this.method.Clone(this.method.Library);

            // auto patch storage methods
            if (this.generics.Count == 0)
            {
                var genericLib = this.method.Library as GenericLibraryDeclaration;
                if (genericLib != null)
                {
                    foreach (var type in genericLib.Generics)
                    {
                        this.generics.Add(type);
                    }
                }
            }

            if (ResultType.Kind == VarKind.Generic)
            {
                var generic = (GenericVarType)ResultType;

                if (generic.index < 0)
                {
                    throw new CompilerException($"weird generic index for return type of method {this.method.Name}, compiler bug?");
                }

                if (generic.index >= this.generics.Count)
                {
                    throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {this.method.Name}");
                }

                requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

                this.method.ReturnType = this.generics[generic.index];
            }

            for (int paramIndex = 0; paramIndex < this.method.Parameters.Length; paramIndex++)
            {
                var parameter = this.method.Parameters[paramIndex];
                if (parameter.Type.Kind == VarKind.Generic)
                {
                    var generic = (GenericVarType)parameter.Type;

                    if (generic.index < 0)
                    {
                        throw new CompilerException($"weird generic index for parameter {parameter.Name} of method {this.method.Name}, compiler bug?");
                    }

                    if (generic.index >= this.generics.Count)
                    {
                        throw new CompilerException($"missing generic declaration with index {generic.index} when calling method {this.method.Name}");
                    }

                    requiredGenerics = Math.Max(requiredGenerics, generic.index + 1);

                    this.method.Parameters[paramIndex] = new MethodParameter(parameter.Name, this.generics[generic.index]);
                }
            }


            if (requiredGenerics > generics.Count)
            {
                throw new CompilerException($"call to method {this.method.Name} expected {requiredGenerics} generics, got {generics.Count} instead");
            }
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
                reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);
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
                            customAlias = arg.AsStringLiteral();
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
                        Compiler.Instance.DeallocRegister(ref argReg);
                    }
                }
            }

            switch (this.method.Implementation)
            {
                case MethodImplementationType.LocalCall:
                    {
                        output.AppendLine(this, $"CALL @entry_{this.method.Name}");
                        break;
                    }

                case MethodImplementationType.ExtCall:
                    {
                        var extCall = customAlias != null ? customAlias : $"\"{this.method.Alias}\"";
                        output.AppendLine(this, $"LOAD {reg} {extCall}");
                        output.AppendLine(this, $"EXTCALL {reg}");
                        break;
                    }

                case MethodImplementationType.ContractCall:
                    {
                        if (customAlias == null)
                        {
                            output.AppendLine(this, $"LOAD {reg} \"{this.method.Alias}\"");
                            output.AppendLine(this, $"PUSH {reg}");
                        }

                        var contractCall = customAlias != null ? customAlias : $"\"{this.method.Contract}\"";
                        output.AppendLine(this, $"LOAD {reg} {contractCall}");
                        output.AppendLine(this, $"CTX {reg} {reg}");
                        output.AppendLine(this, $"SWITCH {reg}");
                        break;
                    }

                case MethodImplementationType.Custom:

                    if (this.method.PreCallback == null && this.method.PostCallback == null)
                    {
                        output.AppendLine(this, $"LOAD r0 \"{this.method.Library.Name}.{this.method.Name} not implemented\"");
                        output.AppendLine(this, $"THROW r0");
                    }

                    break;
            }

            if (this.method.ReturnType.Kind != VarKind.None && this.method.Implementation != MethodImplementationType.Custom) 
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
        public VarType type;

        public LiteralExpression(Scope parentScope, string value, VarType type) : base(parentScope)
        {
            this.value = value;
            this.type = type;
        }

        public override string ToString()
        {
            return "literal: " + value;
        }

        public override string AsStringLiteral()
        {
            if (this.type.Kind == VarKind.String)
            {
                return this.value;
            }

            return base.AsStringLiteral();
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Compiler.Instance.AllocRegister(output, this, this.NodeID);

            string val;

            switch (this.type.Kind)
            {
                case VarKind.Decimal:
                    {
                        var decType = this.type as DecimalVarType;
                        var temp = decimal.Parse(this.value, CultureInfo.InvariantCulture);
                        val = UnitConversion.ToBigInteger(temp, decType.decimals).ToString();
                        break;
                    }

                case VarKind.Enum:
                    {
                        val = $"{this.value} Enum";
                        break;
                    }

                case VarKind.Type:
                    {
                        var srcType = (VarKind) Enum.Parse(typeof(VarKind), this.value);
                        var vmType = MethodInterface.ConvertType(srcType);
                        val = $"{(int)vmType} Enum";
                        break;
                    }

                default:
                    {
                        val = this.value;
                        break;
                    }

            }

            output.AppendLine(this, $"LOAD {reg} {val}");

            this.CallNecessaryConstructors(output, type, reg);

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

        public override VarType ResultType => type;
    }

    public class MacroExpression : Expression
    {
        public string value;
        public string[] args;

        public MacroExpression(Scope parentScope, string value, IEnumerable<string> args) : base(parentScope)
        {
            this.value = value;
            this.args = args.ToArray();
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
                        var addr = SmartContract.GetAddressForName(scope.Module.Name);
                        var hex = Base16.Encode(addr.ToByteArray());
                        return new LiteralExpression(scope, "0x" + hex, VarType.Find(VarKind.Address));
                    }

                case "THIS_SYMBOL":
                    {
                        var module = scope.Module;

                        while (module.Kind != ModuleKind.Token && module.Parent != null)
                        {
                            module = module.Parent;
                        }

                        if (module.Kind == ModuleKind.Token)
                        {
                            return new LiteralExpression(scope, '\"' + module.Name + '\"', VarType.Find(VarKind.String));
                        }

                        throw new CompilerException($"macro {value} is not available here");
                    }

                case "TYPE_OF":
                    {
                        if (args.Length != 1)
                        {
                            throw new CompilerException($"macro {value} requires 1 argument");
                        }

                        var target = args[0];

                        VarKind kind;
                        if (!Enum.TryParse<VarKind>(target, true, out kind))
                        {
                            var decl = scope.FindVariable(target, false);
                            if (decl == null)
                            {
                                throw new CompilerException($"unknown identifier: {target}");
                            }

                            kind = decl.Type.Kind;
                        }

                        return new LiteralExpression(scope, kind.ToString(), VarType.Find(VarKind.Type));
                    }

                default:
                    throw new CompilerException($"unknown compile time macro: {value}");
            }
        }

        public override VarType ResultType => VarType.Find(VarKind.Unknown);
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

            var reg = Compiler.Instance.AllocRegister(output, this);
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

        public override VarType ResultType => decl.Type;
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

        public override string AsStringLiteral()
        {
            if (decl.Type.Kind == VarKind.String)
            {
                return decl.Value;
            }

            return base.AsStringLiteral();
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Compiler.Instance.AllocRegister(output, this, decl.Name);
            output.AppendLine(this, $"LOAD {reg} {decl.Value}");
            this.CallNecessaryConstructors(output, decl.Type, reg);
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

        public override VarType ResultType => decl.Type;
    }

}

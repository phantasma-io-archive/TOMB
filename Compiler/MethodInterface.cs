using Phantasma.VM;
using System;

namespace Phantasma.Tomb.Compiler
{
    public class MethodParameter: Node
    {
        public string Name { get; private set; }
        public VarKind Kind { get; internal set; }

        public Func<CodeGenerator, Scope, Expression, Register> Callback;

        public MethodParameter(string name, VarKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"{Name}:{Kind}";
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }

        public void SetParameterCallback(Func<CodeGenerator, Scope, Expression, Register> callback)
        {
            this.Callback = callback;
        }
    }

    public enum MethodImplementationType
    {
        ExtCall,
        Contract,
        Custom
    }

    public class MethodInterface
    {
        public string Name;
        public LibraryDeclaration Library;
        public MethodKind Kind;
        public VarKind ReturnType;
        public MethodParameter[] Parameters;
        public string Alias;
        public string Contract;
        public MethodImplementationType Implementation;
        public Func<CodeGenerator, Scope, MethodExpression, Register> PreCallback;
        public Func<CodeGenerator, Scope, MethodExpression, Register, Register> PostCallback;

        public int StartAsmLine;
        public int EndAsmLine;

        public MethodInterface(LibraryDeclaration library, MethodImplementationType implementation, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, string alias = null) 
        {
            this.Name = name;
            this.Library = library;
            this.Implementation = implementation;
            this.Kind = kind;
            this.ReturnType = returnType;
            this.Parameters = parameters;
            this.PreCallback = null;
            this.PostCallback = null;

            this.Contract = this.Library.Name;

            if (alias != null)
            {
                this.Alias = alias;
            }
            else
            {
                this.Alias = $"{char.ToUpper(this.Name[0])}{this.Name.Substring(1)}";
                if (implementation == MethodImplementationType.ExtCall)
                {
                    this.Alias = this.Library.Name + '.' + this.Alias;
                }
            }            
        }

        public override string ToString()
        {
            return $"method {Name}:{ReturnType}";
        }

        public MethodInterface SetContract(string contract)
        {
            this.Contract = contract.ToLower();
            return this;
        }

        public MethodInterface SetAlias(string alias)
        {
            this.Alias = alias;
            return this;
        }

        public MethodInterface SetPreCallback(Func<CodeGenerator, Scope, MethodExpression, Register> callback)
        {
            this.PreCallback = callback;
            return this;
        }

        public MethodInterface SetPostCallback(Func<CodeGenerator, Scope, MethodExpression, Register, Register> callback)
        {
            this.PostCallback = callback;
            return this;
        }

        public MethodInterface SetParameterCallback(string name, Func<CodeGenerator, Scope, Expression, Register> callback)
        {
            foreach (var parameter in Parameters)
            {
                if (parameter.Name == name)
                {
                    parameter.SetParameterCallback(callback);
                    return this;
                }
            }

            throw new Exception($"method {this.Library.Name}.{this.Name} contains no parameter called {name}");
        }

        public void PatchParam(string name, VarKind kind)
        {
            foreach (var arg in Parameters)
            {
                if (arg.Name == name)
                {
                    if (arg.Kind != VarKind.Generic)
                    {
                        throw new Exception($"Expected parameter {arg.Name} to be patchable as generic");
                    }

                    arg.Kind = kind;
                    break;
                }
            }
        }

        public static VMType ConvertType(VarKind kind)
        {
            switch (kind)
            {
                case VarKind.Address:
                case VarKind.Bytes:
                case VarKind.Hash:
                    return VMType.Bytes;

                case VarKind.Bool:
                    return VMType.Bool;

                case VarKind.Method:
                case VarKind.Number:
                    return VMType.Number;

                case VarKind.String:
                    return VMType.String;

                case VarKind.Timestamp:
                    return VMType.Timestamp;

                case VarKind.None:
                    return VMType.None;

                default:
                    throw new System.Exception("Not a valid ABI return type: " + kind);
            }
        }

    }
}

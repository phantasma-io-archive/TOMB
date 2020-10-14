using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System.Collections.Generic;

namespace Phantasma.Tomb.Compiler
{
    public abstract class Declaration: Node
    {
        public readonly string Name;
        public Scope ParentScope { get; }

        protected Declaration(Scope parentScope, string name)
        {
            Name = name;
            ParentScope = parentScope;
        }
    }

    public class VarDeclaration : Declaration
    {
        public VarKind Kind;
        public VarStorage Storage;
        public Register Register = null;

        public VarDeclaration(Scope parentScope, string name, VarKind kind, VarStorage storage) : base(parentScope, name)
        {
            this.Kind = kind;
            this.Storage = storage;
        }

        public override string ToString()
        {
            return $"var {Name}:{Kind}";
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }
    }

    public class MapDeclaration: VarDeclaration
    {
        public VarKind KeyKind;
        public VarKind ValueKind;

        public MapDeclaration(Scope parentScope, string name, VarKind keyKind, VarKind valKind) : base(parentScope, name, VarKind.Storage_Map, VarStorage.Global)
        {
            this.KeyKind = keyKind;
            this.ValueKind = valKind;
        }
    }

    public class ListDeclaration : VarDeclaration
    {
        public VarKind ValueKind;

        public ListDeclaration(Scope parentScope, string name, VarKind valKind) : base(parentScope, name, VarKind.Storage_List, VarStorage.Global)
        {
            this.ValueKind = valKind;
        }
    }

    public class SetDeclaration : VarDeclaration
    {
        public VarKind ValueKind;

        public SetDeclaration(Scope parentScope, string name, VarKind valKind) : base(parentScope, name, VarKind.Storage_Set, VarStorage.Global)
        {
            this.ValueKind = valKind;
        }
    }

    public class ConstDeclaration : Declaration
    {
        public VarKind Kind;
        public string Value;

        public ConstDeclaration(Scope parentScope, string name, VarKind kind, string value) : base(parentScope, name)
        {
            this.Kind = kind;
            this.Value = value;
        }

        public void GenerateCode(CodeGenerator output)
        {
            // DO NOTHING
        }

        public override string ToString()
        {
            return $"const {Name}:{Kind}";
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }
    }

    public class LibraryDeclaration : Declaration
    {
        public Dictionary<string, MethodInterface> methods = new Dictionary<string, MethodInterface>();

        public LibraryDeclaration(Scope parentScope, string name) : base(parentScope, name)
        {
        }

        public void GenerateCode(CodeGenerator output)
        {
            // DO NOTHING
        }

        public void AddMethod(string name, MethodImplementationType convention, VarKind returnType, MethodParameter[] parameters, string alias = null)
        {
            /*if (name != name.ToLower())
            {
                throw new CompilerException(parser, "invalid method name: " + name);
            }*/

            var method = new MethodInterface(this, convention, name, MethodKind.Method, returnType, parameters, alias);
            methods[name] = method;
        }

        public MethodInterface FindMethod(string name, bool required = true)
        {
            /*if (name != name.ToLower())
            {
                throw new CompilerException(parser, "invalid method name: " + name);
            }*/

            if (methods.ContainsKey(name))
            {
                return methods[name];
            }

            if (required)
            {
                throw new CompilerException("unknown method: " + name);
            }

            return null;
        }

        public override string ToString()
        {
            return $"library {Name}";
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }

        public void PatchParam(string name, VarKind kind)
        {
            foreach (var method in methods.Values)
            {
                method.PatchParam(name, kind);
            }
        }

        public void PatchMap(MapDeclaration mapDecl)
        {
            PatchParam("key", mapDecl.KeyKind);
            PatchParam("value", mapDecl.ValueKind);
            FindMethod("get").ReturnType = mapDecl.ValueKind;
        }

        public void PatchList(ListDeclaration mapDecl)
        {
            PatchParam("value", mapDecl.ValueKind);
            FindMethod("get").ReturnType = mapDecl.ValueKind;
        }

        public void PatchSet(SetDeclaration mapDecl)
        {
            PatchParam("value", mapDecl.ValueKind);
            FindMethod("get").ReturnType = mapDecl.ValueKind;
        }
    }

    public class MethodDeclaration : Declaration
    {
        public readonly MethodInterface @interface;
        public readonly StatementBlock body;
        public readonly Scope scope;

        public MethodDeclaration(Scope scope, MethodInterface @interface, StatementBlock body) : base(scope.Parent, @interface.Name)
        {
            this.body = body;
            this.scope = scope;
            this.@interface = @interface;
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this) || (body.IsNodeUsed(node));
        }

        public void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this);
            output.AppendLine(this, $"// ********* {this.Name} {this.@interface.Kind} ***********");
            output.AppendLine(this, $"@{GetEntryLabel()}:");

            Register tempReg1 = null;

            bool isConstructor = this.@interface.Kind == MethodKind.Constructor;

            // here we generate code that runs at the entry point of this method
            // we need to fetch the global variables from storage and allocate registers for them
            foreach (var variable in this.scope.Parent.Variables.Values)
            {
                if (variable.Storage != VarStorage.Global)
                {
                    continue;
                }

                if (!this.IsNodeUsed(variable))
                {
                    variable.Register = null;
                    continue;
                }

                if (tempReg1 == null && !isConstructor)
                {
                    tempReg1 = Parser.Instance.AllocRegister(output, this);
                    output.AppendLine(this, $"LOAD {tempReg1} \"Data.Get\"");
                }

                var reg = Parser.Instance.AllocRegister(output, variable, variable.Name);
                variable.Register = reg;

                if (isConstructor)
                {
                    continue; // in a constructor we don't need to read the vars from storage as they dont exist yet
                }

                var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name, false);

                VM.VMType vmType;

                switch (variable.Kind)
                {
                    case VarKind.Number:
                        vmType = VM.VMType.Number;
                        break;

                    case VarKind.String:
                        vmType = VM.VMType.String;
                        break;

                    case VarKind.Timestamp:
                        vmType = VM.VMType.Timestamp;
                        break;

                    case VarKind.Bool:
                        vmType = VM.VMType.Bool;
                        break;

                    default:
                        vmType = VM.VMType.Bytes;
                        break;
                }

                output.AppendLine(this, $"LOAD r0 {(int)vmType}");
                output.AppendLine(this, $"PUSH r0");
                output.AppendLine(this, $"LOAD r0 0x{Base16.Encode(fieldKey)}");
                output.AppendLine(this, $"PUSH r0");
                output.AppendLine(this, $"EXTCALL {tempReg1}");
                output.AppendLine(this, $"POP {reg}");
            }
            Parser.Instance.DeallocRegister(tempReg1);
            tempReg1 = null;

            foreach (var variable in this.scope.Variables.Values)
            {
                if (variable.Storage != VarStorage.Argument)
                {
                    continue;
                }

                variable.Register = Parser.Instance.AllocRegister(output, variable, variable.Name);
            }

            this.scope.Enter(output);
            body.GenerateCode(output);
            this.scope.Leave(output);

            // NOTE we don't need to dealloc anything here besides the global vars
            foreach (var variable in this.scope.Parent.Variables.Values)
            {
                if (variable.Storage != VarStorage.Global)
                {
                    continue;
                }

                if (variable.Register == null)
                {
                    var isGeneric = variable.Kind == VarKind.Storage_Map || variable.Kind == VarKind.Storage_List || variable.Kind == VarKind.Storage_Set;

                    if (isConstructor && !isGeneric)
                    {
                        throw new CompilerException("global variable not assigned in constructor: " + variable.Name);
                    }

                    continue; // if we hit this, means it went unused 
                }

                if (tempReg1 == null)
                {
                    tempReg1 = Parser.Instance.AllocRegister(output, this);
                    output.AppendLine(this, $"LOAD {tempReg1} \"Data.Set\"");
                }

                var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name, false);

                // NOTE we could keep this key loaded in a register if we had enough spare registers..
                output.AppendLine(this, $"PUSH {variable.Register}");
                output.AppendLine(this, $"LOAD r0 0x{Base16.Encode(fieldKey)}");
                output.AppendLine(this, $"PUSH r0");
                output.AppendLine(this, $"EXTCALL {tempReg1}");

                if (variable.Register != null)
                {
                    Parser.Instance.DeallocRegister(variable.Register);
                    variable.Register = null;
                }
            }
            Parser.Instance.DeallocRegister(tempReg1);
            tempReg1 = null;

            output.AppendLine(this, "RET");
        }

        internal string GetEntryLabel()
        {
            if (@interface.Kind == MethodKind.Constructor)
            {
                return "entry_constructor";
            }
            else
            {
                return "entry_" + this.Name;
            }
        }

        internal ContractMethod GetABI()
        {
            var temp = new List<ContractParameter>();

            foreach (var entry in this.@interface.Parameters)
            {
                temp.Add(new ContractParameter(entry.Name, ConvertType(entry.Kind)));
            }

            return new ContractMethod(this.Name, ConvertType(this.@interface.ReturnType), temp.ToArray());
        }

        private static VMType ConvertType(VarKind kind)
        {
            switch (kind)
            {
                case VarKind.Address:
                case VarKind.Bytes:
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

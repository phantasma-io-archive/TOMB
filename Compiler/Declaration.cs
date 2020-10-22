using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

    public struct StructField
    {
        public readonly string name;
        public readonly VarType type;

        public StructField(string name, VarType type)
        {
            this.name = name;
            this.type = type;
        }
    }

    public class StructDeclaration: Declaration
    {
        public StructField[] fields;

        public StructDeclaration(string name, IEnumerable<StructField> fields) : base(null, name)
        {
            this.fields = fields.ToArray();
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }
    }

    public class VarDeclaration : Declaration
    {
        public VarType Type;
        public VarStorage Storage;
        public Register Register = null;

        public VarDeclaration(Scope parentScope, string name, VarType type, VarStorage storage) : base(parentScope, name)
        {
            this.Type = type;
            this.Storage = storage;
        }

        public override string ToString()
        {
            return $"var {Name}:{Type}";
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }
    }

    public class MapDeclaration: VarDeclaration
    {
        public VarType KeyKind;
        public VarType ValueKind;

        public MapDeclaration(Scope parentScope, string name, VarType keyKind, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_Map), VarStorage.Global)
        {
            this.KeyKind = keyKind;
            this.ValueKind = valKind;
        }
    }

    public class ListDeclaration : VarDeclaration
    {
        public VarType ValueKind;

        public ListDeclaration(Scope parentScope, string name, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_List), VarStorage.Global)
        {
            this.ValueKind = valKind;
        }
    }

    public class SetDeclaration : VarDeclaration
    {
        public VarType ValueKind;

        public SetDeclaration(Scope parentScope, string name, VarType valKind) : base(parentScope, name, VarType.Find(VarKind.Storage_Set), VarStorage.Global)
        {
            this.ValueKind = valKind;
        }
    }

    public class ConstDeclaration : Declaration
    {
        public VarType Type;
        public string Value;

        public ConstDeclaration(Scope parentScope, string name, VarType kind, string value) : base(parentScope, name)
        {
            this.Type = kind;
            this.Value = value;
        }

        public void GenerateCode(CodeGenerator output)
        {
            // DO NOTHING
        }

        public override string ToString()
        {
            return $"const {Name}:{Type}";
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }
 
        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }
    }

    public class LibraryDeclaration : Declaration
    {
        public Dictionary<string, MethodInterface> methods = new Dictionary<string, MethodInterface>();

        public bool IsGeneric { get; private set; }

        public LibraryDeclaration(Scope parentScope, string name) : base(parentScope, name)
        {
            IsGeneric = false;
        }

        public void GenerateCode(CodeGenerator output)
        {
            // DO NOTHING
        }

        public MethodInterface AddMethod(string name, MethodImplementationType convention, VarKind returnKind, MethodParameter[] parameters, string alias = null)
        {
            return AddMethod(name, convention, VarType.Find(returnKind), parameters, alias);
        }

        public MethodInterface AddMethod(string name, MethodImplementationType convention, VarType returnType, MethodParameter[] parameters, string alias = null)
        {
            /*if (name != name.ToLower())
            {
                throw new CompilerException(parser, "invalid method name: " + name);
            }*/

            foreach (var entry in parameters)
            {
                if (entry.Type.Kind == VarKind.Generic)
                {
                    IsGeneric = true;
                }
            }

            var method = new MethodInterface(this, convention, name, true, MethodKind.Method, returnType, parameters, alias);
            methods[name] = method;

            return method;
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

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return node == this;
        }

        public LibraryDeclaration Clone(string name)
        {
            var result = new LibraryDeclaration(this.ParentScope, name);
            foreach (var method in this.methods.Values)
            {
                var parameters = new List<MethodParameter>();
                
                foreach (var parameter in method.Parameters)
                {
                    var entry = new MethodParameter(parameter.Name, parameter.Type);
                    entry.Callback = parameter.Callback;
                    parameters.Add(entry);
                }

                var newMethod = new MethodInterface(result, method.Implementation, method.Name, method.IsPublic, method.Kind, method.ReturnType, parameters.ToArray(), method.Alias);
                newMethod.Contract = method.Contract;
                newMethod.PreCallback = method.PreCallback;
                newMethod.PostCallback = method.PostCallback;

                result.methods[method.Name] = newMethod;
            }

            return result;
        }

        public void PatchParam(string name, VarType kind)
        {
            foreach (var method in methods.Values)
            {
                method.PatchParam(name, kind);
            }
        }

        private Dictionary<string, LibraryDeclaration> _genericCache = new Dictionary<string, LibraryDeclaration>();

        public LibraryDeclaration PatchMap(MapDeclaration mapDecl)
        {
            var key = $"<{mapDecl.KeyKind},{mapDecl.ValueKind}>m";
            if (_genericCache.ContainsKey(key))
            {
                return _genericCache[key];
            }

            var lib = this.Clone(this.Name+key);
            lib.PatchParam("key", mapDecl.KeyKind);
            lib.PatchParam("value", mapDecl.ValueKind);
            lib.FindMethod("get").ReturnType = mapDecl.ValueKind;
            
            _genericCache[key] = lib;
            return lib;
        }

        public LibraryDeclaration PatchList(ListDeclaration listDecl)
        {
            var key = $"<{listDecl.ValueKind}>l";
            if (_genericCache.ContainsKey(key))
            {
                return _genericCache[key];
            }

            var lib = this.Clone(this.Name + key);

            lib.PatchParam("value", listDecl.ValueKind);
            lib.FindMethod("get").ReturnType = listDecl.ValueKind;
            
            _genericCache[key] = lib;
            return lib;
        }

        public LibraryDeclaration PatchSet(SetDeclaration setDecl)
        {
            var key = $"<{setDecl.ValueKind}>s";
            if (_genericCache.ContainsKey(key))
            {
                return _genericCache[key];
            }

            var lib = this.Clone(this.Name + key);

            lib.PatchParam("value", setDecl.ValueKind);
            lib.FindMethod("get").ReturnType = setDecl.ValueKind;

            _genericCache[key] = lib;
            return lib;
        }
    }

    public class EventDeclaration : Declaration
    {
        public readonly Scope scope;
        public readonly byte value;
        public readonly VarType returnType;
        public readonly byte[] descriptionScript;

        struct StringToken
        {
            public readonly bool dynamic;
            public readonly string value;

            public StringToken(bool dynamic, string value)
            {
                this.dynamic = dynamic;
                this.value = value;
            }

            public override string ToString()
            {
                return value;
            }
        }

        public EventDeclaration(Scope scope, string name, byte value, VarType returnType, byte[] description) : base(scope.Parent, name)
        {
            this.scope = scope;
            this.value = value;
            this.returnType = returnType;
            this.descriptionScript = description;

        }

        public static byte[] GenerateScriptFromString(string src)
        {
            src = src.Substring(1, src.Length - 2); // remove "" delimiters

            var tokens = new List<StringToken>();

            var sb = new StringBuilder();

            bool insideTags = false;
            for (int i = 0; i < src.Length; i++)
            {
                var ch = src[i];

                switch (ch)
                {
                    case '{':
                        if (insideTags)
                        {
                            throw new CompilerException("Open declaration tag mismatch");
                        }

                        if (sb.Length > 0)
                        {
                            tokens.Add(new StringToken(false, sb.ToString()));
                            sb.Clear();
                        }
                        insideTags = true;
                        break;

                    case '}':
                        if (!insideTags)
                        {
                            throw new CompilerException("Close declaration tag mismatch");
                        }

                        if (sb.Length == 0)
                        {
                            throw new CompilerException("Empty declaration tag");
                        }
                        insideTags = false;
                        tokens.Add(new StringToken(true, sb.ToString()));
                        sb.Clear();
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            if (sb.Length > 0)
            {
                tokens.Add(new StringToken(false, sb.ToString()));
            }

            sb.Clear();
            sb.AppendLine("POP r2"); // address
            sb.AppendLine("POP r3"); // data
            sb.AppendLine("LOAD r0 \"\"");
            foreach (var token in tokens)
            {
                if (token.dynamic) {
                    if (token.value == "address")
                    {
                        sb.AppendLine($"CAST r2 r1 #String");
                    }
                    else
                    if (token.value == "data")
                    {
                        sb.AppendLine($"CAST r3 r1 #String");
                    }
                    else
                    if (token.value.StartsWith("data."))
                    {
                        throw new CompilerException($"Struct tags not implemented");
                    }
                    else
                    {
                        throw new CompilerException($"Invalid declaration tag: {token.value}");
                    }
                }
                else
                {
                    sb.AppendLine($"LOAD r1 \"{token.value}\"");
                }
                sb.AppendLine("ADD r0 r1 r0");
            }
            sb.AppendLine("PUSH r0"); // return result
            sb.AppendLine("RET"); 

            var asm = sb.ToString();
            var script = AssemblerUtils.BuildScript(asm);


            return script;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
        }

        public override bool IsNodeUsed(Node node)
        {
            return (node == this);
        }

        public void GenerateCode(CodeGenerator output)
        {
            // do nothing
        }

        private VMObject GenerateTestObject(VarType type)
        {
            VMObject obj;

            switch (type.Kind)
            {
                case VarKind.Number:
                    obj = VMObject.FromObject(new BigInteger(123));
                    break;

                case VarKind.String:
                    obj = VMObject.FromObject("test");
                    break;

                case VarKind.Bool:
                    obj = VMObject.FromObject(true);
                    break;

                case VarKind.Struct:
                    var fields = new Dictionary<VMObject, VMObject>();

                    var structInfo = type as StructVarType;
                    foreach (var field in structInfo.decl.fields)
                    {
                        fields[VMObject.FromObject(field.name)] = GenerateTestObject(field.type);
                    }

                    obj = new VMObject();
                    obj.SetValue(fields);

                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(stream))
                        {
                            obj.SerializeData(writer);
                        }

                        var bytes = stream.ToArray();
                        obj.SetValue(bytes);
                    }

                    break;


                default:
                    throw new CompilerException($"Can't initialize test object with type: {type}");
            }

            return obj;
        }

        private class CustomDescriptionVM : DescriptionVM
        {
            public CustomDescriptionVM(byte[] script) : base(script)
            {
            }

            public override IToken FetchToken(string symbol)
            {
                return new Blockchain.Tokens.TokenInfo(symbol, symbol, 0, 8, TokenFlags.None, new byte[0]);
            }

            public override string OutputAddress(Address address)
            {
                return address.Text;
            }

            public override string OutputSymbol(string symbol)
            {
                return symbol;
            }
        }

        public void Validate()
        {
            try
            {
                var vm = new CustomDescriptionVM(this.descriptionScript);

                var obj = GenerateTestObject(this.returnType);
                vm.Stack.Push(obj);
                vm.Stack.Push(VMObject.FromObject(Address.FromText("S3dApERMJUMRYECjyKLJioz2PCBUY6HBnktmC9u1obhDAgm")));
                vm.ThrowOnFault = true;
                var state = vm.Execute();

                if (state != ExecutionState.Halt)
                {
                    throw new CompilerException("description script did not execute correctly");
                }

                if (vm.Stack.Count > 0)
                {
                    var result = vm.Stack.Pop();
                }
                else
                {
                    throw new CompilerException("description script did not return a result");
                }
            }
            catch (Exception e)
            {
                if (e is CompilerException)
                {
                    throw e;
                }

                throw new CompilerException($"Error validating description script. {e.Message}");
            }
        }

        internal ContractEvent GetABI()
        {
            var type = MethodInterface.ConvertType(this.returnType);
            return new ContractEvent(this.value, this.Name, type, descriptionScript);
        }
    }

    public class MethodDeclaration : Declaration
    {
        public readonly MethodInterface @interface;
        public readonly Scope scope;

        public StatementBlock body { get; internal set; }

        public MethodDeclaration(Scope scope, MethodInterface @interface) : base(scope.Parent, @interface.Name)
        {
            this.body = null;
            this.scope = scope;
            this.@interface = @interface;
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);
            body.Visit(callback);
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

            this.@interface.StartAsmLine = output.LineCount;

            Register tempReg1 = null;
            Register tempReg2 = null;

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
                    tempReg1 = Compiler.Instance.AllocRegister(output, this, "dataGet");
                    output.AppendLine(this, $"LOAD {tempReg1} \"Data.Get\"");

                    tempReg2 = Compiler.Instance.AllocRegister(output, this, "contractName");
                    output.AppendLine(this, $"LOAD {tempReg2} \"{this.scope.Root.Name}\"");
                }

                var reg = Compiler.Instance.AllocRegister(output, variable, variable.Name);
                variable.Register = reg;

                if (isConstructor)
                {
                    continue; // in a constructor we don't need to read the vars from storage as they dont exist yet
                }

                //var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name, false);

                VM.VMType vmType = MethodInterface.ConvertType(variable.Type);

                output.AppendLine(this, $"// reading global: {variable.Name}");
                output.AppendLine(this, $"LOAD r0 {(int)vmType}");
                output.AppendLine(this, $"PUSH r0");
                output.AppendLine(this, $"LOAD r0 \"{variable.Name}\"");
                output.AppendLine(this, $"PUSH r0");
                output.AppendLine(this, $"PUSH {tempReg2}");
                output.AppendLine(this, $"EXTCALL {tempReg1}");
                output.AppendLine(this, $"POP {reg}");
                variable.CallNecessaryConstructors(output, variable.Type, reg);
            }

            Compiler.Instance.DeallocRegister(ref tempReg1);
            Compiler.Instance.DeallocRegister(ref tempReg2);

            foreach (var variable in this.scope.Variables.Values)
            {
                if (variable.Storage != VarStorage.Argument)
                {
                    continue;
                }

                variable.Register = Compiler.Instance.AllocRegister(output, variable, variable.Name);
                output.AppendLine(this, $"POP {variable.Register}");
                variable.CallNecessaryConstructors(output, variable.Type, variable.Register);
            }

            this.scope.Enter(output);
            body.GenerateCode(output);
            this.scope.Leave(output);

            foreach (var variable in this.scope.Variables.Values)
            {
                if (variable.Storage != VarStorage.Argument)
                {
                    continue;
                }

                Compiler.Instance.DeallocRegister(ref variable.Register);
            }

            // NOTE we don't need to dealloc anything here besides the global vars
            foreach (var variable in this.scope.Parent.Variables.Values)
            {
                if (variable.Storage != VarStorage.Global)
                {
                    continue;
                }

                if (variable.Register == null)
                {                    
                    if (isConstructor && !variable.Type.IsGeneric)
                    {
                        throw new CompilerException("global variable not assigned in constructor: " + variable.Name);
                    }

                    continue; // if we hit this, means it went unused 
                }

                bool isAssigned = false;
                this.body.Visit((node) =>
                {
                    var assignement = node as AssignStatement;
                    if (assignement != null && assignement.variable == variable)
                    {
                        isAssigned = true;
                    }
                });

                // if the global variable is not assigned within the current method, no need to save it value back to the storage
                if (isAssigned)
                {
                    if (tempReg1 == null)
                    {
                        tempReg1 = Compiler.Instance.AllocRegister(output, this);
                        output.AppendLine(this, $"LOAD {tempReg1} \"Data.Set\"");
                    }

                    // NOTE we could keep this key loaded in a register if we had enough spare registers..
                    output.AppendLine(this, $"// writing global: {variable.Name}");
                    output.AppendLine(this, $"PUSH {variable.Register}");
                    output.AppendLine(this, $"LOAD r0 \"{variable.Name}\"");
                    output.AppendLine(this, $"PUSH r0");
                    output.AppendLine(this, $"EXTCALL {tempReg1}");
                }

                if (variable.Register != null)
                {
                    Compiler.Instance.DeallocRegister(ref variable.Register);
                }
            }
            Compiler.Instance.DeallocRegister(ref tempReg1);

            output.AppendLine(this, "RET");
            this.@interface.EndAsmLine = output.LineCount;
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
                temp.Add(new ContractParameter(entry.Name, MethodInterface.ConvertType(entry.Type)));
            }

            return new ContractMethod(this.Name, MethodInterface.ConvertType(this.@interface.ReturnType), -1, temp.ToArray());
        }
    }
}

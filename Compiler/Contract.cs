using Phantasma.Domain;
using Phantasma.VM;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.Compiler
{
    public class Contract : Node
    {
        public readonly string Name;

        public readonly Dictionary<string, LibraryDeclaration> Libraries = new Dictionary<string, LibraryDeclaration>();

        public readonly Dictionary<string, MethodDeclaration> Methods = new Dictionary<string, MethodDeclaration>();

        public readonly LibraryDeclaration library;

        public Scope Scope { get; }

        public Contract(string name) : base()
        {
            this.Name = name;
            this.Scope = new Scope(this);
            this.library = AddLibrary("this");             
        }

        public override bool IsNodeUsed(Node node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var method in Methods.Values)
            {
                if (method.IsNodeUsed(node))
                {
                    return true;
                }
            }

            foreach (var lib in Libraries.Values)
            {
                if (lib.IsNodeUsed(node))
                {
                    return true;
                }
            }

            return false;
        }

        public ContractInterface GenerateCode(CodeGenerator output)
        {
            this.Scope.Enter(output);

            {
                var reg = Parser.Instance.AllocRegister(output, this, "methodName");
                output.AppendLine(this, $"POP {reg}");
                foreach (var entry in Methods.Values)
                {
                    output.AppendLine(this, $"LOAD r0, \"{entry.Name}\"");
                    output.AppendLine(this, $"EQUAL r0, {reg}");
                    output.AppendLine(this, $"JMPIF r0, @{entry.GetEntryLabel()}");
                }
                Parser.Instance.DeallocRegister(reg);
                output.AppendLine(this, "THROW \"unknown method was called\"");
            }

            foreach (var entry in Methods.Values)
            {
                entry.GenerateCode(output);
            }

            this.Scope.Leave(output);

            var methods = Methods.Values.Select(x => x.GetABI());
            var abi = new ContractInterface(methods);

            return abi;
        }

        public void AddMethod(int line, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, Scope scope, StatementBlock body)
        {
            if (Methods.Count == 0)
            {
                this.LineNumber = line;
            }

            var method = new MethodInterface(this.library, name, kind, returnType, parameters);
            this.Scope.Methods.Add(method);

            var decl = new MethodDeclaration(scope, method, body);
            decl.LineNumber = line;
            this.Methods[name] = decl;
        }

        public LibraryDeclaration FindLibrary(string name, bool required = true)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                if (required)
                {
                    throw new CompilerException("invalid library name: " + name);
                }
                else
                {
                    return null;
                }
            }

            if (Libraries.ContainsKey(name))
            {
                return Libraries[name];
            }

            if (required)
            {
                throw new CompilerException("possibly unimported library: " + name);
            }

            return null;
        }

        internal LibraryDeclaration AddLibrary(string name)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                throw new CompilerException("invalid library name: " + name);
            }

            var libDecl = new LibraryDeclaration(this.Scope, name);

            switch (name)
            {
                case "this":
                    libDecl.AddMethod("getAddress", VarKind.Address, new MethodParameter[] { });
                    libDecl.AddMethod("getMethod", VarKind.Method, new MethodParameter[] { });
                    break;

                case "Runtime":
                    libDecl.AddMethod("log", VarKind.None, new[] { new MethodParameter("message", VarKind.String) });
                    libDecl.AddMethod("isWitness", VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address) });
                    libDecl.AddMethod("getTime", VarKind.Timestamp, new MethodParameter[] { });
                    libDecl.AddMethod("startTask", VarKind.None, new MethodParameter[] { new MethodParameter("from", VarKind.Address), new MethodParameter("task", VarKind.Method) });
                    libDecl.AddMethod("stopTask", VarKind.None, new MethodParameter[] { });
                    break;

                case "Token":
                    libDecl.AddMethod("create", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("maxSupply", VarKind.Number), new MethodParameter("decimals", VarKind.Number), new MethodParameter("flags", VarKind.Number), new MethodParameter("script", VarKind.Bytes) });
                    libDecl.AddMethod("transfer", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                    libDecl.AddMethod("transferAll", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String) });
                    libDecl.AddMethod("mint", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                    libDecl.AddMethod("burn", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                    libDecl.AddMethod("swap", VarKind.None, new[] { new MethodParameter("targetChain", VarKind.String), new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                    libDecl.AddMethod("getBalance", VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String) });
                    break;

                case "Organization":
                    libDecl.AddMethod("create", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("id", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("script", VarKind.Bytes) });
                    libDecl.AddMethod("addMember", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("target", VarKind.Address)});
                    break;

                case "Utils":
                    libDecl.AddMethod("unixTime", VarKind.Timestamp, new[] { new MethodParameter("value", VarKind.Number) });
                    break;

                case "Leaderboard":
                    libDecl.AddMethod("create", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("capacity", VarKind.Number) });
                    libDecl.AddMethod("getAddress", VarKind.Address, new[] { new MethodParameter("index", VarKind.Number), new MethodParameter("boardName", VarKind.String) });
                    libDecl.AddMethod("getScore", VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String) });
                    libDecl.AddMethod("insertScore", VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("score", VarKind.Number)});
                    libDecl.AddMethod("reset", VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String)});
                    libDecl.AddMethod("getSize", VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String) });
                    break;

                case "Map":
                    libDecl.AddMethod("get", VarKind.Unknown, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown) });
                    libDecl.AddMethod("set", VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown), new MethodParameter("value", VarKind.Unknown) });
                    libDecl.AddMethod("remove", VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown) });
                    libDecl.AddMethod("count", VarKind.Number, new[] { new MethodParameter("map", VarKind.String) });
                    libDecl.AddMethod("clear", VarKind.None, new[] { new MethodParameter("map", VarKind.String) });
                    break;

                case "List":
                    libDecl.AddMethod("get", VarKind.Unknown, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) });
                    libDecl.AddMethod("add", VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("value", VarKind.Unknown) });
                    libDecl.AddMethod("replace", VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarKind.Unknown) });
                    libDecl.AddMethod("remove", VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number)});
                    libDecl.AddMethod("count", VarKind.Number, new[] { new MethodParameter("list", VarKind.String) });
                    libDecl.AddMethod("clear", VarKind.None, new[] { new MethodParameter("list", VarKind.String) });
                    break;

                default:
                    throw new CompilerException("unknown library: " + name);
            }

            Libraries[name] = libDecl;
            return libDecl;
        }   

        public void Compile(out string asm, out ContractInterface abi)
        {
            var sb = new CodeGenerator();
            abi = this.GenerateCode(sb);

            Parser.Instance.VerifyRegisters();

            asm = sb.ToString();
        }
    }
}

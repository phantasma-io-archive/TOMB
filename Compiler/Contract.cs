using Phantasma.Blockchain.Contracts;
using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
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
            this.library = new LibraryDeclaration(Scope, name);
        }

        public override void Visit(Action<Node> callback)
        {
            callback(this);

            foreach (var method in Methods.Values)
            {
                method.Visit(callback);
            }

            foreach (var lib in Libraries.Values)
            {
                lib.Visit(callback);
            }
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

            /*{
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
            }*/

            foreach (var entry in Methods.Values)
            {
                entry.GenerateCode(output);
            }

            this.Scope.Leave(output);

            var methods = Methods.Values.Select(x => x.GetABI());
            var abi = new ContractInterface(methods);

            return abi;
        }

        public MethodInterface AddMethod(int line, string name, MethodKind kind, VarKind returnType, MethodParameter[] parameters, Scope scope)
        {
            if (Methods.Count == 0)
            {
                this.LineNumber = line;
            }

            var method = new MethodInterface(this.library, MethodImplementationType.Custom, name, kind, returnType, parameters);
            this.Scope.Methods.Add(method);

            var decl = new MethodDeclaration(scope, method);
            decl.LineNumber = line;
            this.Methods[name] = decl;

            return method;
        }

        public void SetMethodBody(string name, StatementBlock body)
        {
            if (this.Methods.ContainsKey(name))
            {
                this.Methods[name].body = body;
            }
            else
            {
                throw new System.Exception("Cannot set body for unknown method: " + name);
            }

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
                case "Runtime":
                    libDecl.AddMethod("log", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("message", VarKind.String) });
                    libDecl.AddMethod("expect", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("condition", VarKind.Bool), new MethodParameter("error", VarKind.String) });
                    libDecl.AddMethod("isWitness", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address) });
                    libDecl.AddMethod("isTrigger", MethodImplementationType.ExtCall, VarKind.Bool, new MethodParameter[] { });
                    libDecl.AddMethod("time", MethodImplementationType.ExtCall, VarKind.Timestamp, new MethodParameter[] { });
                    libDecl.AddMethod("transactionHash", MethodImplementationType.ExtCall, VarKind.Hash, new MethodParameter[] { });
                    libDecl.AddMethod("startTask", MethodImplementationType.ExtCall, VarKind.None, new MethodParameter[] { new MethodParameter("from", VarKind.Address), new MethodParameter("task", VarKind.Method) });
                    libDecl.AddMethod("stopTask", MethodImplementationType.ExtCall, VarKind.None, new MethodParameter[] { });
                    break;

                case "Token":
                    libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("maxSupply", VarKind.Number), new MethodParameter("decimals", VarKind.Number), new MethodParameter("flags", VarKind.Number), new MethodParameter("script", VarKind.Bytes) });
                    libDecl.AddMethod("transfer", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.TransferTokens");
                    libDecl.AddMethod("transferAll", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.TransferBalance");
                    libDecl.AddMethod("mint", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.MintTokens");
                    libDecl.AddMethod("burn", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.BurnTokens");
                    //libDecl.AddMethod("swap", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("targetChain", VarKind.String), new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                    libDecl.AddMethod("getBalance", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String) });
                    break;

                case "Organization":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("id", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("script", VarKind.Bytes) });
                        libDecl.AddMethod("addMember", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("target", VarKind.Address) });
                        break;
                    }

                case "Oracle":
                    {
                        libDecl.AddMethod("read", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("url", VarKind.String) });
                        libDecl.AddMethod("price", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("quote", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("amount", VarKind.Number) });
                        break;
                    }

                case "Utils":
                    libDecl.AddMethod("unixTime", MethodImplementationType.Custom, VarKind.Timestamp, new[] { new MethodParameter("value", VarKind.Number) }).SetPostCallback((output, scope, method, reg) =>
                    {
                        var nameExpr = method.arguments[0] as LiteralExpression;
                        if (nameExpr != null && nameExpr.kind == VarKind.Number)
                        {
                            var timestamp = uint.Parse(nameExpr.value);
                            output.AppendLine(method, $"LOAD {reg} {timestamp}");
                            this.CallNecessaryConstructors(output, VarKind.Timestamp, reg);
                            return reg;
                        }
                        else
                        {
                            throw new Exception("Expected literal number expression");
                        }
                    });

                    libDecl.AddMethod("contractAddress", MethodImplementationType.Custom, VarKind.Address, new[] { new MethodParameter("name", VarKind.String) }).SetPostCallback((output, scope, method, reg) =>
                    {
                        var nameExpr = method.arguments[0] as LiteralExpression;
                        if (nameExpr != null && nameExpr.kind == VarKind.String)
                        {
                            var address = SmartContract.GetAddressForName(nameExpr.value);
                            var hex = Base16.Encode(address.ToByteArray());
                            output.AppendLine(method, $"LOAD {reg} 0x{hex}");
                            return reg;
                        }
                        else
                        {
                            throw new Exception("Expected literal string expression");
                        }
                    });
                    break;

                case "Leaderboard":
                    {
                        var contract = NativeContractKind.Ranking.ToString();
                        libDecl.AddMethod("create", MethodImplementationType.Contract, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("capacity", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.CreateLeaderboard));
                        libDecl.AddMethod("getAddress", MethodImplementationType.Contract, VarKind.Address, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetAddressByIndex));
                        libDecl.AddMethod("getScoreByIndex", MethodImplementationType.Contract, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByIndex));
                        libDecl.AddMethod("getScoreByAddress", MethodImplementationType.Contract, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByAddress));
                        libDecl.AddMethod("getSize", MethodImplementationType.Contract, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.GetSize));
                        libDecl.AddMethod("insert", MethodImplementationType.Contract, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("score", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.InsertScore));
                        libDecl.AddMethod("reset", MethodImplementationType.Contract, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.ResetLeaderboard));
                        break;
                    }

                case "Map":
                    libDecl.AddMethod("get", MethodImplementationType.ExtCall, VarKind.Unknown, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown) }).SetParameterCallback("map", ConvertFieldToKey)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Parser.Instance.AllocRegister(output, this);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback((output, scope, expr, reg) =>
                         {
                             expr.CallNecessaryConstructors(output, expr.method.ReturnType, reg);
                             return reg;
                         });
                    libDecl.AddMethod("set", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown), new MethodParameter("value", VarKind.Unknown) }).SetParameterCallback("map", ConvertFieldToKey);
                    libDecl.AddMethod("remove", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Unknown) }).SetParameterCallback("map", ConvertFieldToKey);
                    libDecl.AddMethod("count", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToKey);
                    libDecl.AddMethod("clear", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToKey);
                    break;

                case "List":
                    libDecl.AddMethod("get", MethodImplementationType.Custom, VarKind.Unknown, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) });
                    libDecl.AddMethod("add", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("value", VarKind.Unknown) });
                    libDecl.AddMethod("replace", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarKind.Unknown) });
                    libDecl.AddMethod("remove", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number)});
                    libDecl.AddMethod("count", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("list", VarKind.String) });
                    libDecl.AddMethod("clear", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String) });
                    break;

                default:
                    throw new CompilerException("unknown library: " + name);
            }

            Libraries[name] = libDecl;
            return libDecl;
        }   

        private Register ConvertFieldToKey(CodeGenerator output, Scope scope, Expression expression)
        {
            var literal = expression as LiteralExpression;
            if (literal == null)
            {
                throw new System.Exception("expected literal expression for field key");
            }

            var key = SmartContract.GetKeyForField(scope.Root.Name, literal.value, false);
            var hex = Base16.Encode(key);

            var reg = Parser.Instance.AllocRegister(output, this);
            output.AppendLine(expression, $"LOAD {reg} 0x{hex} // field key");

            return reg;
        }

        public void Compile(string fileName, out byte[] script, out string asm, out ContractInterface abi, out DebugInfo debugInfo)
        {
            var sb = new CodeGenerator();
            abi = this.GenerateCode(sb);

            Parser.Instance.VerifyRegisters();

            asm = sb.ToString();

            var lines = asm.Split('\n');
            script = AssemblerUtils.BuildScript(lines, fileName, out debugInfo);

            lines = AssemblerUtils.CommentOffsets(lines, debugInfo).ToArray();

            // here we lookup the script start offset for each method based on debug info obtained from the assembler
            foreach (var abiMethod in abi.Methods)
            {
                var method = this.Methods[abiMethod.name];
                abiMethod.offset = debugInfo.FindOffset(method.@interface.StartAsmLine);                

                if (abiMethod.offset < 0)
                {
                    throw new Exception("Could not calculate script offset for method: " + abiMethod.name);
                }
            }

            asm = string.Join('\n', lines);
        }
    }
}

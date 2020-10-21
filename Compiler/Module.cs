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
    public abstract class Module: Node
    {
        public readonly string Name;

        public readonly bool Hidden;
        public Scope Scope { get; }

        public readonly Dictionary<string, LibraryDeclaration> Libraries = new Dictionary<string, LibraryDeclaration>();

        public readonly LibraryDeclaration library;

        // only available after compilation
        public byte[] script { get; private set; }
        public string asm { get; private set; }
        public ContractInterface abi { get; private set; }
        public DebugInfo debugInfo { get; private set; }

        public Module(string name, bool hidden)
        {
            this.Name = name;
            this.Hidden = hidden;
            this.Scope = new Scope(this);
            this.library = new LibraryDeclaration(Scope, "this");
            this.Libraries[library.Name] = library;
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

        public static string[] AvailableLibraries = new[] { "Call", "Runtime", "Token", "Organization", "Oracle", "Storage", "Utils", "Leaderboard", "Map", "List" };

        public static LibraryDeclaration LoadLibrary(string name, Scope scope)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                throw new CompilerException("invalid library name: " + name);
            }

            var libDecl = new LibraryDeclaration(scope, name);

            switch (name)
            {
                case "Call":
                    libDecl.AddMethod("interop", MethodImplementationType.ExtCall, VarKind.Any, new[] { new MethodParameter("...", VarKind.Generic) });
                    libDecl.AddMethod("contract", MethodImplementationType.ContractCall, VarKind.Any, new[] { new MethodParameter("...", VarKind.Generic) });
                    break;

                case "Runtime":
                    libDecl.AddMethod("expect", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("condition", VarKind.Bool), new MethodParameter("error", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"JMPIF {reg} @expect_{expr.NodeID}");

                            var msg = expr.arguments[1].AsStringLiteral();

                            output.AppendLine(expr, $"THROW {msg}");

                            output.AppendLine(expr, $"@expect_{expr.NodeID}: NOP");
                            return reg;
                        });
                    libDecl.AddMethod("log", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("message", VarKind.String) });
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

                case "Storage":
                    {
                        libDecl.AddMethod("read", MethodImplementationType.ExtCall, VarKind.Any, new[] { new MethodParameter("contract", VarKind.String), new MethodParameter("field", VarKind.String), new MethodParameter("type", VarKind.Number) }).SetAlias("Data.Get");
                        libDecl.AddMethod("write", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("field", VarKind.String), new MethodParameter("value", VarKind.Any) }).SetAlias("Data.Set");
                        libDecl.AddMethod("delete", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("field", VarKind.String) }).SetAlias("Data.Delete");
                        break;
                    }

                case "Utils":
                    libDecl.AddMethod("unixTime", MethodImplementationType.Custom, VarKind.Timestamp, new[] { new MethodParameter("value", VarKind.Number) }).SetPostCallback((output, scope, method, reg) =>
                    {
                        var nameExpr = method.arguments[0] as LiteralExpression;
                        if (nameExpr != null && nameExpr.type.Kind == VarKind.Number)
                        {
                            var timestamp = uint.Parse(nameExpr.value);
                            output.AppendLine(method, $"LOAD {reg} {timestamp}");
                            method.CallNecessaryConstructors(output, VarKind.Timestamp, reg);
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
                        if (nameExpr != null && nameExpr.type.Kind == VarKind.String)
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
                        libDecl.AddMethod("create", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("capacity", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.CreateLeaderboard));
                        libDecl.AddMethod("getAddress", MethodImplementationType.ContractCall, VarKind.Address, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetAddressByIndex));
                        libDecl.AddMethod("getScoreByIndex", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByIndex));
                        libDecl.AddMethod("getScoreByAddress", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByAddress));
                        libDecl.AddMethod("getSize", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.GetSize));
                        libDecl.AddMethod("insert", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("score", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.InsertScore));
                        libDecl.AddMethod("reset", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.ResetLeaderboard));
                        break;
                    }

                case "Map":
                    libDecl.AddMethod("get", MethodImplementationType.ExtCall, VarKind.Generic, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Generic) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback((output, scope, expr, reg) =>
                        {
                            expr.CallNecessaryConstructors(output, expr.method.ReturnType, reg);
                            return reg;
                        });
                    libDecl.AddMethod("set", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Generic), new MethodParameter("value", VarKind.Generic) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("remove", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarKind.Generic) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("clear", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("count", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead);
                    break;

                case "List":
                    libDecl.AddMethod("get", MethodImplementationType.Custom, VarKind.Generic, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) });
                    libDecl.AddMethod("add", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("value", VarKind.Generic) });
                    libDecl.AddMethod("replace", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarKind.Generic) });
                    libDecl.AddMethod("remove", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) });
                    libDecl.AddMethod("count", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("list", VarKind.String) });
                    libDecl.AddMethod("clear", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String) });
                    break;

                default:
                    throw new CompilerException("unknown library: " + name);
            }

            return libDecl;
        }

        private static Register ConvertFieldToStorageAccessRead(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToStorageAccess(output, scope, expression, true);
        }

        private static Register ConvertFieldToStorageAccessWrite(CodeGenerator output, Scope scope, Expression expression)
        {
            return ConvertFieldToStorageAccess(output, scope, expression, false);
        }

        private static Register ConvertFieldToStorageAccess(CodeGenerator output, Scope scope, Expression expression, bool insertContract)
        {
            var literal = expression as LiteralExpression;
            if (literal == null)
            {
                throw new System.Exception("expected literal expression for field key");
            }

            var reg = Compiler.Instance.AllocRegister(output, expression);

            output.AppendLine(expression, $"LOAD {reg} {literal.value} // field name");

            if (insertContract)
            {
                output.AppendLine(expression, $"PUSH {reg}");
                output.AppendLine(expression, $"LOAD {reg} \"{scope.Root.Name}\" // contract name");
            }

            return reg;
        }

        public abstract ContractInterface GenerateCode(CodeGenerator output);


        protected virtual void ProcessABI(ContractInterface abi, DebugInfo debugInfo)
        {
            // do nothing
        }

        public void Compile()
        {
            var sb = new CodeGenerator();
            abi = this.GenerateCode(sb);

            Compiler.Instance.VerifyRegisters();

            asm = sb.ToString();

            var lines = asm.Split('\n');
            DebugInfo temp;
            script = AssemblerUtils.BuildScript(lines, this.Name, out temp);
            this.debugInfo = temp;

            lines = AssemblerUtils.CommentOffsets(lines, this.debugInfo).ToArray();

            ProcessABI(abi, this.debugInfo);

            asm = string.Join('\n', lines);
        }
    }
}

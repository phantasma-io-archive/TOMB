using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;
using Phantasma.CodeGen.Assembler;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Tomb.Compiler
{
    public enum ModuleKind
    {
        Contract,
        Script,
        Description,
        Token,
        Account,
        Organization,
        NFT,
        Test,
    }

    public abstract class Module: Node
    {
        public readonly string Name;

        public readonly ModuleKind Kind;
        public Scope Scope { get; }

        public readonly Module Parent;

        public readonly Dictionary<string, LibraryDeclaration> Libraries = new Dictionary<string, LibraryDeclaration>();

        public readonly LibraryDeclaration library;

        // only available after compilation
        public byte[] script { get; private set; }
        public string asm { get; private set; }
        public ContractInterface abi { get; private set; }
        public DebugInfo debugInfo { get; private set; }

        private List<Module> _subModules = new List<Module>();
        public IEnumerable<Module> SubModules => _subModules;

        public Module(string name, ModuleKind kind, Module parent = null)
        {
            this.Name = name;
            this.Kind = kind;
            this.Parent = parent;
            this.Scope = new Scope(this);
            this.library = new LibraryDeclaration(Scope, "this");
            this.Libraries[library.Name] = library;

            ImportLibrary("String");
            ImportLibrary("Decimal");
            ImportLibrary("Enum");
        }

        public abstract MethodDeclaration FindMethod(string name);

        public void AddSubModule(Module subModule)
        {
            _subModules.Add(subModule);
        }

        public Module FindModule(string name)
        {
            foreach (var module in _subModules)
            {
                if (module.Name == name)
                {
                    return module;
                }
            }

            return null;
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

        public void ImportLibrary(string name)
        {
            var lib = LoadLibrary(name, this.Scope, this.Kind);
            Libraries[lib.Name] = lib;
        }

        public static string[] AvailableLibraries = new[] {
            "Call", "Runtime", "Token", "NFT", "Organization", "Oracle", "Storage", "Utils", "Leaderboard", "Market",
            "Time", "Task", "UID", "Map", "List", "String", "Decimal", "Enum", "Address", "Module", FormatLibraryName };

        public const string FormatLibraryName = "Format";

        public static LibraryDeclaration LoadLibrary(string name, Scope scope, ModuleKind moduleKind)
        {
            if (name != name.UppercaseFirst() && name != "this")
            {
                throw new CompilerException("invalid library name: " + name);
            }

            var libDecl = new LibraryDeclaration(scope, name);

            VarKind libKind;
            if (Enum.TryParse<VarKind>(name, out libKind) && libKind != VarKind.Bytes && libKind != VarKind.Method && libKind != VarKind.Task)
            {
                switch (libKind)
                {
                    case VarKind.Decimal:
                    case VarKind.Struct:
                    case VarKind.Module:
                        libKind = VarKind.Any;
                        break;
                }

                libDecl.AddMethod("toBytes", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", libKind) }).
                SetPreCallback((output, scope, expr) =>
                {
                    var reg = expr.arguments[0].GenerateCode(output);
                    output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.Bytes}");
                    return reg;
                });
            }

            switch (name)
            {
                case "Module":
                    // TODO implementations of those
                    libDecl.AddMethod("script", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Module) });
                    libDecl.AddMethod("abi", MethodImplementationType.Custom, VarKind.Bytes, new[] { new MethodParameter("target", VarKind.Module) });
                    return libDecl;

                case "Struct":
                    libDecl.AddMethod("fromBytes", MethodImplementationType.Custom, VarKind.Struct, new[] { new MethodParameter("source", VarKind.Bytes) });
                    return libDecl;

                case "String":
                    libDecl.AddMethod("length", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("target", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"SIZE {reg} {reg}");
                            return reg;
                        });
                    return libDecl;

                case "Enum":
                    libDecl.AddMethod("isSet", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Enum), new MethodParameter("flag", VarKind.Enum) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var regA = expr.arguments[0].GenerateCode(output);
                            var regB = expr.arguments[1].GenerateCode(output);

                            output.AppendLine(expr, $"AND {regA} {regB} {regA}");

                            Compiler.Instance.DeallocRegister(ref regB);
                            return regA;
                        });
                    return libDecl;

                case "Decimal":
                    libDecl.AddMethod("decimals", MethodImplementationType.Custom, VarKind.Number, new[] { new MethodParameter("target", VarKind.Any) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = Compiler.Instance.AllocRegister(output, expr);
                            var arg = expr.arguments[0];
                            var decType = (DecimalVarType)arg.ResultType;
                            output.AppendLine(expr, $"LOAD {reg} {decType.decimals}");
                            return reg;
                        });
                    return libDecl;

                case "Address":
                    // TODO implementations of those
                    libDecl.AddMethod("isNull", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isUser", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isSystem", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    libDecl.AddMethod("isInterop", MethodImplementationType.Custom, VarKind.Bool, new[] { new MethodParameter("target", VarKind.Address) });
                    return libDecl;
            }

            if (moduleKind == ModuleKind.Description)
            {
                switch (name)
                {
                    case FormatLibraryName:
                        libDecl.AddMethod("decimals", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("value", VarKind.Number), new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("symbol", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("symbol", VarKind.String) });
                        libDecl.AddMethod("account", MethodImplementationType.ExtCall, VarKind.String, new[] { new MethodParameter("address", VarKind.Address) });
                        break;

                    default:
                        throw new CompilerException("unknown library: " + name);
                }

                return libDecl;
            }

            switch (name)
            {
                case "Call":
                    libDecl.AddMethod("interop", MethodImplementationType.ExtCall, VarKind.Any, new[] { new MethodParameter("method", VarKind.String), new MethodParameter("...", VarKind.Any) });
                    libDecl.AddMethod("contract", MethodImplementationType.ContractCall, VarKind.Any, new[] { new MethodParameter("contract", VarKind.String), new MethodParameter("method", VarKind.String), new MethodParameter("...", VarKind.Any) });
                    libDecl.AddMethod("method", MethodImplementationType.Custom, VarKind.Any, new[] { new MethodParameter("method", VarKind.Method), new MethodParameter("...", VarKind.Any) });
                    break;

                case "Chain":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("source", VarKind.Address), new MethodParameter("organization", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("parentName", VarKind.String) }).SetAlias("Nexus.CreateChain");
                        break;
                    }

                case "Cryptography":
                    {
                        libDecl.AddMethod("AESDecrypt", MethodImplementationType.ExtCall, VarKind.Bytes, new[] { new MethodParameter("data", VarKind.Bytes), new MethodParameter("key", VarKind.Bytes) }).SetAlias("Runtime.AESDecrypt");
                        libDecl.AddMethod("AESEncrypt", MethodImplementationType.ExtCall, VarKind.Bytes, new[] { new MethodParameter("data", VarKind.Bytes), new MethodParameter("key", VarKind.Bytes) }).SetAlias("Runtime.AESEncrypt");
                        break;
                    }

                case "Platform":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("source", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("externalAddress", VarKind.String), new MethodParameter("interopAddress", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Nexus.CreatePlatform");
                        libDecl.AddMethod("setTokenHash", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("platform", VarKind.String), new MethodParameter("hash", VarKind.Bytes) }).SetAlias("Nexus.SetTokenPlatformHash");
                        break;
                    }

                case "Runtime":
                    libDecl.AddMethod("expect", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("condition", VarKind.Bool), new MethodParameter("error", VarKind.String) }).
                        SetPreCallback((output, scope, expr) =>
                        {
                            var reg = expr.arguments[0].GenerateCode(output);
                            output.AppendLine(expr, $"JMPIF {reg} @expect_{expr.NodeID}");

                            var reg2 = expr.arguments[1].GenerateCode(output);
                            output.AppendLine(expr, $"THROW {reg2}");

                            Compiler.Instance.DeallocRegister(ref reg2);

                            output.AppendLine(expr, $"@expect_{expr.NodeID}: NOP");
                            return reg;
                        });
                    libDecl.AddMethod("log", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("message", VarKind.String) });
                    libDecl.AddMethod("isWitness", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address) });
                    libDecl.AddMethod("isTrigger", MethodImplementationType.ExtCall, VarKind.Bool, new MethodParameter[] { });
                    libDecl.AddMethod("transactionHash", MethodImplementationType.ExtCall, VarKind.Hash, new MethodParameter[] { });
                    libDecl.AddMethod("deployContract", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("contract", VarKind.Module)}).SetParameterCallback("contract", ConvertFieldToContract);
                    libDecl.AddMethod("upgradeContract", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("name", VarKind.String), new MethodParameter("contract", VarKind.Module)}).SetParameterCallback("contract", ConvertFieldToContract);
                    libDecl.AddMethod("gasTarget", MethodImplementationType.ExtCall, VarKind.Address, new MethodParameter[] {  }).SetAlias("Runtime.GasTarget");
                    libDecl.AddMethod("context", MethodImplementationType.ExtCall, VarKind.String, new MethodParameter[] { }).SetAlias("Runtime.Context");
                    break;

                case "Task":
                    libDecl.AddMethod("start", MethodImplementationType.ExtCall, VarKind.Task, new MethodParameter[] { new MethodParameter("method", VarKind.Method), new MethodParameter("from", VarKind.Address), new MethodParameter("frequency", VarKind.Number), new MethodParameter("mode", VarType.Find(VarKind.Enum, "TaskMode")), new MethodParameter("gasLimit", VarKind.Number) }).SetAlias("Task.Start");
                    libDecl.AddMethod("stop", MethodImplementationType.ExtCall, VarKind.None, new MethodParameter[] { new MethodParameter("task", VarKind.Address) }).SetAlias("Task.Stop");
                    libDecl.AddMethod("current", MethodImplementationType.ExtCall, VarKind.Task, new MethodParameter[] { }).SetAlias("Task.Current");
                    break;


                case "Time":
                    libDecl.AddMethod("now", MethodImplementationType.ExtCall, VarKind.Timestamp, new MethodParameter[] { }).SetAlias("Runtime.Time");
                    libDecl.AddMethod("unix", MethodImplementationType.Custom, VarKind.Timestamp, new[] { new MethodParameter("value", VarKind.Number) }).SetPostCallback((output, scope, method, reg) =>
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
                    break;

                case "UID":
                    {
                        libDecl.AddMethod("generate", MethodImplementationType.ExtCall, VarKind.Number, new MethodParameter[] { }).SetAlias("Runtime.GenerateUID");
                        break;
                    }


                case "Random":
                    libDecl.AddMethod("generate", MethodImplementationType.ExtCall, VarKind.Number, new MethodParameter[] { }).SetAlias("Runtime.Random");
                    libDecl.AddMethod("seed", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("seed", VarKind.Number) }).SetAlias("Runtime.SetSeed");
                    break;

                case "Token":
                    libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("maxSupply", VarKind.Number), new MethodParameter("decimals", VarKind.Number), new MethodParameter("flags", VarKind.Number), new MethodParameter("script", VarKind.Bytes) }).SetAlias("Nexus.CreateToken");
                    libDecl.AddMethod("exists", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.TokenExists");
                    libDecl.AddMethod("getDecimals", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetTokenDecimals");
                    libDecl.AddMethod("getFlags", MethodImplementationType.ExtCall, VarType.Find(VarKind.Enum, "TokenFlag"), new[] { new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetTokenFlags");
                    libDecl.AddMethod("transfer", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.TransferTokens");
                    libDecl.AddMethod("transferAll", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.TransferBalance");
                    libDecl.AddMethod("mint", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.MintTokens");
                    libDecl.AddMethod("burn", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.BurnTokens");
                    libDecl.AddMethod("swap", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("targetChain", VarKind.String), new MethodParameter("source", VarKind.Address), new MethodParameter("destination", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("amount", VarKind.Number) }).SetAlias("Runtime.SwapTokens");
                    libDecl.AddMethod("getBalance", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.GetBalance");
                    libDecl.AddMethod("isMinter", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("address", VarKind.Address), new MethodParameter("symbol", VarKind.String) }).SetAlias("Runtime.IsMinter");
                    break;

                case "NFT":
                    libDecl.AddMethod("transfer", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.TransferToken");
                    libDecl.AddMethod("mint", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("to", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("rom", VarKind.Any), new MethodParameter("ram", VarKind.Any), new MethodParameter("seriesID", VarKind.Any) })
                        .SetParameterCallback("rom", ConvertFieldToBytes).SetParameterCallback("ram", ConvertFieldToBytes).SetAlias("Runtime.MintToken");
                    libDecl.AddMethod("readROM", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.ReadTokenROM").SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("readRAM", MethodImplementationType.ExtCall, VarType.Generic(0), new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.ReadTokenRAM").SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("burn", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) }).SetAlias("Runtime.BurnToken");
                    libDecl.AddMethod("infuse", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("id", VarKind.Number) , new MethodParameter("infuseSymbol", VarKind.String), new MethodParameter("infuseValue", VarKind.Number) }).SetAlias("Runtime.InfuseToken");
                    libDecl.AddMethod("createSeries", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("seriesID", VarKind.Number), new MethodParameter("maxSupply", VarKind.Number), new MethodParameter("mode", VarType.Find(VarKind.Enum, "TokenSeries")), new MethodParameter("nft", VarKind.Module) }).
                        SetAlias("Nexus.CreateTokenSeries").SetParameterCallback("nft", ConvertFieldToContract);
                    break;

                case "Organization":
                    {
                        libDecl.AddMethod("create", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("id", VarKind.String), new MethodParameter("name", VarKind.String), new MethodParameter("script", VarKind.Bytes) }).SetAlias("Nexus.CreateOrganization");
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
                        var contract = NativeContractKind.Ranking.ToString().ToLower();
                        libDecl.AddMethod("create", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("capacity", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.CreateLeaderboard));
                        libDecl.AddMethod("getAddress", MethodImplementationType.ContractCall, VarKind.Address, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetAddressByIndex));
                        libDecl.AddMethod("getScoreByIndex", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("index", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByIndex));
                        libDecl.AddMethod("getScoreByAddress", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String), new MethodParameter("target", VarKind.Address) }).SetContract(contract).SetAlias(nameof(RankingContract.GetScoreByAddress));
                        libDecl.AddMethod("getSize", MethodImplementationType.ContractCall, VarKind.Number, new[] { new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.GetSize));
                        libDecl.AddMethod("insert", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("target", VarKind.Address), new MethodParameter("boardName", VarKind.String), new MethodParameter("score", VarKind.Number) }).SetContract(contract).SetAlias(nameof(RankingContract.InsertScore));
                        libDecl.AddMethod("reset", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("boardName", VarKind.String) }).SetContract(contract).SetAlias(nameof(RankingContract.ResetLeaderboard));
                        break;
                    }

                case "Market":
                    {
                        var contract = NativeContractKind.Market.ToString().ToLower();
                        libDecl.AddMethod("sell", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("baseSymbol", VarKind.String), new MethodParameter("quoteSymbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number), new MethodParameter("price", VarKind.Number), new MethodParameter("endDate", VarKind.Timestamp) }).SetContract(contract).SetAlias(nameof(MarketContract.SellToken));
                        libDecl.AddMethod("buy", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("from", VarKind.Address), new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.BuyToken));
                        libDecl.AddMethod("cancel", MethodImplementationType.ContractCall, VarKind.None, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.CancelSale));
                        libDecl.AddMethod("hasAuction", MethodImplementationType.ContractCall, VarKind.Bool, new[] { new MethodParameter("symbol", VarKind.String), new MethodParameter("tokenID", VarKind.Number) }).SetContract(contract).SetAlias(nameof(MarketContract.HasAuction));
                        break;
                    }

                case "Map":
                    libDecl.AddMethod("get", MethodImplementationType.ExtCall, VarType.Generic(1), new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead)
                        .SetPreCallback((output, scope, expr) =>
                        {
                            var vmType = MethodInterface.ConvertType(expr.method.ReturnType);
                            var reg = Compiler.Instance.AllocRegister(output, expr);

                            output.AppendLine(expr, $"LOAD {reg} {(int)vmType} // field type");
                            output.AppendLine(expr, $"PUSH {reg}");

                            return reg;
                        })
                        .SetPostCallback(ConvertGenericResult);
                    libDecl.AddMethod("set", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)), new MethodParameter("value", VarType.Generic(1)) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("remove", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("clear", MethodImplementationType.ExtCall, VarKind.None, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    libDecl.AddMethod("count", MethodImplementationType.ExtCall, VarKind.Number, new[] { new MethodParameter("map", VarKind.String) }).SetParameterCallback("map", ConvertFieldToStorageAccessRead);
                    libDecl.AddMethod("has", MethodImplementationType.ExtCall, VarKind.Bool, new[] { new MethodParameter("map", VarKind.String), new MethodParameter("key", VarType.Generic(0)) }).SetParameterCallback("map", ConvertFieldToStorageAccessWrite);
                    break;

                case "List":
                    libDecl.AddMethod("get", MethodImplementationType.Custom, VarType.Generic(0), new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number) });
                    libDecl.AddMethod("add", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("value", VarType.Generic(0)) });
                    libDecl.AddMethod("replace", MethodImplementationType.Custom, VarKind.None, new[] { new MethodParameter("list", VarKind.String), new MethodParameter("index", VarKind.Number), new MethodParameter("value", VarType.Generic(0)) });
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
                output.AppendLine(expression, $"LOAD {reg} \"{scope.Module.Name}\" // contract name");
            }

            return reg;
        }

        private static Register ConvertFieldToBytes(CodeGenerator output, Scope scope, Expression expr)
        {
            var reg = expr.GenerateCode(output);
            output.AppendLine(expr, $"CAST {reg} {reg} #{VMType.Bytes}");
            return reg;
        }

        private static Register ConvertFieldToContract(CodeGenerator output, Scope scope, Expression expression)
        {
            var literal = expression as LiteralExpression;
            if (literal == null)
            {
                throw new CompilerException("nft argument is not a literal value");
            }

            var module = scope.Module.FindModule(literal.value);

            var abi = module.abi;

            if (module.Kind == ModuleKind.NFT)
            {
                var nftStandard = NFTUtils.GetNFTStandard();
                if (!abi.Implements(nftStandard))
                {
                    throw new CompilerException($"nft {literal.value} not does implement NFT standard");
                }
            }

            var reg = Compiler.Instance.AllocRegister(output, expression);

            var abiBytes = module.abi.ToByteArray();

            output.AppendLine(expression, $"LOAD {reg} 0x{Base16.Encode(abiBytes)} // abi");
            output.AppendLine(expression, $"PUSH {reg}");

            output.AppendLine(expression, $"LOAD {reg} 0x{Base16.Encode(module.script)} // script");
//            output.AppendLine(expression, $"PUSH {reg}");

            return reg;
        }

        private static Register ConvertGenericResult(CodeGenerator output, Scope scope, MethodExpression method, Register reg) 
        {
            if (method.ResultType.IsWeird)
            {
                throw new CompilerException($"possible compiler bug detected on call to method {method.method.Name}");
            }

            switch (method.ResultType.Kind)
            {
                case VarKind.Bytes:
                    break;

                case VarKind.Struct:
                    output.AppendLine(method, $"UNPACK {reg} {reg}");
                    break;

                default:
                    method.CallNecessaryConstructors(output, method.ResultType, reg);
                    break;
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
            foreach (var subModule in this.SubModules)
            {
                subModule.Compile();
            }

            var sb = new CodeGenerator();
            abi = this.GenerateCode(sb);

            Compiler.Instance.VerifyRegisters();

            asm = sb.ToString();

            var lines = asm.Split('\n');
            DebugInfo temp;
            Dictionary<string, int> labels;

            script = AssemblerUtils.BuildScript(lines, this.Name, out temp, out labels);
            this.debugInfo = temp;

            lines = AssemblerUtils.CommentOffsets(lines, this.debugInfo).ToArray();

            ProcessABI(abi, this.debugInfo);

            asm = string.Join('\n', lines);
        }
    }
}

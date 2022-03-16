using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.AST;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb
{
    public enum ExecutionResult
    {
        Break,
        Yield,
    }

    public class Compiler
    {
        private List<LexerToken> tokens;
        private int tokenIndex = 0;

        private int currentLabel = 0;

        public int CurrentLine { get; private set; }
        public int CurrentColumn { get; private set; }

        private string[] lines;

        public static Compiler Instance { get; private set; }

        public readonly int TargetProtocolVersion;

        public Compiler(int version)
        {
            TargetProtocolVersion = version;
            Instance = this;
        }

        private void InitEnums()
        {
            _enums.Clear();
            CreateEnum<TokenFlags>("TokenFlags");
            CreateEnum<TaskFrequencyMode>("TaskMode");
            CreateEnum<TokenSeriesMode>("TokenSeries");
        }

        private void CreateEnum<T>(string name)
        {
            CreateEnum(name, typeof(T));
        }

        private void CreateEnum(string enumName, Type enumType)
        {
            var tokenFlagsNames = Enum.GetNames(enumType).Cast<string>().ToArray();
            var tokenFlagsEntries = new List<EnumEntry>();
            foreach (var name in tokenFlagsNames)
            {
                var temp = Enum.Parse(enumType, name);
                var value = Convert.ToUInt32(temp);
                tokenFlagsEntries.Add(new EnumEntry(name, value));
            }
            var tokenFlagsDecl = new EnumDeclaration(enumName, tokenFlagsEntries);
            _enums[tokenFlagsDecl.Name] = tokenFlagsDecl;
        }

        private void InitStructs()
        {
            _structs.Clear();

            CreateStruct("NFT", new[]
            {
                new StructField("chain", VarKind.Address),
                new StructField("owner", VarKind.Address),
                new StructField("creator", VarKind.Address),
                new StructField("ROM", VarKind.Bytes),
                new StructField("RAM", VarKind.Bytes),
                new StructField("seriesID", VarKind.Number),
                new StructField("mintID", VarKind.Number),
            });
        }

        private void CreateStruct(string structName, IEnumerable<StructField> fields)
        {
            var structType = (StructVarType)VarType.Find(VarKind.Struct, structName);
            structType.decl = new StructDeclaration(structName, fields);

            _structs[structName] = structType.decl;
        }

        private void Rewind(int steps = 1)
        {
            tokenIndex -= steps;
            if (tokenIndex < 0)
            {
                throw new CompilerException("unexpected rewind");
            }
        }

        public int AllocateLabel()
        {
            currentLabel++;
            return currentLabel;
        }

        private bool HasTokens()
        {
            return tokenIndex < tokens.Count;
        }

        private LexerToken FetchToken()
        {
            if (tokenIndex >= tokens.Count)
            {
                throw new CompilerException("unexpected end of file");
            }

            var token = tokens[tokenIndex];
            tokenIndex++;

            this.CurrentLine = token.line;
            this.CurrentColumn = token.column;

            //Console.WriteLine(token);
            return token;
        }

        private void ExpectToken(string val, string msg = null)
        {
            var token = FetchToken();

            if (token.value != val)
            {
                throw new CompilerException(msg != null ? msg : ("expected " + val));
            }
        }

        private string ExpectKind(TokenKind expectedKind)
        {
            var token = FetchToken();

            if (token.kind != expectedKind)
            {
                throw new CompilerException($"expected {expectedKind}, got {token.kind} instead");
            }

            return token.value;
        }

        private string ExpectIdentifier()
        {
            return ExpectKind(TokenKind.Identifier);
        }

        private string[] ExpectAsm()
        {
            return ExpectKind(TokenKind.Asm).Split('\n').Select(x => x.TrimStart()).ToArray();
        }

        private string ExpectString()
        {
            return ExpectKind(TokenKind.String);
        }

        private string ExpectNumber()
        {
            return ExpectKind(TokenKind.Number);
        }

        private string ExpectBool()
        {
            return ExpectKind(TokenKind.Bool);
        }

        private VarType ExpectType()
        {
            var token = FetchToken();

            if (token.kind != TokenKind.Type)
            {
                if (token.value.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return VarType.Find(VarKind.None);
                }

                if (_structs.ContainsKey(token.value))
                {
                    return VarType.Find(VarKind.Struct, token.value);
                }

                if (_enums.ContainsKey(token.value))
                {
                    return VarType.Find(VarKind.Enum, token.value);
                }

                throw new CompilerException($"expected type, got {token.kind} [{token.value}]");
            }

            if (token.value == "decimal")
            {
                return ParseDecimal();
            }

            if (token.value == "array")
            {
                return ParseArray();
            }

            var kind = (VarKind)Enum.Parse(typeof(VarKind), token.value, true);
            return VarType.Find(kind);
        }

        public string GetLine(int index)
        {
            if (index <= 0 || index > lines.Length)
            {
                return "";
            }

            return lines[index - 1];
        }

        private List<Module> _modules = new List<Module>();
        private Dictionary<string, StructDeclaration> _structs = new Dictionary<string, StructDeclaration>();
        private Dictionary<string, EnumDeclaration> _enums = new Dictionary<string, EnumDeclaration>();

        public Module FindModule(string name, bool mustBeCompiled)
        {
            foreach (var entry in _modules)
            {
                if (entry.Name == name)
                {
                    if (mustBeCompiled && entry.script == null)
                    {
                        return null;
                    }

                    return entry;
                }
            }

            return null;
        }

        public Module[] Process(string[] lines)
        {
            var sourceCode = string.Join('\n', lines);
            return Process(sourceCode);
        }

        public Module[] Process(string sourceCode)
        {
            this.tokens = Lexer.Process(sourceCode);

            /*foreach (var token in tokens)
            {
                Console.WriteLine(token);
            }*/

            this.lines = sourceCode.Replace("\r", "").Split('\n');

            _modules.Clear();

            InitEnums();
            InitStructs();

            while (HasTokens())
            {
                var firstToken = FetchToken();

                Module module;

                switch (firstToken.value)
                {
                    case "struct":
                        {
                            module = null;

                            var structName = ExpectIdentifier();

                            var fields = new List<StructField>();

                            ExpectToken("{");
                            do
                            {
                                var next = FetchToken();
                                if (next.value == "}")
                                {
                                    break;
                                }

                                Rewind();

                                var fieldName = ExpectIdentifier();
                                ExpectToken(":");

                                var fieldType = ExpectType();
                                ExpectToken(";");

                                fields.Add(new StructField(fieldName, fieldType));
                            } while (true);

                            CreateStruct(structName, fields);
                            break;
                        }

                    case "enum":
                        {
                            module = null;

                            var enumName = ExpectIdentifier();

                            var entries = new List<EnumEntry>();

                            ExpectToken("{");
                            do
                            {
                                var next = FetchToken();
                                if (next.value == "}")
                                {
                                    break;
                                }

                                Rewind();

                                if (entries.Count > 0)
                                {
                                    ExpectToken(",");
                                }

                                var entryName = ExpectIdentifier();

                                next = FetchToken();

                                string enumValueStr;

                                if (next.value == "=") {
                                    enumValueStr = ExpectNumber();
                                }
                                else
                                {
                                    enumValueStr = entries.Count.ToString();
                                    Rewind();
                                }


                                uint entryValue;
                                if (!uint.TryParse(enumValueStr, out entryValue))
                                {
                                    throw new CompilerException($"Invalid enum value for {entryName} => {enumValueStr}");
                                }

                                entries.Add(new EnumEntry(entryName, entryValue));
                            } while (true);


                            var enumType = (EnumVarType)VarType.Find(VarKind.Enum, enumName);
                            enumType.decl = new EnumDeclaration(enumName, entries);

                            _enums[enumName] = enumType.decl;

                            break;
                        }


                    case "contract":
                    case "token":
                        {
                            var contractName = ExpectIdentifier();

                            if (firstToken.value == "token")
                            {
                                if (!ValidationUtils.IsValidTicker(contractName))
                                {
                                    throw new CompilerException("token does not have a valid name: " + contractName);
                                }
                            }
                            else
                            {
                                if (!ValidationUtils.IsValidIdentifier(contractName))
                                {
                                    throw new CompilerException("contract does not have a valid name: " + contractName);
                                }
                            }

                            module = new Contract(contractName, firstToken.value == "token" ? ModuleKind.Token: ModuleKind.Contract);
                            ExpectToken("{");
                            ParseModule(module);
                            ExpectToken("}");
                            break;
                        }

                    case "script":
                    case "description":
                        {
                            var scriptName = ExpectIdentifier();

                            module = new Script(scriptName, firstToken.value == "description" ? ModuleKind.Description : ModuleKind.Script);

                            ExpectToken("{");
                            ParseModule(module);
                            ExpectToken("}");
                            break;
                        }

                    default:
                        throw new CompilerException("Unexpected token: " + firstToken.value);
                }

                if (module != null)
                {
                    module.Compile();
                    _modules.Add(module);
                }
            }

            return _modules.ToArray();
        }

        private void ParseModule(Module module)
        {
            var structLibName = "Struct";
            module.Libraries[structLibName] = Module.LoadLibrary(structLibName, null, ModuleKind.Script);
            var structLib = module.FindLibrary(structLibName);

            foreach (var structInfo in _structs.Values)
            {
                var args = new List<MethodParameter>();

                foreach (var field in structInfo.fields)
                {
                    args.Add(new MethodParameter(field.name, field.type));
                }

                structLib.AddMethod(structInfo.Name, MethodImplementationType.Custom, VarType.Find(VarKind.Struct, structInfo.Name), args.ToArray()).SetPreCallback(
                    (output, scope, expr) =>
                    {
                        var reg = Compiler.Instance.AllocRegister(output, expr);
                        var keyReg = Compiler.Instance.AllocRegister(output, expr);

                        output.AppendLine(expr, $"CLEAR {reg}");

                        int index = -1;
                        foreach (var field in structInfo.fields)
                        {
                            index++;
                            var argument = expr.arguments[index];

                            var exprReg = argument.GenerateCode(output);
                            output.AppendLine(expr, $"LOAD {keyReg} \"{field.name}\"");
                            output.AppendLine(expr, $"PUT {exprReg} {reg} {keyReg}");

                            Compiler.Instance.DeallocRegister(ref exprReg);
                        }

                        Compiler.Instance.DeallocRegister(ref keyReg);

                        return reg;

                    });
            }

            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return;

                    case "const":
                        {
                            var constName = ExpectIdentifier();
                            ExpectToken(":");
                            var type = ExpectType();
                            ExpectToken("=");

                            string constVal;

                            switch (type.Kind)
                            {
                                case VarKind.String:
                                    constVal = ExpectString();
                                    break;

                                case VarKind.Number:
                                    constVal = ExpectNumber();
                                    break;

                                case VarKind.Bool:
                                    constVal = ExpectBool();
                                    break;

                                default:
                                    constVal = ExpectIdentifier();
                                    break;
                            }

                            ExpectToken(";");

                            var constDecl = new ConstDeclaration(module.Scope, constName, type, constVal);
                            module.Scope.AddConstant(constDecl);
                            break;
                        }

                    case "global":
                        {                            
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var type = ExpectType();

                            VarDeclaration varDecl;

                            switch (type.Kind)
                            {
                                case VarKind.Storage_Map:
                                    {
                                        ExpectToken("<");
                                        var map_key = ExpectType();
                                        ExpectToken(",");
                                        var map_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new MapDeclaration(module.Scope, varName, map_key, map_val);
                                        break;
                                    }

                                case VarKind.Storage_List:
                                    {
                                        ExpectToken("<");
                                        var list_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new ListDeclaration(module.Scope, varName, list_val);
                                        break;
                                    }

                                case VarKind.Storage_Set:
                                    {
                                        ExpectToken("<");
                                        var set_val = ExpectType();
                                        ExpectToken(">");

                                        varDecl = new SetDeclaration(module.Scope, varName, set_val);
                                        break;
                                    }

                                default:
                                    {
                                        varDecl = new VarDeclaration(module.Scope, varName, type, VarStorage.Global);
                                        break;
                                    }
                            }

                            ExpectToken(";");
                            module.Scope.AddVariable(varDecl);
                            break;
                        }

                    case "import":
                        {
                            var libName = ExpectIdentifier();
                            ExpectToken(";");

                            var libDecl = Contract.LoadLibrary(libName, module.Scope, module.Kind);
                            module.Libraries[libName] = libDecl;

                            break;
                        }

                    case "event":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var eventName = ExpectIdentifier();
                                ExpectToken(":");
                                var eventType = ExpectType();
                                ExpectToken("=");

                                if (eventType.IsWeird)
                                {
                                    throw new CompilerException("invalid event type: " + eventType);
                                }

                                var temp = FetchToken();
                                byte[] description;
                                switch (temp.kind)
                                {
                                    case TokenKind.String:
                                        description = EventDeclaration.GenerateScriptFromString(eventType, temp.value);
                                        break;

                                    case TokenKind.Bytes:
                                        description = Base16.Decode(temp.value);
                                        break;

                                    case TokenKind.Identifier:
                                        {
                                            var descModule = FindModule(temp.value, true) as Script;
                                            if (descModule != null)
                                            {
                                                var descSecondType = descModule.Parameters[1].Type;
                                                if (descSecondType != eventType)
                                                {
                                                    throw new CompilerException($"descriptions second parameter has type {descSecondType}, does not match event type: {eventType}");
                                                }

                                                if (descModule.script != null)
                                                {
                                                    description = descModule.script;
                                                }
                                                else
                                                {
                                                    throw new CompilerException($"description module not ready: {temp.value}");
                                                }
                                            }
                                            else
                                            {
                                                throw new CompilerException($"description module not found: {temp.value}");
                                            }
                                            break;
                                        }

                                    default:
                                        throw new CompilerException($"expected valid event description, got {temp.kind} instead");
                                }

                                ExpectToken(";");

                                var value = (byte)((byte)EventKind.Custom + contract.Events.Count);

                                var eventDecl = new EventDeclaration(module.Scope, eventName, value, eventType, description);

                                if (contract.Events.ContainsKey(eventName))
                                {
                                    throw new CompilerException($"duplicated event: {eventName}");
                                }

                                contract.Events[eventName] = eventDecl;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                            break;
                        }


                    case "constructor":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = SmartContract.ConstructorName;
                                var parameters = ParseParameters(module.Scope);
                                var scope = new Scope(module.Scope, name, parameters);

                                if (parameters.Length != 1 || parameters[0].Type.Kind != VarKind.Address)
                                {
                                    throw new CompilerException("constructor must have only one parameter of type address");
                                }

                                var method = contract.AddMethod(line, name, true, MethodKind.Constructor, VarType.Find(VarKind.None), parameters, scope);

                                ExpectToken("{");

                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);                                    
                            }

                        }

                    case "property":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var propertyName = ExpectIdentifier();

                                var parameters = new MethodParameter[0];
                                var scope = new Scope(module.Scope, propertyName, parameters);

                                ExpectToken(":");

                                var returnType = ExpectType();

                                if (!propertyName.StartsWith("is") || char.IsLower(propertyName[2]))
                                {
                                    propertyName = "get" + char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                                }

                                var method = contract.AddMethod(line, propertyName, true, MethodKind.Property, returnType, parameters, scope);

                                var next = FetchToken();
                                if (next.value == "=")
                                {
                                    var literal = ExpectExpression(scope);

                                    if (literal.ResultType != returnType)
                                    {
                                        throw new CompilerException($"Expected expression of type {returnType} for property {propertyName}, found {literal.ResultType} instead");
                                    }

                                    var block = new StatementBlock(scope);
                                    block.Commands.Add(new ReturnStatement(method, literal));
                                    contract.SetMethodBody(propertyName, block);

                                    ExpectToken(";");
                                }
                                else
                                {
                                    Rewind();

                                    ExpectToken("{");
                                    contract.SetMethodBody(propertyName, ParseCommandBlock(scope, method));
                                    ExpectToken("}");
                                }


                                contract.library.AddMethod(propertyName, MethodImplementationType.LocalCall, returnType, parameters);
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }

                        }

                    case "public":
                    case "private":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = ExpectIdentifier();

                                var parameters = ParseParameters(module.Scope);
                                var scope = new Scope(module.Scope, name, parameters);

                                var returnType = VarType.Find(VarKind.None);

                                var next = FetchToken();
                                if (next.value == ":")
                                {
                                    returnType = ExpectType();
                                }
                                else
                                {
                                    Rewind();
                                }

                                var method = contract.AddMethod(line, name, token.value == "public", MethodKind.Method, returnType, parameters, scope);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                contract.library.AddMethod(name, MethodImplementationType.LocalCall, returnType, parameters);
                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }

                        }

                    case "task":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = ExpectIdentifier();

                                var parameters = ParseParameters(module.Scope);

                                if (parameters.Length != 0)
                                {
                                    throw new CompilerException("task should not have parameters");
                                }

                                var scope = new Scope(module.Scope, name, parameters);

                                var method = contract.AddMethod(line, name, true, MethodKind.Task, VarType.Find(VarKind.Bool), parameters, scope);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "trigger":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;
                                var name = ExpectIdentifier();

                                if (!name.StartsWith("on"))
                                {
                                    name = "on" + name;
                                }

                                var isValid = false;

                                string[] validTriggerNames;

                                switch (module.Kind)
                                {
                                    case ModuleKind.Account:
                                    case ModuleKind.Contract:
                                        validTriggerNames = Enum.GetNames(typeof(AccountTrigger)).ToArray();
                                        break;

                                    case ModuleKind.Token:
                                        validTriggerNames = Enum.GetNames(typeof(TokenTrigger)).ToArray();
                                        break;

                                    case ModuleKind.Organization:
                                        validTriggerNames = Enum.GetNames(typeof(OrganizationTrigger)).ToArray();
                                        break;

                                    default:
                                        throw new CompilerException("Triggers not supported in " + module.Kind);
                                }

                                foreach (var allowedName in validTriggerNames)
                                {
                                    if (allowedName.Equals(name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isValid = true;
                                        break;
                                    }
                                }

                                if (!isValid)
                                {
                                    throw new CompilerException("invalid trigger name:" + name);
                                }

                                var parameters = ParseParameters(module.Scope);

                                switch (name)
                                {
                                    case "onMint":
                                    case "onBurn":
                                    case "onSend":
                                    case "onReceive": // address, symbol, amount
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Address, VarKind.String, VarKind.Number });
                                        break;

                                    case "onWitness":
                                    case "onSeries":
                                    case "onKill":
                                    case "onUpgrade": // address
                                        CheckParameters(name, parameters, new[] { VarKind.Address });
                                        break;

                                    case "onMigrate": // from, to
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Address });
                                        break;

                                    case "onWrite": // address, data
                                        CheckParameters(name, parameters, new[] { VarKind.Address, VarKind.Any });
                                        break;

                                    default:
                                        throw new CompilerException($"Proper trigger support for trigger {name} is not implemented");
                                }

                                var scope = new Scope(module.Scope, name, parameters);

                                var method = contract.AddMethod(line, name, true, MethodKind.Trigger, VarType.Find(VarKind.None), parameters, scope);

                                ExpectToken("{");
                                contract.SetMethodBody(name, ParseCommandBlock(scope, method));
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "code":
                        {
                            var script = module as Script;
                            if (script != null)
                            {
                                var blockName = "main";
                                script.Parameters = ParseParameters(module.Scope);

                                if (module.Kind == ModuleKind.Description) 
                                {
                                    CheckParameters(blockName, script.Parameters, new[] { VarKind.Address, VarKind.Any });
                                }

                                var scope = new Scope(module.Scope, blockName, script.Parameters);

                                script.ReturnType = VarType.Find(VarKind.None);

                                var next = FetchToken();
                                if (next.value == ":")
                                {
                                    script.ReturnType = ExpectType();
                                }
                                else
                                {
                                    Rewind();
                                }

                                var method = new MethodInterface(script.library, MethodImplementationType.Custom, blockName, true, MethodKind.Method, script.ReturnType, new MethodParameter[0]);

                                // TODO does this have to be added somewhere or its ok to just do this?
                                var decl = new MethodDeclaration(scope, method);

                                ExpectToken("{");
                                script.main = ParseCommandBlock(scope, decl);
                                ExpectToken("}");

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }

                    case "nft":
                        {
                            if (module.Kind != ModuleKind.Token)
                            {

                                throw new CompilerException("unexpected token: " + token.value);
                            }

                            var nftName = ExpectIdentifier();

                            ExpectToken("<");
                            var romType = ExpectType();
                            ExpectToken(",");
                            var ramType = ExpectType();
                            ExpectToken(">");

                            var subModule = new NFT(nftName, romType, ramType, module);
                            ExpectToken("{");
                            ParseModule(subModule);
                            ExpectToken("}");

                            module.AddSubModule(subModule);

                            break;
                        }


                    default:
                        throw new CompilerException("unexpected token: " + token.value);
                }

            } while (true);
        }

        private void CheckParameters(string name, MethodParameter[] parameters, VarKind[] expected)
        {
            if (parameters.Length != expected.Length)
            {
                throw new CompilerException($"Expected {expected.Length} parameters for {name}, found {parameters.Length} instead");
            }

            for (int i=0; i<parameters.Length; i++)
            if (parameters[i].Type.Kind != expected[i] && expected[i] != VarKind.Any)
            {
                throw new CompilerException($"Expected parameter #{i+1} to be {expected[i]} got {parameters[i].Type} instead");
            }
        }

        private DecimalVarType ParseDecimal()
        {
            ExpectToken("<");
            var decimals = int.Parse(ExpectNumber());
            ExpectToken(">");

            if (decimals < 1 || decimals > 32)
            {
                throw new CompilerException("invalid decimals: " + decimals);
            }

            return (DecimalVarType)VarType.Find(VarKind.Decimal, decimals);
        }

        private ArrayVarType ParseArray()
        {
            ExpectToken("<");
            var elementType = ExpectType();
            ExpectToken(">");

            return (ArrayVarType)VarType.Find(VarKind.Array, elementType);
        }

        private MethodParameter[] ParseParameters(Scope scope)
        {
            var list = new List<MethodParameter>();

            ExpectToken("(");

            do
            {
                var token = FetchToken();

                if (token.value == ")")
                {
                    break;                
                }
                else
                {
                    Rewind();
                }

                if (list.Count > 0)
                {
                    ExpectToken(",");
                }

                var name = ExpectIdentifier();
                ExpectToken(":");
                var type = ExpectType();

                list.Add(new MethodParameter(name, type));

            } while (true);


            return list.ToArray();
        }

        private StatementBlock ParseCommandBlock(Scope scope, MethodDeclaration method)
        {
            var block = new StatementBlock(scope);
            var terminateEarly = false;

            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return block;

                    case "emit":
                        {
                            var contract = scope.Module as Contract;
                            if (contract != null)
                            {
                                var eventName = ExpectIdentifier();

                                ExpectToken("(");

                                var addrExpr = ExpectExpression(scope);
                                ExpectToken(",");
                                var valueExpr = ExpectExpression(scope);
                                ExpectToken(")");
                                ExpectToken(";");

                                if (contract.Events.ContainsKey(eventName))
                                {
                                    var evt = contract.Events[eventName];

                                    if (addrExpr.ResultType.Kind != VarKind.Address)
                                    {
                                        throw new CompilerException($"Expected first argument of type {VarKind.Address}, got {addrExpr.ResultType} instead");
                                    }

                                    if (evt.returnType != valueExpr.ResultType)
                                    {
                                        throw new CompilerException($"Expected second argument of type {evt.returnType}, got {valueExpr.ResultType} instead");
                                    }

                                    block.Commands.Add(new EmitStatement(evt, addrExpr, valueExpr));
                                }
                                else
                                {
                                    throw new CompilerException($"Undeclared event: {eventName}");
                                }

                            }
                            else
                            {
                                throw new CompilerException("Emit statments only allowed in contracts");
                            }

                            break;
                        }

                    case "return":
                        {
                            var temp = FetchToken();
                            Rewind();

                            Expression expr;
                            if (temp.value != ";")
                            {
                                expr = ExpectExpression(scope);
                            }
                            else
                            {
                                expr = null;
                            }

                            block.Commands.Add(new ReturnStatement(method, expr));
                            ExpectToken(";");

                            terminateEarly = true;
                            break;
                        }

                    case "throw":
                        {
                            var expr = ExpectExpression(scope);

                            if (expr.ResultType.Kind != VarKind.String)
                            {
                                throw new CompilerException("string expression expected");
                            }

                            block.Commands.Add(new ThrowStatement(expr));
                            ExpectToken(";");

                            terminateEarly = true;
                            break;
                        }

                    case "asm":
                        {
                            ExpectToken("{");
                            var lines = ExpectAsm();
                            ExpectToken("}");

                            block.Commands.Add(new AsmBlockStatement(lines));
                            break;
                        }

                    case "local":
                        {
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var type = ExpectType();

                            var next = FetchToken();

                            Expression initExpr;
                            if (next.value == ":=")
                            {
                                initExpr = ExpectExpression(scope);
                            }
                            else
                            {
                                initExpr = null;
                                Rewind();
                            }

                            ExpectToken(";");

                            var varDecl = new VarDeclaration(scope, varName, type, VarStorage.Local);

                            scope.AddVariable(varDecl);

                            if (initExpr != null)
                            {
                                var initCmd = new AssignStatement();
                                initCmd.variable = varDecl;
                                initCmd.valueExpression = initExpr;
                                block.Commands.Add(initCmd);
                            }

                            break;
                        }

                    case "if":
                        {
                            var ifCommand = new IfStatement(scope);

                            ExpectToken("(");
                            ifCommand.condition = ExpectExpression(scope);

                            if (ifCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            ifCommand.body = ParseCommandBlock(ifCommand.Scope, method);

                            ExpectToken("}");

                            var next = FetchToken();

                            if (next.value == "else")
                            {
                                ExpectToken("{");

                                ifCommand.@else = ParseCommandBlock(ifCommand.Scope, method);

                                ExpectToken("}");
                            }
                            else
                            {
                                Rewind();
                            }

                            block.Commands.Add(ifCommand);
                            break;
                        }

                    case "switch":
                        {
                            var switchCommand = new SwitchStatement(scope);

                            ExpectToken("(");
                            switchCommand.variable = ExpectExpression(scope) as VarExpression;

                            if (switchCommand.variable == null)
                            {
                                throw new CompilerException($"switch condition must be variable expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            while (true)
                            {
                                var next = FetchToken();

                                if (next.value == "}")
                                {
                                    break;
                                }
                                else
                                if (next.value == "default")
                                {
                                    ExpectToken(":");
                                    switchCommand.@default = ParseCommandBlock(switchCommand.Scope, method);
                                    break;
                                }

                                Rewind();
                                ExpectToken("case");

                                var literal = ParseExpression(scope, false) as LiteralExpression;

                                if (literal == null)
                                {
                                    throw new CompilerException($"switch case condition must be literal expression");
                                }

                                ExpectToken(":");

                                var body = ParseCommandBlock(switchCommand.Scope, method);

                                switchCommand.cases.Add(new CaseStatement(literal, body));
                            }

                            ExpectToken("}");

                            block.Commands.Add(switchCommand);
                            break;
                        }

                    case "while":
                        {
                            var whileCommand = new WhileStatement(scope);

                            ExpectToken("(");
                            whileCommand.condition = ExpectExpression(scope);

                            if (whileCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            whileCommand.body = ParseCommandBlock(whileCommand.Scope, method);

                            ExpectToken("}");

                            block.Commands.Add(whileCommand);
                            break;
                        }

                    case "do":
                        {
                            var whileCommand = new DoWhileStatement(scope);

                            ExpectToken("{");

                            whileCommand.body = ParseCommandBlock(whileCommand.Scope, method);

                            ExpectToken("}");

                            ExpectToken("while");

                            ExpectToken("(");
                            whileCommand.condition = ExpectExpression(scope);

                            if (whileCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");
                            ExpectToken(";");

                            block.Commands.Add(whileCommand);
                            break;
                        }

                    case "break":
                        {
                            ExpectToken(";");

                            block.Commands.Add(new BreakStatement(scope));
                            terminateEarly = true;
                            break;
                        }

                    case "continue":
                        {
                            ExpectToken(";");

                            block.Commands.Add(new ContinueStatement(scope));
                            terminateEarly = true;
                            break;
                        }

                    default:
                        if (token.kind == TokenKind.Identifier)
                        {
                            var next = FetchToken();

                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                var setCommand = new AssignStatement();

                                var varName = token.value;
                                VarType expressionExpectedType;

                                var indexerPos = varName.IndexOf("[");
                                if (indexerPos >= 0)
                                {
                                    if (!varName.EndsWith("]"))
                                    {
                                        throw new CompilerException("expected ] in array indexer");
                                    }

                                    var tmp = varName.Substring(indexerPos + 1, (varName.Length - indexerPos) - 2);

                                    varName = varName.Substring(0, indexerPos);

                                    setCommand.variable = scope.FindVariable(varName);

                                    var arrayType = setCommand.variable.Type as ArrayVarType;

                                    if (arrayType == null)
                                    {
                                        throw new CompilerException($"variable {setCommand.variable} is not an array");
                                    }

                                    expressionExpectedType = arrayType.elementType;
                                    setCommand.keyExpression = ParseArrayIndexingExpression(scope, tmp, expressionExpectedType);
                                }
                                else
                                {
                                    setCommand.variable = scope.FindVariable(varName);
                                    expressionExpectedType = setCommand.variable.Type;
                                }

                                var expr = ParseAssignmentExpression(scope, next, setCommand.variable, expressionExpectedType);

                                setCommand.valueExpression = expr;
                                block.Commands.Add(setCommand);
                            }
                            else
                            if (next.kind == TokenKind.Selector)
                            {
                                var varDecl = scope.FindVariable(token.value, false);
                                bool isStructField = false;

                                LibraryDeclaration libDecl;

                                if (varDecl != null)
                                {
                                    switch (varDecl.Type.Kind)
                                    {
                                        case VarKind.Storage_Map:
                                            {
                                                var mapDecl = (MapDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("Map");
                                                libDecl = libDecl.PatchMap(mapDecl);
                                                break;
                                            }

                                        case VarKind.Storage_List:
                                            {
                                                var listDecl = (ListDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("List");
                                                libDecl = libDecl.PatchList(listDecl);
                                                break;
                                            }

                                        case VarKind.Storage_Set:
                                            {
                                                var setDecl = (SetDeclaration)varDecl;
                                                libDecl = scope.Module.FindLibrary("Set");
                                                libDecl = libDecl.PatchSet(setDecl);
                                                break;
                                            }

                                        case VarKind.Struct:
                                            {
                                                libDecl = null;
                                                isStructField = true;
                                                break;
                                            }


                                        default:
                                            throw new CompilerException($"expected {token.value} to be generic type, but is {varDecl.Type} instead");
                                    }
                                }
                                else
                                {
                                    libDecl = scope.Module.FindLibrary(token.value);
                                }

                                if (isStructField)
                                {
                                    var fieldName = ExpectIdentifier();

                                    next = FetchToken();

                                    if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                                    {
                                        var structName = ((StructVarType)varDecl.Type).name;
                                        if (!_structs.ContainsKey(structName))
                                        {
                                            throw new CompilerException("unknown struct: " + structName);
                                        }

                                        var structDecl = _structs[structName];
                                        var fieldDecl = structDecl.fields.FirstOrDefault(x => x.name == fieldName);

                                        var assignment = new AssignStatement();
                                        assignment.variable = varDecl;
                                        assignment.keyExpression = new LiteralExpression(scope, "\"" + fieldName + "\"" , fieldDecl.type);
                                        assignment.valueExpression = ParseAssignmentExpression(scope, next, varDecl, fieldDecl.type);
                                        block.Commands.Add(assignment);
                                    }
                                    else
                                    {
                                        throw new CompilerException($"expected assignment operator");
                                    }
                                }
                                else
                                {
                                    var methodCall = new MethodCallStatement();
                                    methodCall.expression = ParseMethodExpression(scope, libDecl, varDecl);

                                    block.Commands.Add(methodCall);
                                }
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }

                            ExpectToken(";");
                        }
                        else
                        {
                            throw new CompilerException("unexpected token: " + token.value);
                        }

                        break;
                }
            } while (!terminateEarly);

            if (terminateEarly)
            {
                if (block.Commands.Count > 1)
                {
                    ExpectToken("}");
                    Rewind();
                }

                return block;
            }
            else
            {
                throw new CompilerException("weird compiler flow detected, contact devs");
            }
        }

        private Expression ParseAssignmentExpression(Scope scope, LexerToken next, VarDeclaration varDecl, VarType expectedType)
        {
            var expr = ExpectExpression(scope);
            if (next.value != ":=")
            {
                var str = next.value.Substring(0, next.value.Length - 1);
                var op = ParseOperator(str);

                if (op == OperatorKind.Unknown)
                {
                    throw new CompilerException("unknown operator: " + next.value);
                }

                expr = new BinaryExpression(scope, op, new VarExpression(scope, varDecl), expr);
            }


            expr = Expression.AutoCast(expr, expectedType);

            return expr;
        }

        private Expression ExpectExpression(Scope scope)
        {
            var expr = ParseExpression(scope);
            if (expr == null)
            {
                throw new CompilerException("expected expression");
            }

            var macro = expr as MacroExpression;
            if (macro != null)
            {
                return macro.Unfold(scope);
            }

            return expr;
        }

        private Expression ParseExpressionFromToken(LexerToken first, Scope scope)
        {
            switch (first.kind)
            {
                case TokenKind.Identifier:
                    {
                        var constDecl = scope.FindConstant(first.value, false);
                        if (constDecl != null)
                        {
                            return new ConstExpression(scope, constDecl);
                        }
                        else
                        {
                            var indexerPos = first.value.IndexOf("[");
                            if (indexerPos >= 0)
                            {
                                if (!first.value.EndsWith("]"))
                                {
                                    throw new CompilerException("expected ] in array indexer");
                                }

                                var varName = first.value;

                                var tmp = first.value.Substring(indexerPos + 1, (varName.Length - indexerPos) - 2);

                                varName = varName.Substring(0, indexerPos);

                                var arrayVar = scope.FindVariable(varName);

                                var arrayType = arrayVar.Type as ArrayVarType;

                                if (arrayType == null)
                                {
                                    throw new CompilerException($"variable {arrayVar} is not an array");
                                }

                                var indexExpression = ParseArrayIndexingExpression(scope, tmp, arrayType.elementType);
                                return new ArrayExpression(scope, arrayVar, indexExpression);
                            }

                            var varDecl = scope.FindVariable(first.value, false);
                            if (varDecl != null)
                            {
                                return new VarExpression(scope, varDecl);
                            }

                            var libDecl = scope.Module.FindLibrary(first.value, false);
                            if (libDecl != null)
                            {
                                throw new NotImplementedException();
                            }

                            var method = scope.Module.FindMethod(first.value);
                            if (method != null)
                            {
                                return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Method, method));
                            }

                            var module = scope.Module.FindModule(first.value, true);
                            if (module != null)
                            {
                                return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Module, method));
                            }

                            throw new CompilerException("unknown identifier: " + first.value);
                        }
                    }

                case TokenKind.Number:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Number));
                    }

                case TokenKind.Decimal:
                    {
                        var temp = first.value.Split('.')[1];
                        var decimals = temp.Length;
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Decimal, decimals));
                    }

                case TokenKind.String:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.String));
                    }

                case TokenKind.Bool:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Bool));
                    }

                case TokenKind.Address:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Address));
                    }

                case TokenKind.Hash:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Hash));
                    }

                case TokenKind.Bytes:
                    {
                        return new LiteralExpression(scope, first.value, VarType.Find(VarKind.Bytes));
                    }

                case TokenKind.Macro:
                    {
                        var args = new List<string>();

                        var next = this.FetchToken();
                        if (next.value == "(")
                        {
                            do
                            {
                                next = this.FetchToken();
                                if (next.value == ")")
                                {
                                    break;
                                }
                                else
                                {
                                    switch (next.kind)
                                    {
                                        case TokenKind.Asm:
                                        case TokenKind.Macro:
                                        case TokenKind.Operator:
                                        case TokenKind.Separator:
                                        case TokenKind.Selector:
                                            throw new CompilerException($"not valid macro argument: {next.kind}");


                                        default:
                                            args.Add(next.value);
                                            break;
                                    }
                                }
                            } while (true);
                        }
                        else
                        {
                            Rewind();
                        }

                        var macro = new MacroExpression(scope, first.value, args);

                        return macro.Unfold(scope);
                    }

                default:
                    throw new CompilerException($"cannot turn {first.kind} to an expression");
            }
        }

        private OperatorKind ParseOperator(string str)
        {
            switch (str)
            {
                case "<":
                    return OperatorKind.Less;

                case "<=":
                    return OperatorKind.LessOrEqual;

                case ">":
                    return OperatorKind.Greater;

                case ">=":
                    return OperatorKind.GreaterOrEqual;

                case "==":
                    return OperatorKind.Equal;

                case "!=":
                    return OperatorKind.Different;

                case "/":
                    return OperatorKind.Division;

                case "%":
                    return OperatorKind.Modulus;

                case "*":
                    return OperatorKind.Multiplication;

                case "+":
                    return OperatorKind.Addition;

                case "-":
                    return OperatorKind.Subtraction;

                case "^":
                    return OperatorKind.Power;

                case "<<":
                    return OperatorKind.ShiftLeft;

                case ">>":
                    return OperatorKind.ShiftRight;

                case "or":
                    return OperatorKind.Or;

                case "and":
                    return OperatorKind.And;

                case "xor":
                    return OperatorKind.Xor;

                default:
                    return OperatorKind.Unknown;
            }
        }

        // TODO: here we only suppprt variables or literal expressions as index, but could be expand later...
        private Expression ParseArrayIndexingExpression(Scope scope, string exprStr, VarType elementType)
        {
            var varDecl = scope.FindVariable(exprStr, false);
            if (varDecl != null)
            {
                return new VarExpression(scope, varDecl);
            }

            return new LiteralExpression(scope, exprStr, elementType); 
        }

        private Expression ParseBinaryExpression(Scope scope, LexerToken opToken, Expression leftSide, Expression rightSide)
        {
            if (opToken.kind != TokenKind.Operator)
            {
                throw new CompilerException("expected operator, got " + opToken.kind);
            }

            var op = ParseOperator(opToken.value);

            if (op == OperatorKind.Unknown)
            {
                throw new CompilerException("unknown operator: " + opToken.value);
            }

            if (rightSide.ResultType != leftSide.ResultType)
            {
                if (leftSide.ResultType.Kind == VarKind.String && op == OperatorKind.Addition)
                {
                    rightSide = new CastExpression(scope, VarType.Find(VarKind.String), rightSide);
                }
                else
                if (leftSide.ResultType.Kind == VarKind.Number && rightSide.ResultType.Kind == VarKind.Timestamp && op == OperatorKind.Subtraction)
                {
                    rightSide = new CastExpression(scope, VarType.Find(VarKind.Number), rightSide);
                }
                else
                if (leftSide.ResultType.Kind == VarKind.Timestamp && rightSide.ResultType.Kind == VarKind.Number && op == OperatorKind.Addition)
                {
                    rightSide = new CastExpression(scope, VarType.Find(VarKind.Timestamp), rightSide);
                }
                else
                {
                    throw new CompilerException($"type mistmatch, {leftSide.ResultType} on left, {rightSide.ResultType} on right");
                }
            }

            if (op == OperatorKind.Different)
            {
                var innerExpr = new BinaryExpression(scope, OperatorKind.Equal, leftSide, rightSide);

                return new NegationExpression(scope, innerExpr);
            }

            return new BinaryExpression(scope, op, leftSide, rightSide);
        }

        private Expression ParseExpression(Scope scope, bool allowBinary = true)
        {
            var first = FetchToken();

            if (first.kind == TokenKind.Operator && (first.value == "!" || first.value == "-"))
            {
                var expr = ParseExpression(scope, allowBinary);
                return new NegationExpression(scope, expr);
            }

            var second = FetchToken();

            switch (second.kind)
            {
                case TokenKind.Separator:
                    {
                        Rewind();
                        return ParseExpressionFromToken(first, scope);
                    }

                case TokenKind.Operator:
                    {
                        var leftSide = ParseExpressionFromToken(first, scope);

                        if (!allowBinary)
                        {
                            Rewind();
                            return leftSide;
                        }

                        var rightSide = ParseExpression(scope, false);
                        var result = ParseBinaryExpression(scope, second, leftSide, rightSide);

                        var next = FetchToken();
                        if (next.kind == TokenKind.Operator)
                        {
                            var third = ParseExpression(scope);
                            var op = ParseOperator(next.value);
                            return new BinaryExpression(scope, op, result, third);
                        }
                        else
                        {
                            Rewind();
                            return result;
                        }
                    }

                case TokenKind.Selector:
                    {
                        var varDecl = scope.FindVariable(first.value, false);

                        LibraryDeclaration libDecl;

                        Expression leftSide = null;

                        bool implicitIsLiteral = true;

                        if (varDecl != null)
                        {
                            // TODO this code is duplicated, copypasted from other method above, refactor this later...
                            switch (varDecl.Type.Kind)
                            {
                                case VarKind.Storage_Map:
                                    {
                                        var mapDecl = (MapDeclaration)varDecl;
                                        libDecl = scope.Module.FindLibrary("Map");
                                        libDecl = libDecl.PatchMap(mapDecl);
                                        break;
                                    }

                                case VarKind.Storage_List:
                                    {
                                        var listDecl = (ListDeclaration)varDecl;
                                        libDecl = scope.Module.FindLibrary("List");
                                        libDecl = libDecl.PatchList(listDecl);
                                        break;
                                    }

                                case VarKind.Storage_Set:
                                    {
                                        var setDecl = (SetDeclaration)varDecl;
                                        libDecl = scope.Module.FindLibrary("Set");
                                        libDecl = libDecl.PatchSet(setDecl);
                                        break;
                                    }

                                case VarKind.Struct:
                                    {
                                        libDecl = null;

                                        leftSide = ParseStructFieldExpression(scope, varDecl);
                                        break;
                                    }

                                default:
                                    {
                                        var typeName = varDecl.Type.Kind.ToString();
                                        libDecl = scope.Module.FindLibrary(typeName, false);
                                        if (libDecl != null)
                                        {
                                            implicitIsLiteral = false;

                                            if (varDecl.Type.Kind == VarKind.Enum)
                                            {
                                                // Patch Enum methods to use proper type
                                                foreach (var method in libDecl.methods.Values)
                                                {
                                                    foreach (var arg in method.Parameters)
                                                    {
                                                        if (arg.Type.Kind == VarKind.Enum)
                                                        {
                                                            arg.Type = varDecl.Type;
                                                        }
                                                    }
                                                }
                                            }

                                            break;
                                        }

                                        throw new CompilerException($"expected {first.value} to be generic type, but is {varDecl.Type} instead");
                                    }

                            }
                        }
                        else
                        if (_enums.ContainsKey(first.value))
                        {
                            var enumDecl = _enums[first.value];
                            var entryName = ExpectIdentifier();
                            if (enumDecl.entryNames.ContainsKey(entryName))
                            {
                                var enumValue = enumDecl.entryNames[entryName].ToString();
                                return new LiteralExpression(scope, enumValue, VarType.Find(VarKind.Enum, first.value));
                            }
                            else
                            {
                                throw new CompilerException($"Enum {first.value} does not contain member {entryName}");

                            }
                        }
                        else
                        {
                            libDecl = scope.Module.FindLibrary(first.value);
                        }

                        if (leftSide == null)
                        {
                            leftSide = ParseMethodExpression(scope, libDecl, varDecl, implicitIsLiteral);
                        }

                        second = FetchToken();
                        if (second.kind == TokenKind.Operator)
                        {
                            var rightSide = ExpectExpression(scope);
                            return ParseBinaryExpression(scope, second, leftSide, rightSide);
                        }
                        else
                        {
                            Rewind();
                            return leftSide;
                        }
                    }

                default:
                    if (first.kind == TokenKind.Separator)
                    {
                        switch (first.value)
                        {
                            case "(":
                                {
                                    Rewind();
                                    var leftSide = ExpectExpression(scope);
                                    ExpectToken(")");
                                    var op = FetchToken();
                                    if (op.kind != TokenKind.Operator)
                                    {
                                        throw new CompilerException($"expected operator, got {op.kind} instead");
                                    }
                                    var rightSide = ExpectExpression(scope);
                                    return ParseBinaryExpression(scope, op, leftSide, rightSide);
                                }

                            default:
                                throw new CompilerException($"unexpected token: {first.value}");
                        }

                    }
                    break;
            }

            return null;
        }

        private MethodExpression ParseMethodExpression(Scope scope, LibraryDeclaration library, VarDeclaration implicitArg = null, bool implicitAsLiteral = true)
        {
            var expr = new MethodExpression(scope);

            var methodName = ExpectIdentifier();

            expr.method = library.FindMethod(methodName);

            var next = FetchToken();

            if (next.value == "<")
            {
                do
                {
                    next = FetchToken();
                    if (next.value == ">")
                    {
                        break;
                    }

                    Rewind();

                    if (expr.generics.Count > 0)
                    {
                        ExpectToken(",");
                    }

                    var genType = ExpectType();

                    if (genType.IsStorageBound || (genType.Kind != VarKind.None && genType.IsWeird))
                    {
                        throw new CompilerException($"{genType.Kind} can't be used as generic type");
                    }

                    expr.generics.Add(genType);
                } while (true);
            }
            else
            {
                Rewind();
            }

            expr.PatchGenerics();

            ExpectToken("(");

            var firstIndex = implicitArg != null ? 1 : 0;

            bool isCallLibrary = expr.method.Library.Name == "Call";

            if (isCallLibrary)
            {
                int i = 0;
                do
                {
                    next = FetchToken();
                    if (next.value == ")")
                    {
                        break;
                    }
                    else
                    {
                        Rewind();
                    }

                    if (i > firstIndex)
                    {
                        ExpectToken(",");
                    }

                    Expression arg;

                    if (i == 0 && implicitArg != null)
                    {
                        arg = new LiteralExpression(scope, $"\"{implicitArg.Name}\"", VarType.Find(VarKind.String));
                    }
                    else
                    {
                        arg = ExpectExpression(scope);
                    }

                    expr.arguments.Add(arg);

                    i++;
                } while (true);
            }
            else
            {
                var paramCount = expr.method.Parameters.Length;
                for (int i = 0; i < paramCount; i++)
                {
                    if (i > firstIndex)
                    {
                        ExpectToken(",", $"missing arguments for {expr.method.Library.Name}.{methodName}(), got {expr.arguments.Count} but expected {paramCount}");
                    }

                    Expression arg;

                    if (i == 0 && implicitArg != null)
                    {
                        if (implicitAsLiteral)
                        {
                            arg = new LiteralExpression(scope, $"\"{implicitArg.Name}\"", VarType.Find(VarKind.String));
                        }
                        else
                        {
                            arg = new VarExpression(scope, implicitArg);
                        }
                    }
                    else
                    {
                        arg = ExpectExpression(scope);
                    }

                    expr.arguments.Add(arg);

                    var expectedType = expr.method.Parameters[i].Type;

                    arg = Expression.AutoCast(arg, expectedType);

                    if (arg.ResultType != expectedType && expectedType.Kind != VarKind.Any)
                    {
                        throw new CompilerException($"expected argument of type {expectedType}, got {arg.ResultType} instead");
                    }
                }

                ExpectToken(")");
            }

            return expr;
        }


        private StructFieldExpression ParseStructFieldExpression(Scope scope, VarDeclaration varDecl)
        {
            var fieldName = ExpectIdentifier();
            var expr = new StructFieldExpression(scope, varDecl, fieldName);
            return expr;
        }

        private const int MaxRegisters = VirtualMachine.DefaultRegisterCount;
        private Node[] registerAllocs = new Node[MaxRegisters];
        private string[] registerAlias = new string[MaxRegisters];

        public Register AllocRegister(CodeGenerator generator, Node node, string alias = null)
        {
            if (alias != null)
            {
                foreach (var entry in registerAlias)
                {
                    if (entry == alias)
                    {
                        throw new Exception("alias already exists: " + alias);
                    }
                }
            }

            int baseRegister = 1;
            for (int i = baseRegister; i < registerAllocs.Length; i++)
            {
                if (registerAllocs[i] == null)
                {
                    registerAllocs[i] = node;
                    registerAlias[i] = alias;

                    string extra = alias != null ? " => " + alias : "";
                    //Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "alloc r" + i + extra);

                    if (alias != null)
                    {
                        generator.AppendLine(node, $"ALIAS r{i} ${alias}");
                    }

                    return new Register(i, alias);
                }
            }

            throw new CompilerException("no more available registers");
        }

        public void DeallocRegister(ref Register register)
        {
            if (register == null)
            {
                return;
            }

            var index = register.Index;

            if (registerAllocs[index] != null)
            {
                var alias = registerAlias[index];

                //Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "dealloc r" + index + " => "+alias);

                registerAllocs[index] = null;
                registerAlias[index] = null;

                register = null;
                return;
            }

            throw new CompilerException("register not allocated");
        }

        public void VerifyRegisters()
        {

            for (int i = 0; i < registerAllocs.Length; i++)
            {
                if (registerAllocs[i] != null)
                {
                    throw new CompilerException($"register r{i} not deallocated");
                }
            }            
        }

        public LoopStatement CurrentLoop { get; private set; }
        private Stack<LoopStatement> _loops = new Stack<LoopStatement>();
        public void PushLoop(LoopStatement loop)
        {
            _loops.Push(loop);
            CurrentLoop = loop;
        }

        public void PopLoop(LoopStatement loop)
        {
            if (CurrentLoop != loop)
            {
                throw new CompilerException("error popping loop node");
            }

            _loops.Pop();
            CurrentLoop = _loops.Count > 0 ? _loops.Peek() : null;
        }
    }
}

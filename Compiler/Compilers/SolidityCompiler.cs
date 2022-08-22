using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Phantasma.Domain;
using Phantasma.Neo.VM.Types;
using Phantasma.Numerics;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.AST.Statements;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Lexers;
using System.Reflection.Metadata;
using Phantasma.Tomb.AST.Expressions;

namespace Phantasma.Tomb.Compilers
{
    [Compiler(Extension = ".sol")]
    public class SolidityCompiler: Compiler
    {
        public SolidityCompiler(int version = DomainSettings.LatestKnownProtocol) : base(version)
        {
        }

        protected override Lexer CreateLexer()
        {
            return new SolidityLexer();
        }

        protected override void GenerateModules()
        {
            while (HasTokens())
            {
                var firstToken = FetchToken();

                Module module = null;

                switch (firstToken.value)
                {
                    case "pragma":
                        {
                            ExpectToken("solidity");
                            // we just ignore the pragma contents for now
                            SkipUntilToken(";");
                            break;
                        }

                    case "contract":
                        {
                            var contractName = ExpectIdentifier();

                            if (!ValidationUtils.IsValidIdentifier(contractName))
                            {
                                throw new CompilerException("contract does not have a valid name: " + contractName);
                            }

                            module = new Contract(contractName, ModuleKind.Contract);
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
                    AddModule(module);
                }
            }
        }

        private void ParseModule(Module module)
        {
            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return;

                    case "function":
                        {
                            var contract = module as Contract;
                            if (contract != null)
                            {
                                var line = this.CurrentLine;

                                var next = FetchToken();

                                string name = next.value;

                                var parameters = ParseParameters(module.Scope);
                                var scope = new Scope(module.Scope, name, parameters);

                                VarType returnType;

                                var modifiers = ParseFunctionAttributes(out returnType);

                                var isMulti = false; // no support for multi returns in Solidity (at least for now, investigate if this should be possible)

                                var method = contract.AddMethod(line, name, token.value == "public", MethodKind.Method, returnType, parameters, scope, isMulti);

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


                    default:
                        {
                            if (token.kind == TokenKind.Type)
                            {
                                var type = TypeFromToken(token);
                                var varName = ExpectIdentifier();

                                var varDecl = new VarDeclaration(module.Scope, varName, type, VarStorage.Global);

                                ExpectToken(";");
                                module.Scope.AddVariable(varDecl);

                                break;
                            }
                            else
                            {
                                throw new CompilerException("unexpected token: " + token.value);
                            }
                        }
                }

            } while (true);
        }

        private VarType TypeFromToken(LexerToken token)
        {
            if (token.value.Contains("int"))
            {
                return VarType.Find(VarKind.Number);
            }

            switch (token.value)
            {
                case "string":
                    return VarType.Find(VarKind.String);

                case "address":
                    return VarType.Find(VarKind.Address);

                case "bool":
                    return VarType.Find(VarKind.Bool);

                default:
                    throw new CompilerException("Unknown or unsupported type: " + token.value);
            }
        }

        protected override VarType ExpectType()
        {
            var token = FetchToken();
            return TypeFromToken(token);
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

                var type = ExpectType();
                var name = ExpectIdentifier();

                list.Add(new MethodParameter(name, type));

            } while (true);


            return list.ToArray();
        }

        private string[] ParseFunctionAttributes(out VarType returnType)
        {
            var list = new List<string>();

            returnType = VarType.Find(VarKind.None);

            do
            {
                var token = FetchToken();

                if (token.value == "returns")
                {
                    ExpectToken("(");
                    returnType = ExpectType();
                    ExpectToken(")");
                    break;
                }
                else
                if (token.value == "{")
                {
                    Rewind();
                    break;
                }
                else
                {
                    switch (token.value)
                    {
                        // case "payable": NOTE this one makes no sense in TOMB
                        case "view":
                        case "pure":
                        case "public":
                        case "private":
                            list.Add(token.value);
                            break;

                        default:
                            throw new CompilerException("Invalid or unsupported function attribute: " + token.value);
                    }
                }

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

                            if (!method.@interface.IsMulti || expr == null)
                            {
                                terminateEarly = true;
                            }
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
                                bool skipBraces = false;

                                var ahead = FetchToken();
                                if (ahead.value == "if")
                                {
                                    skipBraces = true;
                                }

                                Rewind();

                                if (!skipBraces) ExpectToken("{");

                                ifCommand.@else = ParseCommandBlock(ifCommand.Scope, method);

                                if (!skipBraces) ExpectToken("}");
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

                    case "for":
                        {
                            var forCommand = new ForStatement(scope);

                            ExpectToken("(");

                            ExpectToken("local");

                            AssignStatement initCmd;
                            forCommand.loopVar = ParseVariableDeclaration(scope, out initCmd);
                            if (initCmd == null)
                            {
                                throw new CompilerException("variable missing initialization statement in for loop");
                            }
                            else
                            {
                                forCommand.initStatement = initCmd;
                            }

                            forCommand.condition = ExpectExpression(scope);

                            if (forCommand.condition.ResultType.Kind != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(";");

                            var varName = ExpectIdentifier();
                            if (varName != forCommand.loopVar.Name)
                            {
                                throw new CompilerException($"expected variable {varName} (temporary compiler limitation, no complex for statements supported!)");
                            }

                            var next = FetchToken();

                            if (next.kind == TokenKind.Postfix)
                            {
                                forCommand.loopStatement = BuildPostfixStatement(scope, varName, next.value);
                            }
                            else
                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                forCommand.loopStatement = BuildBinaryShortAssigment(scope, varName, next.value);
                            }
                            else
                            {
                                throw new CompilerException($"expected simple statement using {varName} (temporary compiler limitation, no complex for statements supported!)");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            forCommand.body = ParseCommandBlock(forCommand.Scope, method);

                            ExpectToken("}");

                            block.Commands.Add(forCommand);
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

                            if (next.kind == TokenKind.Postfix)
                            {
                                var setCommand = BuildPostfixStatement(scope, token.value, next.value);
                                block.Commands.Add(setCommand);
                            }
                            else
                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                var setCommand = BuildBinaryShortAssigment(scope, token.value, next.value);
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
                                        assignment.keyExpression = new LiteralExpression(scope, "\"" + fieldName + "\"", fieldDecl.type);
                                        assignment.valueExpression = ParseAssignmentExpression(scope, next.value, varDecl, fieldDecl.type);
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

        private VarDeclaration ParseVariableDeclaration(Scope scope, out AssignStatement assignment)
        {
            var varName = ExpectIdentifier();

            var tmp = FetchToken();
            VarType type = null;

            if (tmp.value == ":")
            {
                type = ExpectType();
            }
            else
            {
                Rewind();
            }

            var next = FetchToken();

            Expression initExpr;
            if (next.kind == TokenKind.Operator)
            {
                if (next.value == Lexer.AssignmentOperator)
                {
                    initExpr = ExpectExpression(scope);

                    if (type == null)
                    {
                        type = initExpr.ResultType;
                    }
                }
                else
                {
                    throw new CompilerException($"Expected {Lexer.AssignmentOperator} in variable initialization");
                }
            }
            else
            if (type == null)
            {
                throw new CompilerException($"Type for variable {varName} must be specified!");
            }
            else
            {
                initExpr = null;
                Rewind();
            }

            var varDecl = new VarDeclaration(scope, varName, type, VarStorage.Local);

            scope.AddVariable(varDecl);

            if (initExpr != null)
            {
                var initCmd = new AssignStatement();
                initCmd.variable = varDecl;
                initCmd.valueExpression = initExpr;
                assignment = initCmd;
            }
            else
            {
                assignment = null;
            }

            ExpectToken(";");

            return varDecl;
        }

    }
}

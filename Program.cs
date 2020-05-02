using Phantasma.Contracts;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TombCompiler
{
    public enum VarKind
    {
        Unknown,
        None,
        Number,
        Bool,
        String,
        Timestamp,
        Address,
        Bytes,
        Task
    }

    public enum VarStorage
    {
        Global,
        Local,
        Argument,
    }

    public enum OperatorKind
    {
        Unknown,
        Assignment,
        Equal,
        Different,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        Addition,
        Subtraction,
        Multiplication,
        Division,
    }

    public enum MethodKind
    {
        Method,
        Constructor,
        Task,
        Trigger,
    }

    public static class Extensions
    {
        public static string UppercaseFirst(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
        public static bool IsLogicalOperator(this OperatorKind op)
        {
            return op != OperatorKind.Unknown && op < OperatorKind.Addition;
        }
    }

    public class Register
    {
        public readonly int Index;
        public readonly string Alias;

        public readonly static Register Temporary = new Register(0, null);

        public Register(int index, string alias = null)
        {
            Index = index;
            Alias = alias;
        }

        public override string ToString()
        {
            if (Alias != null)
            {
                return "$" + Alias;

            }
            return "r"+Index;
        }
    }

    public abstract class Declaration: CodeNode
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

        public VarDeclaration(Scope parentScope,  string name, VarKind kind, VarStorage storage) : base(parentScope, name)
        {
            this.Kind = kind;
            this.Storage = storage;
        }

        public override string ToString()
        {
            return $"var {Name}:{Kind}";
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return node == this;
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

        public override bool IsNodeUsed(CodeNode node)
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

        public void AddMethod(string name, VarKind returnType, ParameterDeclaration[] parameters)
        {
            /*if (name != name.ToLower())
            {
                throw new CompilerException(parser, "invalid method name: " + name);
            }*/
                      
            var method = new MethodInterface(this, name, MethodKind.Method, returnType, parameters);
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

        public override bool IsNodeUsed(CodeNode node)
        {
            return node == this;
        }
    }

    public class ParameterDeclaration
    {
        public string Name;
        public VarKind Kind;

        public ParameterDeclaration(string name, VarKind kind)
        {
            Name = name;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"{Name}:{Kind}";
        }
    }

    public class MethodInterface
    {
        public string Name;
        public LibraryDeclaration Library;
        public MethodKind Kind;
        public VarKind ReturnType;
        public ParameterDeclaration[] Parameters;

        public MethodInterface(LibraryDeclaration library, string name, MethodKind kind, VarKind returnType, ParameterDeclaration[] parameters) 
        {
            this.Name = name;
            this.Library = library;
            this.Kind = kind;
            this.ReturnType = returnType;
            this.Parameters = parameters;
        }

        public override string ToString()
        {
            return $"method {Name}:{ReturnType}";
        }
    }

    public class CodeGenerator
    {
        private StringBuilder _sb = new StringBuilder();

        public static Scope currentScope = null;
        public static int currentLine = 0;

        public static string Tabs(int n)
        {
            return new string('\t', n);
        }

        public void AppendLine(CodeNode node, string line = "")
        {
            if (node.LineNumber <= 0)
            {
                throw new CompilerException("line number failed for " + node.GetType().Name);
            }

            while (currentLine <= node.LineNumber)
            {
                if (currentLine > 0)
                {
                    var lineContent = Parser.Instance.GetLine(currentLine);
                    _sb.Append($"// Line {currentLine}:" + lineContent);
                }
                currentLine++;
            }

            line = Tabs(currentScope.Level) + line;
            _sb.AppendLine(line);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }
    }

    public abstract class CodeNode
    {
        public int LineNumber;
        public int Column;
        public string NodeID;

        public CodeNode()
        {
            this.LineNumber = Parser.Instance.CurrentLine;
            this.Column = Parser.Instance.CurrentColumn;
            this.NodeID = this.GetType().Name.ToLower() + Parser.Instance.AllocateLabel();
        }

        public abstract bool IsNodeUsed(CodeNode node);
    }

    public abstract class CommandNode: CodeNode
    {
        public abstract void GenerateCode(CodeGenerator output);
    }

    public class CommandBlock : CodeNode
    {
        public readonly List<CommandNode> Commands = new List<CommandNode>();

        public Scope ParentScope { get; }

        public CommandBlock(Scope scope) : base()
        {
            this.ParentScope = scope;
        }

        public void GenerateCode(CodeGenerator output)
        {
            foreach (var cmd in Commands)
            {
                cmd.GenerateCode(output);
            }
        }
        public override bool IsNodeUsed(CodeNode node)
        {
            if (node == this)
            {
                return true;
            }

            foreach (var cmd in Commands)
            {
                if (cmd.IsNodeUsed(node))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class Scope
    {
        public readonly Scope Parent;
        public readonly Contract Root;
        public readonly string Name;

        public int Level
        {
            get
            {
                if (Parent != null)
                {
                    return Parent.Level + 1;
                }

                return 0;
            }
        }

        public Scope(Scope parent, string name, ParameterDeclaration[] parameters)
        {
            this.Parent = parent;
            this.Root = parent.Root;
            this.Name = name;

            foreach (var entry in parameters)
            {
                var varDecl = new VarDeclaration(this, entry.Name, entry.Kind, VarStorage.Argument);
                this.AddVariable( varDecl);
            }
        }

        public Scope(Scope parent, string name): this(parent, name, new ParameterDeclaration[0])
        {
        }

        public Scope(Contract contract)
        {
            this.Parent = null;
            this.Root = contract;
            this.Name = contract.Name;
        }

        public override string ToString()
        {
            if (Parent != null)
            {
                return Parent.ToString() + "=>" + Name;
            }

            return Name;
        }

        public readonly Dictionary<string, VarDeclaration> Variables = new Dictionary<string, VarDeclaration>();
        public readonly Dictionary<string, ConstDeclaration> Constants = new Dictionary<string, ConstDeclaration>();
        public readonly List<MethodInterface> Methods = new List<MethodInterface>();

        public void AddVariable(VarDeclaration decl)
        {
            if (Variables.ContainsKey(decl.Name))
            {
                throw new CompilerException("duplicated declaration: " + decl.Name);
            }

            if (decl.Name == decl.Name.ToUpper())
            {
                throw new CompilerException("invalid variable name: " + decl.Name);
            }

            Variables[decl.Name] = decl;
        }

        public void AddConstant(ConstDeclaration decl)
        {
            if (Constants.ContainsKey(decl.Name))
            {
                throw new CompilerException("duplicated declaration: " + decl.Name);
            }

            if (decl.Name != decl.Name.ToUpper())
            {
                throw new CompilerException("invalid constant name: " + decl.Name);
            }

            Constants[decl.Name] = decl;
        }

        public VarDeclaration FindVariable(string name, bool required = true)
        {
            if (Variables.ContainsKey(name))
            {
                return Variables[name];
            }

            if (Parent != null)
            {
                return Parent.FindVariable(name, required);
            }

            if (required)
            {
                throw new CompilerException("variable not declared: " + name);
            }

            return null;
        }

        public ConstDeclaration FindConstant( string name, bool required = true)
        {
            if (Constants.ContainsKey(name))
            {
                return Constants[name];
            }

            if (Parent != null)
            {
                return Parent.FindConstant(name, required);
            }

            if (required)
            {
                throw new CompilerException("constant not declared: " + name);
            }

            return null;
        }

        private Scope previousScope;

        public void Enter(CodeGenerator output)
        {
            previousScope = CodeGenerator.currentScope;
            CodeGenerator.currentScope = this;

            Console.WriteLine("entering " + this.Name);

            /*foreach (var variable in this.Variables.Values)
            {
                variable.Register = Parser.Instance.AllocRegister(output, variable, variable.Name);
            }*/
        }

        public void Leave(CodeGenerator output)
        {
            Console.WriteLine("leaving " + this.Name);

            foreach (var variable in this.Variables.Values)
            {
                if (variable.Storage == VarStorage.Global)
                {
                    continue;
                }

                if (variable.Register == null)
                {
                    throw new CompilerException("unused variable: " + variable.Name);
                }

                Parser.Instance.DeallocRegister(variable.Register);
                variable.Register = null;
            }

            CodeGenerator.currentScope = previousScope;
        }
    }

    #region LEXER
    public enum TokenKind
    {
        Separator,
        Operator,
        Selector,
        Identifier,
        Type,
        String,
        Number,
        Bool,
        Address,
        Bytes,
    }

    public struct LexerToken
    {
        public readonly int column;
        public readonly int line;
        public readonly string value;
        public readonly TokenKind kind;

        public LexerToken(int column, int line, string value)
        {
            this.column = column;
            this.line = line;
            this.value = value;

            if (value.StartsWith("0x"))
            {
                this.kind = TokenKind.Bytes;
            }
            else
            if (value.StartsWith("@"))
            {
                this.kind = TokenKind.Address;
                this.value = value.Substring(1);

                Address addr;

                if (this.value.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    addr = Address.Null;
                }
                else
                {
                    addr = Address.FromText(value);
                }

                this.value = "0x"+Base16.Encode(addr.ToByteArray());
            }
            else
            if (value == "true" || value == "false")
            {
                this.kind = TokenKind.Bool;
            }
            else
            if (value == ".")
            {
                this.kind = TokenKind.Selector;
            }
            else
            if (value == ":=")
            {
                this.kind = TokenKind.Operator;
            }
            else
            if (BigInteger.IsParsable(value))
            {
                this.kind = TokenKind.Number;
            }
            else
            {
                var first = value[0];

                switch (first)
                {
                    case '\'':
                        this.kind = TokenKind.String;
                        break;

                    default:
                        if (Lexer.IsSeparatorSymbol(first))
                        {
                            this.kind = TokenKind.Separator;
                        }
                        else
                        if (Lexer.IsOperatorSymbol(first))
                        {
                            this.kind = TokenKind.Operator;
                        }
                        else
                        {
                            this.kind = TokenKind.Identifier;

                            foreach (var varType in Lexer.VarTypeNames)
                            {
                                if (varType == this.value)
                                {
                                    this.kind = TokenKind.Type;
                                    break;
                                }
                            }
                        }
                        break;
                }
            }

        }

        public override string ToString()
        {
            return $"{kind} => {value}";
        }
    }

    public static class Lexer
    {
        public readonly static string[] VarTypeNames = Enum.GetNames(typeof(VarKind)).Cast<string>().Select(x => x.ToLower()).ToArray();

        internal static bool IsOperatorSymbol(char ch)
        {
            switch (ch)
            {
                case '=':
                case '+':
                case '-':
                case '*':
                case '/':
                case '<':
                case '>':
                case '!':
                case ':':
                    return true;

                default:
                    return false;
            }
        }

        internal static bool IsSeparatorSymbol(char ch)
        {
            switch (ch)
            {
                case ';':
                case ',':
                case '{':
                case '}':
                case '(':
                case ')':
                case '.':
                    return true;

                default:
                    return false;
            }
        }

        public static List<LexerToken> Process(string sourceCode)
        {
            var tokens = new List<LexerToken>();
            int i = 0;

            int tokenX = 0;
            int tokenY = 0;

            int col = 0;
            int line = 1;
            var sb = new StringBuilder();

            bool insideString = false;
            bool insideComment = false;

            int lastType = -1;
            char lastChar = '\0';
            while (i < sourceCode.Length)
            {
                var ch = sourceCode[i];
                i++;
                col++;

                if (insideComment)
                {
                    if (ch == '\n')
                    {
                        insideComment = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (ch == '/' && lastChar == ch)
                {
                    sb.Length--;
                    insideComment = true;
                    continue;
                }

                switch (ch)
                {
                    case '\t':
                    case ' ':
                        if (insideString)
                        {
                            sb.Append(ch);
                        }
                        else
                        {
                            lastType = -1;
                        }

                        break;

                    case '\r':
                        break;

                    case '\n':
                        col = 0;
                        line++;
                        break;

                    default:
                        int curType;

                        if (ch == '/')
                        {
                            curType =0;
                        }
                        
                        if (ch == '\'')
                        {
                            insideString = !insideString;
                            curType = 0;
                        }
                        else
                        if (insideString)
                        {
                            curType = 0;
                        }
                        else
                        if (IsSeparatorSymbol(ch))
                        {
                            curType = 2;
                        }
                        else
                        if (IsOperatorSymbol(ch))
                        {
                            curType = 1;
                        }
                        else
                        {
                            curType = 0;
                        }

                        if (sb.Length > 0 && curType != lastType)
                        {
                            var val = sb.ToString();
                            tokens.Add(new LexerToken(tokenX, tokenY, val));
                            sb.Clear();
                        }

                        if (sb.Length == 0)
                        {
                            tokenX = col;
                            tokenY = line;
                        }

                        sb.Append(ch);

                        if (curType == 2)
                        {
                            curType = -1;
                        }

                        lastType = curType;
                        break;
                }

                lastChar = ch;
            }

            if (sb.Length > 0)
            {
                var val = sb.ToString();
                tokens.Add(new LexerToken(tokenX, tokenY, val));
            }

            return tokens;
        }
    }
    #endregion

    public class MethodDeclaration: Declaration
    {
        public readonly MethodInterface @interface;
        public readonly CommandBlock body;
        public readonly Scope scope;

        public MethodDeclaration(Scope scope, MethodInterface @interface, CommandBlock body) : base(scope.Parent, @interface.Name)
        {
            this.body = body;
            this.scope = scope;
            this.@interface = @interface;
        }

        public override bool IsNodeUsed(CodeNode node)
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
                    output.AppendLine(this, $"LOAD {tempReg1} 'Data.Get'");
                }

                var reg = Parser.Instance.AllocRegister(output, variable, variable.Name);
                variable.Register = reg;

                if (isConstructor)
                {
                    continue; // in a constructor we don't need to read the vars from storage as they dont exist yet
                }

                var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name);

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
                    if (isConstructor)
                    {
                        throw new CompilerException("global variable not assigned in constructor: " + variable.Name);
                    }

                    continue; // if we hit this, means it went unused 
                }

                if (tempReg1 == null)
                {
                    tempReg1 = Parser.Instance.AllocRegister(output, this);
                    output.AppendLine(this, $"LOAD {tempReg1} 'Data.Set'");
                }

                var fieldKey = SmartContract.GetKeyForField(this.scope.Root.Name, variable.Name);

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
    }

    public class Contract : CodeNode
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

        public override bool IsNodeUsed(CodeNode node)
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

        public void GenerateCode(CodeGenerator output)
        {
            this.Scope.Enter(output);

            {
                var reg = Parser.Instance.AllocRegister(output, this, "methodName");
                output.AppendLine(this, $"POP {reg}");
                foreach (var entry in Methods.Values)
                {
                    output.AppendLine(this, $"LOAD r0, '{entry.Name}'");
                    output.AppendLine(this, $"CMP r0, {reg}");
                    output.AppendLine(this, $"JMPIF r0, @{entry.GetEntryLabel()}");
                }
                Parser.Instance.DeallocRegister(reg);
                output.AppendLine(this, "THROW 'unknown method was called'");
            }

            foreach (var entry in Methods.Values)
            {
                entry.GenerateCode(output);
            }

            this.Scope.Leave(output);
        }

        public void AddMethod(int line, string name, MethodKind kind, VarKind returnType, ParameterDeclaration[] parameters, Scope scope, CommandBlock body)
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
                    libDecl.AddMethod("getAddress", VarKind.Address, new ParameterDeclaration[] { });
                    break;

                case "Runtime":
                    libDecl.AddMethod("log", VarKind.None, new[] { new ParameterDeclaration("message", VarKind.String) });
                    libDecl.AddMethod("isWitness", VarKind.Bool, new[] { new ParameterDeclaration("address", VarKind.Address) });
                    libDecl.AddMethod("getTime", VarKind.Timestamp, new ParameterDeclaration[] { });
                    libDecl.AddMethod("startTask", VarKind.None, new ParameterDeclaration[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("taskName", VarKind.String) });
                    libDecl.AddMethod("stopTask", VarKind.None, new ParameterDeclaration[] { });
                    break;

                case "Token":
                    libDecl.AddMethod("create", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String), new ParameterDeclaration("name", VarKind.String), new ParameterDeclaration("maxSupply", VarKind.Number), new ParameterDeclaration("decimals", VarKind.Number), new ParameterDeclaration("flags", VarKind.Number), new ParameterDeclaration("script", VarKind.Bytes) });
                    libDecl.AddMethod("transfer", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("to", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String), new ParameterDeclaration("amount", VarKind.Number) });
                    libDecl.AddMethod("transferAll", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("to", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String) });
                    libDecl.AddMethod("mint", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("to", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String), new ParameterDeclaration("amount", VarKind.Number) });
                    libDecl.AddMethod("burn", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String), new ParameterDeclaration("amount", VarKind.Number) });
                    libDecl.AddMethod("swap", VarKind.None, new[] { new ParameterDeclaration("targetChain", VarKind.String), new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("to", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String), new ParameterDeclaration("amount", VarKind.Number) });
                    libDecl.AddMethod("getBalance", VarKind.Number, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("symbol", VarKind.String) });
                    break;

                case "Organization":
                    libDecl.AddMethod("create", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("id", VarKind.String), new ParameterDeclaration("name", VarKind.String), new ParameterDeclaration("script", VarKind.Bytes) });
                    libDecl.AddMethod("addMember", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("name", VarKind.String), new ParameterDeclaration("target", VarKind.Address)});
                    break;

                case "Leaderboard":
                    libDecl.AddMethod("create", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("boardName", VarKind.String), new ParameterDeclaration("capacity", VarKind.Number) });
                    libDecl.AddMethod("getAddress", VarKind.Address, new[] { new ParameterDeclaration("index", VarKind.Number), new ParameterDeclaration("boardName", VarKind.String) });
                    libDecl.AddMethod("getScore", VarKind.Number, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("boardName", VarKind.String) });
                    libDecl.AddMethod("insertScore", VarKind.Number, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("target", VarKind.Address), new ParameterDeclaration("boardName", VarKind.String), new ParameterDeclaration("score", VarKind.Number)});
                    libDecl.AddMethod("reset", VarKind.None, new[] { new ParameterDeclaration("from", VarKind.Address), new ParameterDeclaration("boardName", VarKind.String)});
                    libDecl.AddMethod("getSize", VarKind.Number, new[] { new ParameterDeclaration("boardName", VarKind.String) });
                    break;

                default:
                    throw new CompilerException("unknown library: " + name);
            }

            Libraries[name] = libDecl;
            return libDecl;
        }

        public string Compile()
        {
            var sb = new CodeGenerator();
            this.GenerateCode(sb);

            Parser.Instance.VerifyRegisters();

            return sb.ToString();
        }
    }

    public abstract class Expression :CodeNode
    {
        public abstract VarKind ResultType { get; }
        public Scope ParentScope { get; }

        public Expression(Scope parentScope) : base()
        {
            this.ParentScope = parentScope;
        }

        public abstract Register GenerateCode(CodeGenerator output);
    }

    public class NegationExpression: Expression
    {
        public Expression expr;
        public override VarKind ResultType => VarKind.Bool;

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

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this) || expr.IsNodeUsed(node);
        }
    }

    public class BinaryExpression : Expression
    {
        private OperatorKind op;
        private Expression left;
        private Expression right;

        public override VarKind ResultType => op.IsLogicalOperator() ? VarKind.Bool: left.ResultType;

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

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this) || left.IsNodeUsed(node) || right.IsNodeUsed(node);
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var regLeft = left.GenerateCode(output);
            var regRight = right.GenerateCode(output);

            Opcode opcode;
            switch (this.op)
            {
                case OperatorKind.Addition: opcode = Opcode.ADD; break;
                case OperatorKind.Subtraction: opcode = Opcode.SUB; break;
                case OperatorKind.Multiplication: opcode = Opcode.MUL; break;
                case OperatorKind.Division: opcode = Opcode.DIV; break;

                case OperatorKind.Equal: opcode = Opcode.EQUAL; break;
                case OperatorKind.Less: opcode = Opcode.LT; break;
                case OperatorKind.LessOrEqual: opcode = Opcode.LTE; break;
                case OperatorKind.Greater: opcode = Opcode.GT; break;
                case OperatorKind.GreaterOrEqual: opcode = Opcode.GTE; break;

                default:
                    throw new CompilerException("not implemented vmopcode for "+op);
            }

            output.AppendLine(this, $"{opcode} {regLeft} {regLeft} {regRight}");

            Parser.Instance.DeallocRegister(regRight);

            return regLeft;
        }

        public override string ToString()
        {
            return $"{left} {op} {right}";
        }
    }

    public class MethodExpression: Expression
    {
        public MethodInterface method;
        public List<Expression> arguments = new List<Expression>();

        public override VarKind ResultType => method.ReturnType;

        public MethodExpression(Scope parentScope): base(parentScope)
        {

        }

        public override bool IsNodeUsed(CodeNode node)
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

        public override Register GenerateCode(CodeGenerator output)
        {
            foreach (var arg in arguments)
            {
                var argReg = arg.GenerateCode(output);
                output.AppendLine(arg, $"PUSH {argReg}");
                Parser.Instance.DeallocRegister(argReg);
            }

            var reg = Parser.Instance.AllocRegister(output, this, this.NodeID);
            output.AppendLine(this, $"LOAD {reg} '{this.method.Library.Name}.{this.method.Name}'");
            output.AppendLine(this, $"EXTCALL {reg}");
            output.AppendLine(this, $"POP {reg}");
            return reg;
        }
    }

    public class LiteralExpression : Expression
    {
        public string value;
        public VarKind kind;

        public LiteralExpression(Scope parentScope, string value, VarKind kind): base(parentScope)
        {
            this.value = value;
            this.kind = kind;
        }

        public override string ToString()
        {
            return "literal: " + value;
        }

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Parser.Instance.AllocRegister(output, this, this.NodeID);
            output.AppendLine(this, $"LOAD {reg} {this.value}");
            return reg;
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this);
        }

        public override VarKind ResultType => kind;
    }

    public class VarExpression : Expression
    {
        public VarDeclaration decl;

        public VarExpression(Scope parentScope, VarDeclaration declaration): base(parentScope)
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

            var reg = Parser.Instance.AllocRegister(output, this, decl.Name);            
            output.AppendLine(this, $"MOVE {reg} {decl.Register}");
            return reg;
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this) || node == decl;
        }

        public override VarKind ResultType => decl.Kind;
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

        public override Register GenerateCode(CodeGenerator output)
        {
            var reg = Parser.Instance.AllocRegister(output, this, decl.Name);
            output.AppendLine(this, $"LOAD {reg} {decl.Value}");
            return reg;
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this) || node == decl;
        }

        public override VarKind ResultType => decl.Kind;
    }

    public class AssignCommand : CommandNode
    {
        public VarDeclaration variable;
        public Expression expression;

        public AssignCommand(): base()
        {
        
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this)  || variable.IsNodeUsed(node) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            if (variable.Register == null)
            {
                variable.Register = Parser.Instance.AllocRegister(output, variable, variable.Name);
            }

            var srcReg = expression.GenerateCode(output);
            output.AppendLine(this, $"MOVE {variable.Register} {srcReg}");
            Parser.Instance.DeallocRegister(srcReg);
        }
    }

    public class ReturnCommand : CommandNode
    {
        public ReturnCommand() : base()
        {
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this, "RET");
        }
    }

    public class ThrowCommand : CommandNode
    {
        public readonly string message;

        public ThrowCommand(string msg) : base()
        {
            this.message = msg;
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            output.AppendLine(this, $"THROW {message}");
        }
    }

    public class IfCommand : CommandNode
    {
        public Expression condition;
        public CommandBlock body;
        public CommandBlock @else;
        public Scope Scope { get; }

        private int label;

        public IfCommand(Scope parentScope)
        {
            this.Scope = new Scope(parentScope, this.NodeID);
            this.label = Parser.Instance.AllocateLabel();
        }

        public override bool IsNodeUsed(CodeNode node)
        {
            if (@else != null && @else.IsNodeUsed(node))
            {
                return true;
            }

            return (node == this) || condition.IsNodeUsed(node) || body.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = condition.GenerateCode(output);

            this.Scope.Enter(output);
            if (@else != null)
            {
                output.AppendLine(this, $"JMPNOT @else_{this.NodeID} {reg}");
                body.GenerateCode(output);
                output.AppendLine(this, $"JMP @then_{this.NodeID}");
                output.AppendLine(this, $"@else_{this.NodeID}: NOP");
                @else.GenerateCode(output);
            }
            else
            {
                output.AppendLine(this, $"JMPNOT @then_{this.NodeID} {reg}");
                body.GenerateCode(output);
            }
            output.AppendLine(this, $"@then_{this.NodeID}: NOP");
            this.Scope.Leave(output);

            Parser.Instance.DeallocRegister(reg);

        }
    }

    public class MethodCallCommand : CommandNode
    {
        public MethodExpression expression;
        
        public MethodCallCommand() : base()
        {
            
        }
        public override bool IsNodeUsed(CodeNode node)
        {
            return (node == this) || expression.IsNodeUsed(node);
        }

        public override void GenerateCode(CodeGenerator output)
        {
            var reg = expression.GenerateCode(output);
            Parser.Instance.DeallocRegister(reg);
        }
    }

    public class Parser
    {
        private List<LexerToken> tokens;
        private int tokenIndex = 0;

        private int currentLabel = 0;

        public int CurrentLine { get; private set; }
        public int CurrentColumn { get; private set; }

        private string[] lines;

        public static Parser Instance { get; private set; }

        public Parser()
        {
            Instance = this;
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

        private void ExpectToken(string val)
        {
            var token = FetchToken();

            if (token.value != val)
            {
                throw new CompilerException("expected " + val);
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

        private VarKind ExpectType()
        {
            var token = FetchToken();

            if (token.kind != TokenKind.Type)
            {
                throw new CompilerException("expected type, got " + token.kind);
            }

            return (VarKind)Enum.Parse(typeof(VarKind), token.value, true);
        }

        public string GetLine(int index)
        {
            if (index <= 0 || index > lines.Length)
            {
                return "";
            }

            return lines[index-1];
        }

        public Contract Parse(string sourceCode)
        {
            this.tokens = Lexer.Process(sourceCode);

            /*foreach (var token in tokens)
            {
                Console.WriteLine(token);
            }*/

            ExpectToken("contract");
            var contractName = ExpectIdentifier();

            this.lines = sourceCode.Split('\n');

            var contractBlock = new Contract(contractName);
            ExpectToken("{");
            ParseContractBlock(contractBlock);
            ExpectToken("}");
            return contractBlock;
        }

        private void ParseContractBlock(Contract contract)
        {
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
                            var kind = ExpectType();
                            ExpectToken("=");

                            string constVal;

                            switch (kind)
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

                            var constDecl = new ConstDeclaration(contract.Scope, constName, kind, constVal);
                            contract.Scope.AddConstant(constDecl);
                            break;
                        }

                    case "global":
                        {                            
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var kind = ExpectType();
                            ExpectToken(";");

                            var varDecl = new VarDeclaration(contract.Scope, varName, kind, VarStorage.Global);
                            contract.Scope.AddVariable(varDecl);
                            break;
                        }

                    case "import":
                        {
                            var libName = ExpectIdentifier();
                            ExpectToken(";");

                            contract.AddLibrary(libName);
                            break;
                        }

                    case "constructor":
                        {
                            var line = this.CurrentLine;
                            var name = contract.Name + "()";
                            var parameters = ParseParameters(contract.Scope);
                            var scope = new Scope(contract.Scope, name, parameters);

                            ExpectToken("{");
                            var body = ParseCommandBlock(scope);
                            ExpectToken("}");
                            
                            contract.AddMethod(line, name, MethodKind.Constructor, VarKind.None, parameters, scope, body);
                            break;
                        }

                    case "method":
                        {
                            var line = this.CurrentLine;
                            var name = ExpectIdentifier();

                            var parameters = ParseParameters(contract.Scope);
                            var scope = new Scope(contract.Scope, name, parameters);

                            var returnType = VarKind.None;

                            var next = FetchToken();
                            if (next.value == ":")
                            {
                                returnType = ExpectType();
                            }
                            else
                            {
                                Rewind();
                            }

                            ExpectToken("{");
                            var body = ParseCommandBlock(scope);
                            ExpectToken("}");

                            contract.AddMethod(line, name, MethodKind.Method, returnType, parameters, scope, body);
                            break;
                        }

                    case "task":
                        {
                            var line = this.CurrentLine;
                            var name = ExpectIdentifier();

                            var parameters = ParseParameters(contract.Scope);
                            var scope = new Scope(contract.Scope, name, parameters);

                            ExpectToken("{");
                            var body = ParseCommandBlock(scope);
                            ExpectToken("}");

                            contract.AddMethod(line, name, MethodKind.Task, VarKind.None, parameters, scope, body);
                            break;
                        }

                    case "on":
                        {
                            var line = this.CurrentLine;
                            var name = ExpectIdentifier();

                            var parameters = ParseParameters(contract.Scope);
                            var scope = new Scope(contract.Scope, name, parameters);

                            ExpectToken("{");
                            var body = ParseCommandBlock(scope);
                            ExpectToken("}");

                            contract.AddMethod(line, name, MethodKind.Trigger, VarKind.None, parameters, scope, body);

                            break;
                        }

                    default:
                        throw new CompilerException("unexpected token: " + token.value);
                }

            } while (true);
        }

        private ParameterDeclaration[] ParseParameters(Scope scope)
        {
            var list = new List<ParameterDeclaration>();

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

                list.Add(new ParameterDeclaration(name, type));

            } while (true);


            return list.ToArray();
        }

        private CommandBlock ParseCommandBlock(Scope scope)
        {
            var block = new CommandBlock(scope);

            do
            {
                var token = FetchToken();

                switch (token.value)
                {
                    case "}":
                        Rewind();
                        return block;

                    case "return":
                        {
                            block.Commands.Add(new ReturnCommand());
                            ExpectToken(";");
                            break;
                        }

                    case "throw":
                        {
                            var msg = ExpectString();
                            block.Commands.Add(new ThrowCommand(msg));
                            ExpectToken(";");
                            break;
                        }

                    case "local":
                        {
                            var varName = ExpectIdentifier();
                            ExpectToken(":");
                            var kind = ExpectType();
                            ExpectToken(";");

                            var varDecl = new VarDeclaration(scope, varName, kind, VarStorage.Local);
                            scope.AddVariable(varDecl);
                            break;
                        }

                    case "if":
                        {
                            var ifCommand = new IfCommand(scope);

                            ExpectToken("(");
                            ifCommand.condition = ExpectExpression(scope);

                            if (ifCommand.condition.ResultType != VarKind.Bool)
                            {
                                throw new CompilerException($"condition must be boolean expression");
                            }

                            ExpectToken(")");

                            ExpectToken("{");

                            ifCommand.body = ParseCommandBlock(ifCommand.Scope);

                            ExpectToken("}");

                            var next = FetchToken();

                            if (next.value == "else")
                            {
                                ExpectToken("{");

                                ifCommand.@else = ParseCommandBlock(ifCommand.Scope);

                                ExpectToken("}");
                            }
                            else
                            {
                                Rewind();
                            }

                            block.Commands.Add(ifCommand);
                            break;
                        }

                    default:
                        if (token.kind == TokenKind.Identifier)
                        {
                            var next = FetchToken();

                            if (next.kind == TokenKind.Operator && next.value.EndsWith("="))
                            {
                                var setCommand = new AssignCommand();
                                setCommand.variable = scope.FindVariable(token.value);

                                var expr = ExpectExpression(scope);
                                if (next.value != ":=")
                                {
                                    var str = next.value.Substring(0, next.value.Length - 1);
                                    var op = ParseOperator(str);

                                    if (op == OperatorKind.Unknown)
                                    {
                                        throw new CompilerException("unknown operator: " + next.value);
                                    }

                                    expr = new BinaryExpression(scope, op, new VarExpression(scope, setCommand.variable), expr);
                                }

                                setCommand.expression = expr;

                                if (setCommand.expression.ResultType != setCommand.variable.Kind)
                                {
                                    throw new CompilerException($"expected {setCommand.variable.Kind} expression");
                                }

                                block.Commands.Add(setCommand);
                            }
                            else
                            if (next.kind == TokenKind.Selector)
                            {
                                var lib = scope.Root.FindLibrary(token.value);

                                var methodCall = new MethodCallCommand();
                                methodCall.expression = ParseMethodExpression(scope, lib);

                                block.Commands.Add(methodCall);
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
            } while (true);
        }

        private Expression ExpectExpression(Scope scope)
        {
            var expr = ParseExpression(scope);
            if (expr == null)
            {
                throw new CompilerException("expected expression");
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
                            var varDecl = scope.FindVariable(first.value, false);
                            if (varDecl != null)
                            {
                                return new VarExpression(scope, varDecl);
                            }

                            var libDecl = scope.Root.FindLibrary(first.value, false);
                            if (libDecl != null)
                            {
                                throw new NotImplementedException();
                            }

                            throw new CompilerException("unknown identifier: " + first.value);
                        }
                    }

                case TokenKind.Number:
                    {
                        return new LiteralExpression(scope, first.value, VarKind.Number);
                    }

                case TokenKind.String:
                    {
                        return new LiteralExpression(scope, first.value, VarKind.String);
                    }

                case TokenKind.Bool:
                    {
                        return new LiteralExpression(scope, first.value, VarKind.Bool);
                    }

                case TokenKind.Address:
                    {
                        return new LiteralExpression(scope, first.value, VarKind.Address);
                    }

                case TokenKind.Bytes:
                    {
                        return new LiteralExpression(scope, first.value, VarKind.Bytes);
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

                case "*":
                    return OperatorKind.Multiplication;

                case "+":
                    return OperatorKind.Addition;

                case "-":
                    return OperatorKind.Subtraction;

                default:
                    return OperatorKind.Unknown;
            }
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
                throw new CompilerException($"type mistmatch, {leftSide.ResultType} on left, {rightSide.ResultType} on right");
            }

            if (op == OperatorKind.Different)
            {
                var innerExpr = new BinaryExpression(scope, OperatorKind.Equal, leftSide, rightSide);

                return new NegationExpression(scope, innerExpr);
            }

            return new BinaryExpression(scope, op, leftSide, rightSide);
        }

        private Expression ParseExpression(Scope scope)
        {
            var first = FetchToken();
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
                        var rightSide = ExpectExpression(scope);
                        return ParseBinaryExpression(scope, second, leftSide, rightSide);
                    }

                case TokenKind.Selector:
                    {
                        var libDecl = scope.Root.FindLibrary(first.value);
                        var leftSide = ParseMethodExpression(scope, libDecl);

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
            }

            return null;
        }

        private MethodExpression ParseMethodExpression(Scope scope, LibraryDeclaration library)
        {
            var expr = new MethodExpression(scope);

            var methodName = ExpectIdentifier();

            expr.method = library.FindMethod(methodName);
            ExpectToken("(");

            var paramCount = expr.method.Parameters.Length;
            for (int i = 0; i < paramCount; i++)
            {
                if (i > 0)
                {
                    ExpectToken(",");
                }

                var arg = ExpectExpression(scope);
                expr.arguments.Add(arg);

                var expectedType = expr.method.Parameters[i].Kind;
                if (arg.ResultType != expectedType)
                {
                    throw new CompilerException($"expected argument of type {expectedType}, got {arg.ResultType} instead");
                }
            }

            ExpectToken(")");

            return expr;
        }

        private const int MaxRegisters = VirtualMachine.DefaultRegisterCount;
        private CodeNode[] registerAllocs = new CodeNode[MaxRegisters];
        private string[] registerAlias = new string[MaxRegisters];

        public Register AllocRegister(CodeGenerator generator, CodeNode node, string alias = null)
        {
            int baseRegister = 1;
            for (int i = baseRegister; i < registerAllocs.Length; i++)
            {
                if (registerAllocs[i] == null)
                {
                    registerAllocs[i] = node;
                    registerAlias[i] = alias;

                    string extra = alias != null ? " => " + alias : "";
                    Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "alloc r" + i + extra);

                    if (alias != null)
                    {
                        generator.AppendLine(node, $"ALIAS r{i} ${alias}");
                    }

                    return new Register(i, alias);
                }
            }

            throw new CompilerException("no more available registers");
        }

        public void DeallocRegister(Register register)
        {
            if (register == null)
            {
                return;
            }

            var index = register.Index;

            if (registerAllocs[index] != null)
            {
                var alias = registerAlias[index];

                Console.WriteLine(CodeGenerator.Tabs(CodeGenerator.currentScope.Level) + "dealloc r" + index);

                registerAllocs[index] = null;
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

    }

    public class CompilerException : Exception
    {
        public CompilerException(string msg) : base($"line {Parser.Instance.CurrentLine}: {msg}")
        {

        }

        public CompilerException(CodeNode node, string msg) : base($"line {node.LineNumber}: {msg}")
        {

        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var sourceFile = "code2.txt";
            Console.WriteLine("Opening " + sourceFile);
            var sourceCode = File.ReadAllText(sourceFile);

            Console.WriteLine("Parsing " + sourceFile);
            var parser = new Parser();
            var contract = parser.Parse(sourceCode);

            Console.WriteLine("Compiling " + sourceFile);
            var asm = contract.Compile();
            File.WriteAllText("asm.txt", asm);

            Console.WriteLine("Done, press any key");
            Console.ReadKey();
        }
    }
}

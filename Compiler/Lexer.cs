using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Tomb.AST;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Phantasma.Tomb
{
    public enum TokenKind
    {
        Invalid,
        Separator,
        Operator,
        Selector,
        Keyword,
        Identifier,
        Type,
        String,
        Number,
        Decimal,
        Bool,
        Address,
        Hash,
        Bytes,
        Asm,
        Macro,
        Postfix,
    }

    public struct LexerToken
    {
        public static readonly string AsmTag = ":ASM:";

        public readonly int column;
        public readonly int line;
        public readonly string value;
        public readonly TokenKind kind;

        public LexerToken(int column, int line, string value)
        {
            this.column = column;
            this.line = line;
            this.value = value;

            decimal decval;

            if (value == "|")
            {
                throw new Exception("bug");
            }

            if (value == "++" || value == "--")
            {
                this.kind = TokenKind.Postfix;
            }
            else
            if (Lexer.GetOperatorMatches(value) > 0)
            {
                this.kind = TokenKind.Operator;
            }
            else
            if (value.StartsWith("0x"))
            {
                this.kind = TokenKind.Bytes;
            }
            else
            if (value.StartsWith(AsmTag))
            {
                this.value = value.Substring(AsmTag.Length);
                this.kind = TokenKind.Asm;
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
                    addr = Address.FromText(this.value);
                }

                this.value = "0x" + Base16.Encode(addr.ToByteArray());
            }
            else
            if (value.StartsWith("#"))
            {
                this.kind = TokenKind.Hash;
                this.value = value.Substring(1);

                Hash hash = Hash.Parse(this.value);

                this.value = "0x" + hash.ToString();
            }
            else
            if (value.StartsWith("$"))
            {
                this.kind = TokenKind.Macro;
                this.value = value.Substring(1);
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
            if (int.TryParse(value, out int temp))
            {
                this.kind = TokenKind.Number;
            }
            else
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decval))
            {
                this.kind = TokenKind.Decimal;
            }
            else
            {
                var first = value.Length > 0 ? value[0] : '\0';

                switch (first)
                {
                    case '\"':
                        this.kind = TokenKind.String;
                        break;

                    default:
                        if (Lexer.IsSeparatorSymbol(first))
                        {
                            this.kind = TokenKind.Separator;
                        }
                        else
                        {
                            foreach (var varType in Lexer.VarTypeNames)
                            {
                                if (varType == this.value)
                                {
                                    this.kind = TokenKind.Type;
                                    return;
                                }
                            }

                            foreach (var word in Lexer.Keywords)
                            {
                                if (word == this.value)
                                {
                                    this.kind = TokenKind.Keyword;
                                    return;
                                }
                            }

                            // otherwise, fall back to identifier
                            if (Lexer.IsValidIdentifier(this.value))
                            {
                                this.kind = TokenKind.Identifier;
                                return;
                            }

                            if (!string.IsNullOrEmpty(this.value))
                            {
                                throw new CompilerException("Parsing failed, unsupported token: " + this.value);
                            }

                            this.kind = TokenKind.Invalid;
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
        public readonly static string[] Keywords = new string[]
        {
            "contract",
            "global",
            "local",
            "struct",
            "enum",
            "if",
            "else",
            "throw",
            "import",
            "const",
            "private",
            "public",
            "return",
            "script",
            "token",
            "nft",
            "code",
            //"description", // this should be a keyword, but contracts like Ghostmarket depend on it not being a keyword! 
            "constructor",
        };

        public readonly static string[] VarTypeNames = Enum.GetNames(typeof(VarKind)).Cast<string>().Select(x => x.ToLower()).ToArray();

        public readonly static string AssignmentOperator = "=";

        public readonly static string[] ComparisonOperators = new[] { "==", "!=", ">=", "<=", ">", "<" };
        public readonly static string[] ArithmeticOperators = new[] { "+", "-", "*", "/", "%" };
        public readonly static string[] LogicalOperators = new[] { "!", "^", "&&", "||" };
        public readonly static string[] BitshiftOperators = new[] { ">>", "<<" };
        public readonly static string[] CompoundAssigmentOperators = ArithmeticOperators.Select(x => x + "=").ToArray(); // generates +=, -= etc

        public readonly static string[] Operators = new[] { AssignmentOperator }.
            Concat(ComparisonOperators).
            Concat(ArithmeticOperators).
            Concat(LogicalOperators).
            Concat(BitshiftOperators).
            Concat(CompoundAssigmentOperators).
            ToArray();

        internal static int GetOperatorMatches(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                return 0;
            }

            int result = 0;

            for (int i = 0; i < Operators.Length; i++)
            {
                if (Operators[i].StartsWith(val))
                {
                    result++;
                }
            }

            return result;
        }

        internal static bool IsSeparatorSymbol(char ch)
        {
            switch (ch)
            {
                case ';':
                case ':':
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

        public enum CommentMode
        {
            None,
            Single,
            Multi
        }

        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            for (int i = 0; i < identifier.Length; i++)
            {
                var ch = identifier[i];

                if (char.IsLetter(ch) || ch == '_')
                {
                    continue;
                }

                if (i > 0 && char.IsDigit(ch))
                {
                    continue;
                }

                if (i > 0 && (ch == '[' || ch == ']')) // Array support
                {
                    continue;
                }

                return false;
            }

            if (Keywords.Contains(identifier))
            {
                return false;
            }

            return true;
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
            bool insideAsm = false;
            bool insideNumber = false;
            var insideComment = CommentMode.None;

            LexerToken prevToken = new LexerToken(0, 0, "");
            LexerToken curToken = prevToken;

            Action finishToken = () =>
            {
                if (sb.Length == 0)
                {
                    return;
                }

                var val = sb.ToString();

                prevToken = curToken;
                curToken = new LexerToken(tokenX, tokenY, val);
                tokens.Add(curToken);
                sb.Clear();

                insideNumber = false;

                if (val == "{" && prevToken.value == "asm")
                {
                    insideAsm = true;
                    sb.Append(LexerToken.AsmTag); // hack for lexer Token to detect this later as "asm"
                }

                tokenX = col;
                tokenY = line;
            };


            char lastChar = '\0';
            while (i < sourceCode.Length)
            {
                var ch = sourceCode[i];
                i++;
                col++;

                if (insideString)
                {
                    sb.Append(ch);

                    if (ch == '"')
                    {
                        insideString = false;
                    }

                    continue;
                }
                else
                if (insideAsm)
                {
                    if (ch == '}')
                    {
                        insideAsm = false;
                    }
                    else
                    {
                        if (ch == '\n')
                        {
                            line++;
                        }

                        sb.Append(ch);
                        continue;
                    }
                }
                else
                if (insideComment == CommentMode.Single)
                {
                    if (ch == '\n')
                    {
                        insideComment = CommentMode.None;
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                if (insideComment == CommentMode.Multi)
                {
                    if (ch == '/' && lastChar == '*')
                    {
                        insideComment = CommentMode.None;
                    }
                    else
                    {
                        if (ch == '\n')
                        {
                            line++;
                        }
                        lastChar = ch;
                    }
                    continue;
                }

                if (ch == '/' && lastChar == ch)
                {
                    sb.Length--;
                    insideComment = CommentMode.Single;
                    continue;
                }

                if (ch == '*' && lastChar == '/')
                {
                    sb.Length--;
                    insideComment = CommentMode.Multi;
                    continue;
                }

                switch (ch)
                {
                    case '\t':
                    case ' ':
                        {
                            finishToken();
                            break;
                        }

                    case '\r':
                        break;

                    case '\n':
                        col = 0;
                        line++;
                        break;

                    default:

                        if (ch == '\"')
                        {
                            finishToken();

                            sb.Append(ch);
                            insideString = true;
                        }
                        else
                        if (IsSeparatorSymbol(ch))
                        {
                            if (ch == '.' && insideNumber)
                            {
                                sb.Append(ch);
                            }
                            else
                            {
                                finishToken();
                                sb.Append(ch);

                                finishToken();
                            }
                        }
                        else
                        {
                            var prev = sb.ToString();
                            var prevOpMatches = GetOperatorMatches(prev);

                            var chOpMatches = GetOperatorMatches("" + ch);

                            if (prevOpMatches == 0 && chOpMatches > 0)
                            {
                                finishToken();
                            }
                            else
                            if (prevOpMatches > 0 && chOpMatches == 0)
                            {
                                if (!Operators.Contains(prev))
                                {
                                    throw new Exception("Possible operator bug in lexer?");
                                }

                                finishToken();
                            }

                            sb.Append(ch);

                            var tmp = sb.ToString();
                            var opMatches = insideNumber ? 0 : GetOperatorMatches(tmp);

                            if (opMatches == 1 && Operators.Contains(tmp))
                            {
                                finishToken();
                            }
                        }

                        if (sb.Length != 0 && char.IsDigit(ch))
                        {
                            insideNumber = true;
                        }

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
}

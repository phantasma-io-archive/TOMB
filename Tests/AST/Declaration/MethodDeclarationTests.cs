using NUnit.Framework;
using Phantasma.Tomb.Lexers;

namespace Tests.AST.Declaration;

public class MethodDeclarationTests
{
    [SetUp]
    public void Setup()
    {
        TombLangLexer lexer = new TombLangLexer();
    }
}
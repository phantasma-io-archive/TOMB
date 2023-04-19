using NUnit.Framework;
using Phantasma.Tomb.Lexers;

namespace Tests.AST.Declaration;

public class SetDeclarationTests
{
    [SetUp]
    public void Setup()
    {
        TombLangLexer lexer = new TombLangLexer();
    }
}
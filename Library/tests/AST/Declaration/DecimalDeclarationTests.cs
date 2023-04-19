using NUnit.Framework;
using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using Phantasma.Tomb.CodeGen;
using Phantasma.Tomb.Lexers;

namespace TOMBLib.Tests.AST.Declaration;

public class DecimalDeclarationTests
{
    [SetUp]
    public void Setup()
    {
        TombLangLexer lexer = new TombLangLexer();
    }
    
    [Test]
    public void DecimalDeclaration_Constructor_SetsProperties()
    {
        // Arrange
        var module = new Contract("myself", ModuleKind.Contract);
        var parentScope = new Scope(module);
        var name = "myDecimal";
        var type = VarType.Find(VarKind.Decimal, 2);
        var storage = VarStorage.Local;
        var value = "3.12";
        var decimals = 2;

        // Act
        var constDeclaration = new DecimalDeclaration(parentScope, name, decimals, storage);

        // Assert
        Assert.AreEqual(parentScope, constDeclaration.ParentScope);
        Assert.AreEqual(name, constDeclaration.Name);
        Assert.AreEqual(type, constDeclaration.Type);
        Assert.AreEqual(decimals, constDeclaration.Decimals);
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Utils;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class SwitchTest
{
    [Test]
    public void SwitchNumber()
    {
        var sourceCode =
            @"
contract test {
    public check(x:number): string {
        switch (x) {
            case 0: return ""zero"";
            case 1: return ""one"";
            case 2: return ""two"";
            default: return ""other"";
        }                  
     }}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var check = contract.abi.FindMethod("check");
        Assert.IsNotNull(check);

        // test different cases
        for (int i = -1; i <= 4; i++)
        {
            var vm = new TestVM(contract, storage, check);
            vm.Stack.Push(VMObject.FromObject(i));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);
            var result = vm.Stack.Pop().AsString();

            string expected;
            switch (i)
            {
                case 0: expected = "zero"; break;
                case 1: expected = "one"; break;
                case 2: expected = "two"; break;
                default: expected = "other"; break;
            }

            Assert.IsTrue(result == expected);
        }
    }

    [Test]
    public void TestSwitchString()
    {
        var sourceCode =
            @"
contract test {
    public check(x:string): number {
        switch (x) {
            case ""zero"": return 0;
            case ""one"": return 1;
            case ""two"": return 2;
            default: return 99;
        }                  
     }}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var check = contract.abi.FindMethod("check");
        Assert.IsNotNull(check);

        // test different cases
        for (int i = 0; i <= 4; i++)
        {
            var vm = new TestVM(contract, storage, check);
            var str = i.ToString();
            switch (i)
            {
                case 0: str = "zero"; break;
                case 1: str = "one"; break;
                case 2: str = "two"; break;
                default: str = "other"; break;
            }
            
            vm.Stack.Push(VMObject.FromObject(str));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);
            var result = vm.Stack.Pop().AsNumber();

            BigInteger expected = i;
            if ( i > 2 )
            {
                expected = 99;
            }
            
            Assert.AreEqual(expected, result);
        }
    }

    [Test]
    public void TestSwitchDecimal()
    {
        var sourceCode =
            @"
contract test {
    public check(x:decimal<2>): decimal<2> {
        switch (x) {
            case 1.0: return x;
            case 1.1: return x;
            default: return x;
        }                  
     }}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var check = contract.abi.FindMethod("check");
        Assert.IsNotNull(check);

        // test different cases
        for (int i = 0; i <= 4; i++)
        {
            var vm = new TestVM(contract, storage, check);
            decimal _value = 1.1m;
            switch (i)
            {
                case 0: _value = 1.00m; break;
                case 1: _value = 1.10m; break;
                case 2:  _value = 1.20m; break;
                default: _value = 1.50m; break;
            }
            
            vm.Stack.Push(VMObject.FromObject( _value));
            var state = vm.Execute();
            Assert.IsTrue(state == ExecutionState.Halt);
            var obj = vm.Stack.Pop();
            Assert.Fail(obj.AsNumber());
            
            // TODO: Finish this one.
            /* 
            var result = obj.AsNumber();

            decimal expected = _value;
            switch (i)
            {
                case 0: expected += 1 ; break;
                case 1: expected += 2; break;
                case 2:  expected += 3; break;
                default: expected += 5m; break;
            }*/
            
           // Assert.AreEqual(expected, result);
        }
    }
}
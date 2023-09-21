using System.Numerics;
using Nethereum.Util;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class StructTests
{
    private struct MyLocalStruct
    {
        public string name;
        public BigInteger age;
    }
    
    [Test]
    public void TestStructChanging()
    {
        var sourceCode =
            @"
struct MyLocalStruct {
    name:string;
    age:number;
}

contract test{
    import Struct;          
    public testMyStruct (name:string, age:number) : MyLocalStruct {
        local myStruct : MyLocalStruct = Struct.MyLocalStruct(name, age);
        if ( myStruct.age == 10 ) {
            myStruct.age = 20;
        }
        return myStruct;
    }
}";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;
        var method = contract.abi.FindMethod("testMyStruct");
        // Age 10
        var myStruct = new MyLocalStruct();
        myStruct.name = "John";
        myStruct.age = 10;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(myStruct.name));
        vm.Stack.Push(VMObject.FromObject(myStruct.age));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var returnObject = obj.AsStruct<MyLocalStruct>();
        Assert.AreEqual(myStruct.name,returnObject.name );
        Assert.AreEqual(myStruct.age, (BigInteger)20);
        
        myStruct.name = "BartSimpson";
        myStruct.age = 50;
        vm = new TestVM(contract, storage, method);
        vm.Stack.Push(VMObject.FromObject(myStruct.name));
        vm.Stack.Push(VMObject.FromObject(myStruct.age));
        result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);

        obj = vm.Stack.Pop();
        returnObject = obj.AsStruct<MyLocalStruct>();
        Assert.AreEqual(myStruct.name,returnObject.name );
        Assert.AreEqual(myStruct.age, returnObject.age);
    }
}
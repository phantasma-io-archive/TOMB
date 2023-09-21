using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Utils;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class EventTests
{
    [Test]
    public void TestEvent()
    {
        var sourceCode =
            @"
contract test {
	import Runtime;

	event MyPayment:number = ""{address} paid {data}""; // here we use a short-form description

	public paySomething(from:address, x:number)
	{
		local price: number = 10;
		local thisAddr:address = $THIS_ADDRESS;
		emit MyPayment(from, price);
	}
}
";
        
        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        var payStuff = contract.abi.FindMethod("paySomething");
        Assert.IsNotNull(payStuff);

        // Test for the 1st case
        var vm = new TestVM(contract, storage, payStuff);
        var keys = PhantasmaKeys.Generate();
        vm.Stack.Push(VMObject.FromObject((BigInteger)10));
        vm.Stack.Push(VMObject.FromObject(keys.Address));
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(vm.Stack.Count == 1);
    }
    
    
    [Test]
    public void TestEventWithDescription()
    {
	    var sourceCode =
		    @"
description payment_event {

	code(from:address, amount:number): string {
		local result:string = """";
		result += from;
		result += "" paid "";
		result += amount;
		return result;
	}
}

contract test {
	import Runtime;

	event MyPayment:number = payment_event; // here we use a short-form declaration


	public paySomething(from:address, x:number)
	{
		local price: number = 10;
		local thisAddr:address = $THIS_ADDRESS;
		emit MyPayment(from, price);
	}
}
";
	    
	     
	    var parser = new TombLangCompiler();
	    var contract = parser.Process(sourceCode).First();

	    var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

	    var payStuff = contract.abi.FindMethod("paySomething");
	    Assert.IsNotNull(payStuff);

	    // Test for the 1st case
	    var vm = new TestVM(contract, storage, payStuff);
	    var keys = PhantasmaKeys.Generate();
	    vm.Stack.Push(VMObject.FromObject((BigInteger)10));
	    vm.Stack.Push(VMObject.FromObject(keys.Address));
	    var result = vm.Execute();
	    Assert.IsTrue(result == ExecutionState.Halt);

	    Assert.IsTrue(vm.Stack.Count == 1);
	    
    }
}
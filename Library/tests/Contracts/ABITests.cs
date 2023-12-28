using Nethereum.Util;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Tomb.Compilers;

namespace TOMBLib.Tests.Contracts;

public class ABITests
{
    [Test]
    public void ABITest()
    {
        var sourceCode = @"
token MTEST {
	public getName(): string {
		return ""Unit test"";
	}
}

contract mytests {
	import Array;
	import ABI;
	import Module;
	public test():number {
		local myABI = Module.getABI(MTEST);
		local myMethod = ABI.getMethod(MTEST, ""getName"");
		return 0;	
	}	
}
";

        var parser = new TombLangCompiler();
        var contract = parser.Process(sourceCode).First();

        var storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        TestVM vm;

        var testMethod = contract.abi.FindMethod("test");
        Assert.IsNotNull(testMethod);

        var keys = PhantasmaKeys.Generate();

        vm = new TestVM(contract, storage, testMethod);
        var result = vm.Execute();
        Assert.IsTrue(result == ExecutionState.Halt);

        Assert.IsTrue(storage.Count == 1);

        Assert.IsTrue(vm.Stack.Count == 1);

        var obj = vm.Stack.Pop();
        var newVal = obj.AsString();
        var expectedVal = "P2KEYzWsbrMbPNtW1tBzzDKeYxYi4hjzpx4EfiyRyaoLkMM";

        Assert.IsTrue(newVal == expectedVal);
    }
}
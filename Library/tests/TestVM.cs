using Phantasma.Business.Blockchain.VM;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Tomb.CodeGen;
using System.Numerics;
using Phantasma.Core.Domain.Events.Structs;
using ExecutionContext = Phantasma.Core.Domain.Execution.ExecutionContext;

namespace TOMBLib.Tests;

public class TestVM : VirtualMachine
{
    private Module module;

    private Dictionary<string, Func<VirtualMachine, ExecutionState>> _interops =
        new Dictionary<string, Func<VirtualMachine, ExecutionState>>();

    private Func<string, ExecutionContext> _contextLoader;
    private Dictionary<string, ScriptContext> contexts;
    private Dictionary<byte[], byte[]> storage;

    public TestVM(Module module, Dictionary<byte[], byte[]> storage, ContractMethod method) : base(module.script,
        (uint)method.offset, module.Name)
    {
        this.module = module;
        this.storage = storage;
        RegisterContextLoader(ContextLoader);

        RegisterMethod("ABI()", ExtCalls.Constructor_ABI);
        RegisterMethod("Address()", ExtCalls.Constructor_Address);
        RegisterMethod("Hash()", ExtCalls.Constructor_Hash);
        RegisterMethod("Timestamp()", ExtCalls.Constructor_Timestamp);

        RegisterMethod("Data.Set", Data_Set);
        RegisterMethod("Data.Get", Data_Get);
        RegisterMethod("Data.Delete", Data_Delete);
        RegisterMethod("Runtime.Version", Runtime_Version);
        RegisterMethod("Runtime.TransactionHash", Runtime_TransactionHash);
        RegisterMethod("Runtime.Context", Runtime_Context);
        RegisterMethod("Runtime.ReadInfusions", Runtime_ReadInfusions);
        RegisterMethod("Runtime.GetOwnerships", Runtime_GetOwnerships);
        RegisterMethod("Runtime.Notify", Runtime_Notify);

        RegisterMethod("Runtime.GetAvailableTokenSymbols", Runtime_GetAvailableTokenSymbols);

        contexts = new Dictionary<string, ScriptContext>();
    }

    private ExecutionContext ContextLoader(string contextName)
    {
        if (contexts.ContainsKey(contextName))
            return contexts[contextName];

        return null;
    }

    public byte[] BuildScript(string[] lines)
    {
        IEnumerable<Semanteme> semantemes = null;
        try
        {
            semantemes = Semanteme.ProcessLines(lines);
        }
        catch (Exception e)
        {
            throw new Exception("Error parsing the script");
        }

        var sb = new ScriptBuilder();
        byte[] script = null;

        try
        {
            script = sb.ToScript();
        }
        catch (Exception e)
        {
            throw new Exception("Error assembling the script");
        }

        return script;
    }

    public void RegisterMethod(string method, Func<VirtualMachine, ExecutionState> callback)
    {
        _interops[method] = callback;
    }

    public void RegisterContextLoader(Func<string, ExecutionContext> callback)
    {
        _contextLoader = callback;
    }

    public override ExecutionState ExecuteInterop(string method)
    {
        if (_interops.ContainsKey(method))
        {
            return _interops[method](this);
        }

        throw new VMException(this, $"unknown interop: {method}");
    }

    public override ExecutionContext LoadContext(string contextName)
    {
        if (_contextLoader != null)
        {
            return _contextLoader(contextName);
        }

        throw new VMException(this, $"unknown context: {contextName}");
    }

    public override void DumpData(List<string> lines)
    {
        // do nothing
    }

    private ExecutionState Data_Get(VirtualMachine vm)
    {
        var contractName = vm.PopString("contract");
        //vm.Expect(vm.ContractDeployed(contractName), $"contract {contractName} is not deployed");

        var field = vm.PopString("field");
        var key = SmartContract.GetKeyForField(contractName, field, false);

        var type_obj = vm.Stack.Pop();
        var vmType = type_obj.AsEnum<VMType>();

        if (vmType == VMType.Object)
        {
            vmType = VMType.Bytes;
        }

        var value_bytes = this.storage.ContainsKey(key) ? this.storage[key] : new byte[0];
        var val = new VMObject();
        val.SetValue(value_bytes, vmType);

        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Data_Set(VirtualMachine vm)
    {
        // for security reasons we don't accept the caller to specify a contract name
        var contractName = vm.CurrentContext.Name;

        var field = vm.PopString("field");
        var key = SmartContract.GetKeyForField(contractName, field, false);

        var obj = vm.Stack.Pop();
        var valBytes = obj.AsByteArray();

        this.storage[key] = valBytes;

        return ExecutionState.Running;
    }

    private ExecutionState Data_Delete(VirtualMachine vm)
    {
        // for security reasons we don't accept the caller to specify a contract name
        var contractName = vm.CurrentContext.Name;

        var field = vm.PopString("field");
        var key = SmartContract.GetKeyForField(contractName, field, false);

        this.storage.Remove(key);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_Version(VirtualMachine vm)
    {
        var val = VMObject.FromObject(DomainSettings.LatestKnownProtocol);
        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_TransactionHash(VirtualMachine vm)
    {
        var val = VMObject.FromObject(
            Hash.FromString("F6C095A0ED5984F76994EDD8BA555EC10A4B601337B0A15F94162DCD38348534"));
        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_GetOwnerships(VirtualMachine vm)
    {
        var from = vm.Stack.Pop();
        var symbol = vm.PopString("symbol");

        var array = new BigInteger[] { 123, 456, 789 };

        var val = VMObject.FromArray(array);
        this.Stack.Push(val);

        return ExecutionState.Running;
    }
    
    private ExecutionState Runtime_Notify(VirtualMachine vm)
    {
        var kind = vm.Stack.Pop().AsEnum<EventKind>();
        var address = vm.Stack.Pop().AsAddress();
        var obj = vm.Stack.Pop();

        var array = new BigInteger[] { 123, 456, 789 };

        var val = VMObject.FromArray(array);
        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_ReadInfusions(VirtualMachine vm)
    {
        var symbol = vm.PopString("symbol");
        var id = vm.PopNumber("token_id");

        var infusion = new TokenInfusion("SOUL", 1234);

        var infusionArray = new TokenInfusion[] { infusion };

        var val = VMObject.FromArray(infusionArray);

        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_Context(VirtualMachine vm)
    {
        var val = VMObject.FromObject("test");
        this.Stack.Push(val);

        return ExecutionState.Running;
    }

    private ExecutionState Runtime_GetAvailableTokenSymbols(VirtualMachine vm)
    {
        var symbols = new string[] { "LOL", module.Name };

        var val = VMObject.FromArray(symbols);
        this.Stack.Push(val);

        return ExecutionState.Running;
    }
}
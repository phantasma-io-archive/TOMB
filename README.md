# TOMB
TOMB smart contract compiler for Phantasma platform


## Supported features

- Smart contracts
- Numbers, strings, bools, timestamps, addresses, hashes
- Constants
- Global and local variables
- Bitshifting and logical operators
- Contract constructors, methods and triggers
- Contract public methods
- Return values
- Generics (Maps and lists)
- If ... Else
- Throw Exceptions
- Uninitialized globals validation
- Interop and Contract calls
- Import libraries (Runtime, Leaderboard, Token, etc)
- Comments (single and multi line)
- ABI generation

## Planned features

- Non-contract Scripts
- Contract tasks
- Structs
- Loops
- Switch .. case
- Try .. Catch
- More...
- Warnings
- Debugger support

## Literals

Different data types are recognized by the compiler.

| Type  | Example |
| ------------- | ------------- |
| Number  | 123  |
| Bool  | false  |
| String  | "hello"  |
| Timestamp  | no literal support, use either Runtime.time or Utils.unixTime  |
| Byte array  | 0xFAFAFA2423424 |
| Address | @P2K6p3VzyRhxqHE2KcNV2B3QjVrv5ekvWPZLevteDoBQTzA |
| Hash  | #E3FE7BB73996CF7057913BD916F1B07AC0EAB4916DF3BCBDC221829F5CBEA9AF |
| Compiler macro   | $SOMETHING |

## Available libraries

The following libraries can be imported into a contract.

### Runtime
| Method | Description|
| ------------- | ------------- |
| Runtime.log(message:String) | TODO|
| Runtime.expect(condition:Bool, error:String) | TODO|
| Runtime.isWitness(address:Address) | TODO|
| Runtime.isTrigger() | TODO|
| Runtime.time() | TODO|
| Runtime.transactionHash() | TODO|
| Runtime.startTask(from:Address, task:Method) | TODO|
| Runtime.stopTask() | TODO|

### Token
| Method | Description|
| ------------- | ------------- |
| Token.create(from:Address, symbol:String, name:String, maxSupply:Number, decimals:Number, flags:Number, script:Bytes) | TODO|
| Token.transfer(from:Address, to:Address, symbol:String, amount:Number) | TODO|
| Token.transferAll(from:Address, to:Address, symbol:String) | TODO|
| Token.mint(from:Address, to:Address, symbol:String, amount:Number) | TODO|
| Token.burn(from:Address, symbol:String, amount:Number) | TODO|
| Token.getBalance(from:Address, symbol:String) | TODO|

### Organization
| Method | Description|
| ------------- | ------------- |
| Organization.create(from:Address, id:String, name:String, script:Bytes) | TODO|
| Organization.addMember(from:Address, name:String, target:Address) | TODO|

### Oracle
| Method | Description|
| ------------- | ------------- |
| Oracle.read(url:String) | TODO|
| Oracle.price(symbol:String) | TODO|
| Oracle.quote(baseSymbol:String, quoteSymbol:String, amount:Number) | TODO|

### Utils
| Method | Description|
| ------------- | ------------- |
| Utils.unixTime(value:Number) | TODO|
| Utils.contractAddress(name:String) | TODO|

### Leaderboard
| Method | Description|
| ------------- | ------------- |
| Leaderboard.create(from:Address, boardName:String, capacity:Number) | TODO|
| Leaderboard.getAddress(boardName:String, index:Number) | TODO|
| Leaderboard.getScoreByIndex(boardName:String, index:Number) | TODO|
| Leaderboard.getScoreByAddress(boardName:String, target:Address) | TODO|
| Leaderboard.getSize(boardName:String) | TODO|
| Leaderboard.insert(from:Address, target:Address, boardName:String, score:Number) | TODO|
| Leaderboard.reset(from:Address, boardName:String) | TODO|

## Available generic types

### Map
| Method | Description|
| ------------- | ------------- |
| Map.get(key:Generic) | TODO|
| Map.set(key:Generic, value:Generic) | TODO|
| Map.remove(key:Generic) | TODO|
| Map.count() | TODO|
| Map.clear() | TODO|

### List
| Method | Description|
| ------------- | ------------- |
| List.get(index:Number) | TODO|
| List.add(value:Generic) | TODO|
| List.replace(index:Number, value:Generic) | TODO|
| List.remove(index:Number) | TODO|
| List.count() | TODO|
| List.clear() | TODO|


### Available macros

| Macro  | Description |
| ------------- | ------------- |
| $THIS_ADDRESS  | The address of the current contract  |

# Examples

Simple contract that sums two numbers and returns the result

```c#
contract test {	
	method sum(a:number, b:number):number
	{
		return a + b;
	}
}
```

Simple contract that implements a global counter (that can be incremented by anyone who calls the contract).<br/>
Note that any global variable that is not generic must be initialized in the contract constructor.<br/>

```c#
contract test {
	global counter: number;
	
	constructor(owner:address) 
	{
		counter:= 0;
	}
	
	method increment()
	{
		if (counter < 0){
			throw "invalid state";
		}
		counter += 1;
	}
}
```

Another contract that implements a counter, this time unique per user address.<br/>
Showcases how to validate that a transaction was done by user possessing private keys to 'from' address

```c#
contract test {
	import Runtime;
	import Map;
	
	global counters: storage_map<address, number>;
		
	method increment(from:address)
	{
		Runtime.expect(Runtime.isWitness(from), "witness failed");
		local temp: number;
		temp := counters.get(from);
		temp += 1;
		counters.set(from, temp);
	}
}
```

A contract that takes a payment in tokens from a user.<br/>
Showcases how to transfer tokens and how to use macro $THIS_ADDRESS to obtain address of the contract.

```c#
contract test {
	import Runtime;
	import Token;
			
	method paySomething(from:address, quantity:number)
	{
		Runtime.expect(Runtime.isWitness(from), "witness failed");
		
		local price: number := 10;
		price *= quantity;
		
		local thisAddr:address := $THIS_ADDRESS;
		Token.transfer(from, thisAddr, "SOUL", price);

		// TODO after payment give something to 'from' address 
	}
}
```
# TOMB
TOMB smart contract compiler for Phantasma platform


## Supported features

- Smart contracts and Non-contract Scripts (eg: transactions, raw invokes)
- Numbers, strings, bools, timestamps, addresses, hashes
- Constants
- Global and local variables
- Bitshifting and logical operators
- Contract constructors, methods and triggers
- Contract public methods
- Return values
- Generics (Maps and lists)
- If ... Else
- While ... and Do ... While loops
- Break and Continue
- Throw Exceptions
- Uninitialized globals validation
- Custom events
- Interop and Contract calls
- Inline asm
- Structs
- Import libraries (Runtime, Leaderboard, Token, etc)
- Comments (single and multi line)
- ABI generation

## Planned features

- Contract tasks
- For.. Loops
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
| Address | @P2K6p3VzyRhxqHE2KcNV2B3QjVrv5ekvWPZLevteDoBQTzA or @null|
| Hash  | #E3FE7BB73996CF7057913BD916F1B07AC0EAB4916DF3BCBDC221829F5CBEA9AF |
| Compiler macro   | $SOMETHING |

## Available libraries

The following libraries can be imported into a contract.

### Call
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Call.interop(interopName:string, ...) | ... | TODO|
| Call.contract(contractName:string, contractMethod:string, ...) | ... | TODO|

### Runtime
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Runtime.log(message:String) | None | TODO|
| Runtime.expect(condition:Bool, error:String) | None | TODO|
| Runtime.isWitness(address:Address) | Bool | TODO|
| Runtime.isTrigger() | Bool | TODO|
| Runtime.time() | Timestamp | TODO|
| Runtime.transactionHash() | Hash | TODO|
| Runtime.startTask(from:Address, task:Method) | None | TODO|
| Runtime.stopTask() | None | TODO|

### Token
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Token.create(from:Address, symbol:String, name:String, maxSupply:Number, decimals:Number, flags:Number, script:Bytes) | None | TODO|
| Token.transfer(from:Address, to:Address, symbol:String, amount:Number) | None | TODO|
| Token.transferAll(from:Address, to:Address, symbol:String) | None | TODO|
| Token.mint(from:Address, to:Address, symbol:String, amount:Number) | None | TODO|
| Token.burn(from:Address, symbol:String, amount:Number) | None | TODO|
| Token.getBalance(from:Address, symbol:String) | Number | TODO|

### Organization
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Organization.create(from:Address, id:String, name:String, script:Bytes) | None | TODO|
| Organization.addMember(from:Address, name:String, target:Address) | None | TODO|

### Oracle
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Oracle.read(url:String) | None | TODO|
| Oracle.price(symbol:String) | None | TODO|
| Oracle.quote(baseSymbol:String, quoteSymbol:String, amount:Number) | None | TODO|

### Storage
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Storage.read(contract:String, field:String, type:Number) | Bytes | TODO|
| Storage.write(field:String, value:Bytes) | None | TODO|
| Storage.delete(field:String) | None | TODO|

### Utils
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Utils.unixTime(value:Number) | Timestamp | TODO|
| Utils.contractAddress(name:String) | Address | TODO|

### Leaderboard
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Leaderboard.create(from:Address, boardName:String, capacity:Number) | None | TODO|
| Leaderboard.getAddress(boardName:String, index:Number) | Address | TODO|
| Leaderboard.getScoreByIndex(boardName:String, index:Number) | Number | TODO|
| Leaderboard.getScoreByAddress(boardName:String, target:Address) | Number | TODO|
| Leaderboard.getSize(boardName:String) | Number | TODO|
| Leaderboard.insert(from:Address, target:Address, boardName:String, score:Number) | None | TODO|
| Leaderboard.reset(from:Address, boardName:String) | None | TODO|

### Output
Note: Only available in description scripts
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Output.decimals(value:Number, symbol:String) | None | TODO|
| Output.symbol(symbol:String) | None | TODO|
| Output.account(address:Address) | None | TODO|


## Available generic types

### Map
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Map.get(key:Generic) | Generic | TODO|
| Map.set(key:Generic, value:Generic) | None | TODO|
| Map.remove(key:Generic) | None | TODO|
| Map.count() | Number | TODO|
| Map.clear() | None | TODO|

### List
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| List.get(index:Number) | Generic | TODO|
| List.add(value:Generic) | None | TODO|
| List.replace(index:Number, value:Generic) | None | TODO|
| List.remove(index:Number) | None | TODO|
| List.count() | Number | TODO|
| List.clear() | None | TODO|



### Available macros

| Macro  | Description |
| ------------- | ------------- |
| $THIS_ADDRESS  | The address of the current contract  |

## Exception support

Currently it is possible to throw exceptions with a string message.<br/>
Runtime.expect() can also be used as an alternative way of throwing exceptions based on a condition.<br/>
More work will include support for other data types and support for try..catch.<br/>

```c#
...
throw "something happened";
...
}
```


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

Showcases how a contract method can call other methods.

```c#
contract test {
	method sum(a:number, b:number) {
		return a + b;
	}
			
	method calculatePrice(x:number): number
	{		
		local price: number := 10;
		price := this.sum(price, x); // here we use 'this' for calling another method
		
		return price;
	}
}
```

Showcases how a contract can declare and emit custom events.

```c#
contract test {
	event MyPayment:number = "{address} paid {data}"; // here we use a short-form description
	
	method paySomething(from:address, x:number)
	{		
		Runtime.expect(Runtime.isWitness(from), "witness failed");

		local price: number := 10;
		local thisAddr:address := $THIS_ADDRESS;
		Token.transfer(from, thisAddr, "SOUL", price);
		
		emit MyPayment(from, price);
	}
}
```

A more complex version of the previous example, showcasing custom description scripts.

```c#
description payment_event {

	code(from:address, amount:number): string {
		local result:string := "";
		result += from;
		result += " paid ";
		result += amount;
		return result;
	}
}

contract test {
	event MyPayment:number = payment_event; // here we use a short-form declaration

	// everything else would be same as previous example
}
```

A yet more complex version of the previous examples, showcasing custom description scripts and also struct declarations.

```c#

struct my_event_data {
	amount:number;
	symbol:string;
}

description payment_event {

	code(from:address, data:my_event_data): string {
		local result:string := "";
		result += from;
		result += " paid ";
		result += data.amount;
		result += " ";
		result += data.symbol;
		return result;
	}
}

contract test {
	event MyPayment:my_event_data = payment_event; // here we use a short-form declaration

	// everything else would be same as previous examples
}
```

A script is something that can be used either for a transaction or for an API invokeScript call.<br/>
This example showcases a simple script with one argument, that calls a contract.<br/>
Note that for scripts with arguments, for them to run properly you will have to push them into the stack before.

```c#
script startup {

	import Call;
	
	code(target:address) {
		local temp:number := 50000;
		Call.contract("Stake", "unstake", target, temp);
	}
}```

Inline asm allows to write assembly code that is then inserted and merged into the rest of the code.<br/>
This feature is useful as an workaround for missing features in the compiler.

```c#
script startup {

	import Call;
	
	code() {
		local temp:string;
		asm {
			LOAD $temp "hello"
		}
	}
}```


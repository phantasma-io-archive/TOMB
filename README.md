# TOMB
TOMB smart contract compiler for Phantasma platform


## Supported features

- Numbers, strings, bools, timestamps, addresses, hashes
- Constants
- Global and local variables
- Bitshifting and logical operators
- Contract constructors
- Contract public methods
- Return values
- Generics (Maps and lists)
- If ... Else
- Throw Exceptions
- Uninitialized globals validation
- Interop and Contract calls
- Import libraries (Runtime, Leaderboard, Token, etc)
- Comments (single and multi line)

## Planned features

- Structs
- Loops
- Switch .. case
- Try .. Catch
- More...
- Warnings

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
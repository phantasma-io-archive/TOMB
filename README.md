# TOMB
TOMB smart contract compiler for Phantasma platform


## Supported features

- Contract constructors
- Contract public methods
- Constants
- Global and local variables
- Numbers, strings, bools, timestamps
- Generics (Hashmaps and lists)
- If ... Else
- Throw Exceptions
- Uninitialized globals validation

## Planned features

- Contract calls
- Structs
- Loops
- Switch .. case
- Try .. Catch
- More...
- Warnings

# Examples

Simple contract that implements a global counter (that can be incremented by anyone who calls the contract)

```c#
contract test {
	global counter: number;
	
	constructor() 
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
# TOMB
TOMB smart contract compiler for Phantasma platform

<p align="center">      
  <a href="https://github.com/Relfos/TOMB/workflows/.NET%20Core/badge.svg?branch=master)">
    <img src="https://github.com/Relfos/TOMB/workflows/.NET%20Core/badge.svg?branch=master">
  </a>
  <a href="https://github.com/phantasma-io/PhantasmaChain/blob/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-blue.svg">
  </a>
</p>


## Supported features

- Smart contracts and Non-contract Scripts (eg: transactions, raw invokes)
- Numbers, strings, bools, timestamps, addresses, hashes
- Constants
- Enums
- Global and local variables
- Bitshifting and logical operators
- Contract constructors, methods and triggers
- Contract public methods
- Return values
- Collections (Maps and lists)
- Generic types
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
- Contract tasks
- ABI generation

## Planned features

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
| Decimal<X>  | 0.123  | Where X is the number of maximum decimal places
| Bool  | false  |
| String  | "hello"  |
| Timestamp  | no literal support, use either Time.now or Time.unix  |
| Byte array  | 0xFAFAFA2423424 |
| Address | @P2K6p3VzyRhxqHE2KcNV2B3QjVrv5ekvWPZLevteDoBQTzA or @null|
| Hash  | #E3FE7BB73996CF7057913BD916F1B07AC0EAB4916DF3BCBDC221829F5CBEA9AF |
| Compiler macro   | $SOMETHING |

## Available libraries

The following libraries can be imported into a contract.

### Call
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Call.interop(...:Generic) | Any | TODO|
| Call.contract(method:String, ...:Generic) | Any | TODO|
| Call.method(...:Generic) | Any | TODO|

### Runtime
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Runtime.expect(condition:Bool, error:String) | None | TODO|
| Runtime.log(message:String) | None | TODO|
| Runtime.isWitness(address:Address) | Bool | TODO|
| Runtime.isTrigger() | Bool | TODO|
| Runtime.transactionHash() | Hash | TODO|
| Runtime.deployContract(from:Address, contract:Module) | None | TODO|
| Runtime.upgradeContract(from:Address, contract:Module) | None | TODO|
| Runtime.gasTarget() | Address | TODO|
| Runtime.context() | String | TODO|
| Runtime.previousContext() | String | TODO|

### Token
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Token.create(from:Address, symbol:String, name:String, maxSupply:Number, decimals:Number, flags:Number, script:Bytes, abi:Bytes) | None | TODO|
| Token.exists(symbol:String) | Bool | TODO|
| Token.getDecimals(symbol:String) | Number | TODO|
| Token.getFlags(symbol:String) | Enum<TokenFlag> | TODO|
| Token.transfer(from:Address, to:Address, symbol:String, amount:Number) | None | TODO|
| Token.transferAll(from:Address, to:Address, symbol:String) | None | TODO|
| Token.mint(from:Address, to:Address, symbol:String, amount:Number) | None | TODO|
| Token.burn(from:Address, symbol:String, amount:Number) | None | TODO|
| Token.swap(targetChain:String, source:Address, destination:Address, symbol:String, amount:Number) | None | TODO|
| Token.getBalance(from:Address, symbol:String) | Number | TODO|
| Token.isMinter(address:Address, symbol:String) | Bool | TODO|

### NFT
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| NFT.transfer(from:Address, to:Address, symbol:String, id:Number) | None | TODO|
| NFT.mint(from:Address, to:Address, symbol:String, rom:Any, ram:Any) | None | TODO|
| NFT.burn(from:Address, symbol:String, id:Number) | None | TODO|
| NFT.infuse(from:Address, symbol:String, id:Number, infuseSymbol:String, infuseValue:Number) | None | TODO|
| NFT.createSeries(from:Address, symbol:String, seriesID:Number, maxSupply:Number, mode:Enum<TokenSeries>, nft:Module) | None | TODO|
| NFT.readROM<T>(symbol:String, id:Number) | T | TODO|
| NFT.readRAM<T>(symbol:String, id:Number) | T | TODO|

### Account
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Account.getName(from:Address) | String | TODO|
| Account.getLastActivity(from:Address) | Timestamp | TODO|
| Account.registerName(target:Address, name:String) | None | TODO|
| Account.unregisterName(target:Address) | None | TODO|
| Account.registerScript(target:Address, script:Bytes, abiBytes:Bytes) | None | TODO|
| Account.hasScript(address:Address) | Bool | TODO|
| Account.lookUpAddress(target:Address) | String | TODO|
| Account.lookUpScript(target:Address) | Bytes | TODO|
| Account.lookUpABI(target:Address) | Bytes | TODO|
| Account.lookUpName(name:String) | Address | TODO|
| Account.migrate(from:Address, target:Address) | None | TODO|

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
| Storage.read(contract:String, field:String, type:Number) | Any | TODO|
| Storage.write(field:String, value:Any) | None | TODO|
| Storage.delete(field:String) | None | TODO|
| Storage.calculateStorageSizeForStake(stakeAmount:Number) | Number | TODO|
| Storage.createFile(target:Address, fileName:String, fileSize:Number, contentMerkle:Bytes, encryptionContent:Bytes) | None | TODO|
| Storage.hasFile(target:Address, hash:Hash) | Bool | TODO|
| Storage.addFile(from:Address, target:Address, archiveHash:Hash) | None | TODO|
| Storage.deleteFile(from:Address, targetHash:Hash) | None | TODO|
| Storage.hasPermission(externalAddr:Address, target:Address) | Bool | TODO|
| Storage.addPermission(from:Address, externalAddr:Address) | None | TODO|
| Storage.deletePermission(from:Address, externalAddr:Address) | None | TODO|
| Storage.migratePermission(target:Address, oldAddr:Address, newAddr:Address) | None | TODO|
| Storage.migrate(from:Address, target:Address) | None | TODO|
| Storage.getUsedSpace(from:Address) | Number | TODO|
| Storage.getAvailableSpace(from:Address) | Number | TODO|
| Storage.GetUsedDataQuota(address:Address) | Number | TODO|
| Storage.writeData(target:Address, key:Bytes, value:Bytes) | None | TODO|
| Storage.deleteData(target:Address, key:Bytes) | None | TODO|

### Utils
| Method | Return type | Description|
| ------------- | ------------- |------------- |
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

### Market
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Market.sell(from:Address, baseSymbol:String, quoteSymbol:String, tokenID:Number, price:Number, endDate:Timestamp) | None | TODO|
| Market.buy(from:Address, symbol:String, tokenID:Number) | None | TODO|
| Market.cancel(symbol:String, tokenID:Number) | None | TODO|
| Market.hasAuction(symbol:String, tokenID:Number) | Bool | TODO|
| Market.bid(from:Address, symbol:String, tokenID:Number, price:Number, buyingFee:Number, buyingFeeAddress:Address) | None | TODO|
| Market.listToken(from:Address, baseSymbol:String, quoteSymbol:String, tokenID:Number, price:Number, endPrice:Number, startDate:Timestamp, endDate:Timestamp, extensionPeriod:Number, typeAuction:Number, listingFee:Number, listingFeeAddress:Address) | None | TODO|
| Market.editAuction(from:Address, baseSymbol:String, quoteSymbol:String, tokenID:Number, price:Number, endPrice:Number, startDate:Timestamp, endDate:Timestamp, extensionPeriod:Number) | None | TODO|
 
### Crowdsale
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Crowdsale.create(from:Address, name:String, SaleFlags flags, startDate:Timestamp, endDate:Timestamp, sellSymbol:String, receiveSymbol:String, price:Number, globalSoftCap:Number, globalHardCap:Number, userSoftCap:Number, userHardCap:Number) | Hash | TODO|
| Crowdsale.isSaleActive(saleHash:Hash) | bool | TODO|
| Crowdsale.GetSaleParticipants(saleHash:Hash) | Address[] | TODO|
| Crowdsale.getSaleWhitelists(saleHash:Hash) | Address[] | TODO|
| Crowdsale.isWhitelisted(saleHash:Hash, address:Address) | bool | TODO|
| Crowdsale.addToWhitelist(saleHash:Hash, target:Address) | None | TODO|
| Crowdsale.removeFromWhitelist(saleHash:Hash, target:Address) | None | TODO|
| Crowdsale.getPurchasedAmount(saleHash:Hash, address:Address) | Number | TODO|
| Crowdsale.getSoldAmount(saleHash:Hash) | Number | TODO|
| Crowdsale.purchase(from:Address, saleHash:Hash, quoteSymbol:string, quoteAmount:Number) | None | TODO|
| Crowdsale.closeSale(from:Address, saleHash:Hash) | None | TODO|
| Crowdsale.getLatestSaleHash() | Hash | TODO|
| Crowdsale.EditSalePrice(saleHash:Hash, price:Number) | None | TODO|

### Stake
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Stake.getMasterThreshold() | Number | TODO|
| Stake.isMaster(address:Address) | bool | TODO|
| Stake.getMasterCount() | Number | TODO|
| Stake.getClaimMasterCount(claimDate:Timestamp) | Number | TODO|
| Stake.getMasterClaimDate(claimDistance:Number) | Timestamp | TODO|
| Stake.getMasterDate(target:Address) | Timestamp | TODO|
| Stake.getMasterClaimDateFromReference(claimDistance:Number, referenceTime:Timestamp) | Timestamp | TODO|
| Stake.getMasterRewards(address:Address) | Number | TODO|
| Stake.migrate(from:Address, to:Address) | None | TODO|
| Stake.masterClaim(from:Address) | None | TODO|
| Stake.stake(from:Address, stakeAmount:Number) | None | TODO|
| Stake.unstake(from:Address, unstakeAmount:Number) | None | TODO|
| Stake.getTimeBeforeUnstake(from:Address) | Number | TODO|
| Stake.getStakeTimestamp(from:Address) | Timestamp | TODO|
| Stake.getUnclaimed(from:Address) | Number | TODO|
| Stake.claim(from:Address, stakeAddress:Address) | None | TODO|
| Stake.getStake(address:Address) | Number | TODO|
| Stake.getStorageStake(address:Address) | Number | TODO|
| Stake.fuelToStake(fuelAmount:Number) | Number | TODO|
| Stake.stakeToFuel(stakeAmount:Number) | Number | TODO|
| Stake.getAddressVotingPower(address:Address) | Number | TODO|
| Stake.updateRate() | None | TODO|
| Stake.getRate() | Number | TODO|

### Governance
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Governance.hasName(name:String) | bool | TODO|
| Governance.gasValue(name:String) | bool | TODO|
| Governance.createValue(name:String, initial:Number, serializedConstraints:Bytes) | None | TODO|
| Governance.getValue(name:String) | Number | TODO|
| Governance.setValue(name:String, value:Number) | None | TODO|

### Relay
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Relay.getBalance(from:Address) | Number | TODO|
| Relay.getIndex(from:Address, to:Address) | Number | TODO|
| Relay.getTopUpAddress(from:Address) | Address | TODO|
| Relay.openChannel(from:Address, publicKey:Bytes) | None | TODO|
| Relay.getKey(from:Address) | Bytes | TODO|
| Relay.topUpChannel(from:Address, count:Number) | None | TODO|
| Relay.settleChannel(receipt:RelayReceipt) | None | TODO|

### Mail
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Mail.pushMessage(from:Address, target:Address, archiveHash:Hash) | None | TODO|
| Mail.domainExists(domainName:String) | Bool | TODO|
| Mail.registerDomain(from:Address, domainName:String) | None | TODO|
| Mail.unregisterDomain(domainName:String) | None | TODO|
| Mail.migrateDomain(domainName:String, target:Address) | None | TODO|
| Mail.joinDomain(from:Address, domainName:String) | None | TODO|
| Mail.leaveDomain(from:Address, domainName:String) | None | TODO|
| Mail.getUserDomain(target:Address) | String | TODO|

### Time
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Time.now() | Timestamp | TODO|
| Time.unix(value:Number) | Timestamp | TODO|

### Task
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Task.start(method:Method, from:Address, frequency:Number, mode:Enum<TaskMode>, gasLimit:Number) | Task | TODO|
| Task.stop(task:Address) | None | TODO|
| Task.current() | Task | TODO|

### UID
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| UID.generate() | Number | TODO|

### Map
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Map.get(key:Generic) | Generic | TODO|
| Map.set(key:Generic, value:Generic) | None | TODO|
| Map.remove(key:Generic) | None | TODO|
| Map.clear() | None | TODO|
| Map.count() | Number | TODO|
| Map.has(key:Generic) | Bool | TODO|

### List
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| List.get(index:Number) | Generic | TODO|
| List.add(value:Generic) | None | TODO|
| List.replace(index:Number, value:Generic) | None | TODO|
| List.remove(index:Number) | None | TODO|
| List.count() | Number | TODO|
| List.clear() | None | TODO|

### String
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| String.length(target:String) | Number | TODO|
| String.substr(target:String, index:Number, length:Number) | Number | TODO|

### Decimal
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Decimal.decimals(target:Any) | Number | TODO|
| Decimal.convert(decimalPlaces:Number, value:Number) | Number | TODO|

### Enum
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Enum.isSet(target:Enum<>, flag:Enum<>) | Bool | TODO|

### Address
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Address.isNull(target:Address) | Bool | TODO|
| Address.isUser(target:Address) | Bool | TODO|
| Address.isSystem(target:Address) | Bool | TODO|
| Address.isInterop(target:Address) | Bool | TODO|

### Module
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Module.getScript(target:Module) | Bytes | TODO|
| Module.getABI(target:Module) | Bytes | TODO|

### Format
| Method | Return type | Description|
| ------------- | ------------- |------------- |
| Format.decimals(value:Number, symbol:String) | String | TODO|
| Format.symbol(symbol:String) | String | TODO|
| Format.account(address:Address) | String | TODO|

### Available macros

| Macro  | Description |
| ------------- | ------------- |
| $THIS_ADDRESS  | The address of the current contract  |
| $THIS_SYMBOL  | The symbol of the current token  |
| $TYPE_OF  | The type of the argument  |

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

## Simple Sum
Simple contract that sums two numbers and returns the result

```c#
contract test {
	public sum(a:number, b:number):number
	{
		return a + b;
	}
}
```

## Conditions
Like most programming languages, it is possible to do conditional branching use if statement.<br/>
Logic operators supported include and, or and xor.<br/>

```c#
contract test {
	public isEvenAndPositive(n:number):bool
	{
		if (n>0 and n%2==0)
		{
			return true;
		}
		else {
			return false;
		}
	}
}
```

## Simple Counter
Simple contract that implements a global counter (that can be incremented by anyone who calls the contract).<br/>
Note that any global variable that is not generic must be initialized in the contract constructor.<br/>

```c#
contract test {
	global counter: number;

	constructor(owner:address)
	{
		counter:= 0;
	}

	public increment()
	{
		if (counter < 0){
			throw "invalid state";
		}
		counter += 1;
	}
}
```

## Counter per Address
Another contract that implements a counter, this time unique per user address.<br/>
Showcases how to validate that a transaction was done by user possessing private keys to 'from' address

```c#
contract test {
	import Runtime;
	import Map;

	global counters: storage_map<address, number>;

	public increment(from:address)
	{
		Runtime.expect(Runtime.isWitness(from), "witness failed");
		local temp: number;
		temp := counters.get(from);
		temp += 1;
		counters.set(from, temp);
	}
}
```

## Strings
Simple contract that shows how to use strings and builtin type methods, string.length() in this specific case.<br/>

```c#
contract test {
	global val: string;

	constructor(owner:address)
	{
		val := "hello";
		val += " world";
	}

	public getLength():number
	{
		return val.length();
	}
}
```

## Decimals
There is compiler support for decimal numbers.<br/>
Note that internally those are converted to Number types in fixed point format.

```c#
contract test {
	global val: decimal<4>; // the number between <> is the number of decimal places

	constructor(owner:address)
	{
		val := 2.1425;
	}

	public getValue():number
	{
		return val; // this will return 21425, which is the previous value in fixed point format
	}

	public getDecimals():number
	{
		return val.decimals(); // this returns 4 as result
	}

	public sum(other:decimal<4>):number
	{
		return val + other;
	}

}
```

## Enums
There is compiler support for enumerated value types, that map directly to the Phantasma VM enum type.<br/>

```c#
// NOTE - like other custom types, it is declared outside the scope of a contract
enum MyEnum { A = 0, B = 1, C = 2}
// if the numbers are sequential, it is ok to ommit them, eg:
//enum MyEnum { A, B, C}

contract test {
	global state: MyEnum;

	constructor(owner:address)
	{
		state := MyEnum.B;
	}

	public getValue():MyEnum
	{
		return state;
	}		
}
```

## Map support
The compiler supports generic types, including maps.<br/>
Maps are one of the few types that don't have to initialized in the constructor.<br/>

```c#
contract test {
	global my_state: storage_map<address, number>;

	constructor(owner:address)
	{
		my_state.set(owner, 42);
	}

	public getState(target:address):number
	{
		return my_state.get(target);
	}
}
```

## Random numbers
It is possible to generate pseudo random numbers, and also to control the generation seed.<br/>
If a seed is not specified, then the current transaction hash will be used as seed, if available.<br/>

```c#
contract test {
	import Random;

	global my_state: number;

	constructor(owner:address)
	{
		Random.seed(16676869); // optionally we can specify a seed, this will make the next sequence of random numbers to be deterministic
		my_state := mutateState();
	}

	public mutateState():number
	{
		my_state := Random.generate() % 1024; // Use modulus operator to constrain the random number to a specific range
		return my_state;
	}
}
```

## Transfer Tokens
A contract that takes a payment in tokens from a user.<br/>
Showcases how to transfer tokens and how to use macro $THIS_ADDRESS to obtain address of the contract.

```c#
contract test {
	import Runtime;
	import Token;

	public paySomething(from:address, quantity:number)
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

## Token Flags
There are also some builtin enums, like TokenFlags.<br/>

```c#

contract test {
	import Runtime;
	import Token;

	public paySomething(from:address, amount:number, symbol:string)
	{
		Runtime.expect(Runtime.isWitness(from), "witness failed");

		local flags:TokenFlags := Token.getFlags(symbol);

		if (flags.isSet(TokenFlags.Fungible)) {
			local thisAddr:address := $THIS_ADDRESS;
			Token.transfer(from, thisAddr, "SOUL", price);
		}
	}
}
```

## Validation
Runtime.Expect offers a clean way to validate conditions.<br/>
The developer of a smart contract must be very careful to ensure that no exploits are possible due to missing validations.<br/>

```c#
contract test {
	import Runtime;

	public doSomething(from:address)  {
		Runtime.expect(from.isUser(), "expected user address"); // makes sure the address is of 'user' type
		Runtime.expect(Runtime.isWitness(from), "invalid witness"); // makes sure the transaction was signed by 'from' address
		Runtime.expect(Runtime.gasTarget() == $THIS_ADDRESS, "invalid donation"); // makes sure the transaction fees are donated to this contract

		// actually do something after passing all validation
	}

}
```

## Call method
Showcases how a contract method can call other methods.<br/>
If a method is declared private, it can't be called by anyone except the actual contract that implements it.

```c#
contract test {
	private sum(a:number, b:number) {
		return a + b;
	}

	public calculatePrice(x:number): number
	{		
		local price: number := 10;
		price := this.sum(price, x); // here we use 'this' for calling another method

		return price;
	}
}
```


## Scripts
A script is something that can be used either for a transaction or for an API invokeScript call.<br/>
This example showcases a simple script with one argument, that calls a contract.<br/>
Note that for scripts with arguments, for them to run properly you will have to push them into the stack before.

```c#
script startup {

	import Call;

	code(target:address) {
		local temp:number := 50000;
		Call.contract("Stake", "Unstake", target, temp);
	}
}
```

## Deploy contract script 

This example showcases a script that deploys a token contract.

```c#
token GHOST {
...
}

script deploy {

    import Token;
    import Module;

    code() {
        local maxSupply:number := 50000;
        local decimals:number := 1;
		local flags:TokenFlags := TokenFlags.None;
        Token.create(@P2KAkQRrL62zYvb5198CHBLiKHKr4bJvAG7aXwV69rtbeSz, "GHOST",  "Ghost Token", maxSupply, decimals, flags, Module.getScript(GHOST),  Module.getABI(GHOST));
    }
}
```

## Minting Script
This example showcases a script that mints an custom NFT.<br/>

```c#
struct my_rom_data {
	name:string;
	counter:number;
}

script token_minter {

	import Token;

	code(source:address, target:address) {
		local rom_data:my_rom_data := Struct.my_rom_data("hello", 123);
		NFT.mint(source, target, "LOL", rom_data, "ram_can_be_anything");
	}
}
```

## Inline asm
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
}
```

## Custom events 1
Showcases how a contract can declare and emit custom events.

```c#
contract test {
	event MyPayment:number = "{address} paid {data}"; // here we use a short-form description

	public paySomething(from:address, x:number)
	{		
		Runtime.expect(Runtime.isWitness(from), "witness failed");

		local price: number := 10;
		local thisAddr:address := $THIS_ADDRESS;
		Token.transfer(from, thisAddr, "SOUL", price);

		emit MyPayment(from, price);
	}
}
```

## Custom events 2
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

## Custom events 3
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

## Triggers 1
A contract example showcasing triggers.<br/>
In this example, this account will only accept transfers of KCAL and reject anything else.

```c#
contract test {

	trigger onReceive(from:address, symbol:string, amount:number)
	{
		if (symbol != "KCAL") {
			throw "can't receive asset: " + symbol;
		}

		return;
	}
}
```

## Triggers 2
Another contract example showcasing triggers.<br/>
In this example, any asset sent to this account will be auto-converted into SOUL.

```c#
contract test {

	trigger onReceive(from:address, symbol:string, amount:number)
	{
		if (symbol != "SOUL") {
			Call.contract("Swap", "SwapTokens", from, symbol, "SOUL", amount);
		}

		return;
	}
}
```

## Method variables
It is possible to use methods as variables.<br/>

```c#
contract test {

	import Runtime;
	import Map;

	global _counter:number;

	global _callMap: storage_map<address, method<address>>;

	constructor(owner:address)
	{
		_counter := 0;
	}

	public registerUser(from:address, amount:number)
	{
		local target: method<address>;

		if (amount > 10) {
			target := incCounter;
		}
		else {
			target := decCounter;
		}

		_callMap.set(from, target);
	}

	public callUser(from:address) {
		local target: method<address> := _callMap.get(from);

		Call.method(target, from);
	}

	private incCounter(target:address) {
		_counter += 1;
	}

	private subCounter(target:address) {
		_counter -= 1;
	}

}
```

## Tasks
A task allows a contract method to run periodically without user intervention.<br/>
Tasks can't have parameters, however you can use Task.current() along with a global Map to associate custom user data to each task.<br/>

```c#
contract test {
	import Time;
	import Task;

	global victory:bool;
	global deadline:timestamp;

	constructor(owner:address) {
		victory := false;
		time := Time.now() + time.hours(2);
		Task.start(checkResult, owner, 0, TaskFrequency.Always, 999);
	}

	task checkResult()  {
		if (victory) {
			break;
		}

		local now: timestamp := Time.now();

		if (time >= deadline) {
			break;
		}

		continue;
	}

	public win(from:address)
	{
		victory := true;
	}
}
```

Yet another contract example showcasing triggers.<br/>
In this example, a multi-signature account is implemented.

## Multi-signature
```c#
contract test {

	global owners: storage_list<address>;

	constructor(owner:address)
	{
		owners.add(owner);
	}

	private validateSignatures() {
		local index:number := 0;
		local count:number := owners.count();

		while (index < count) {
			local addr:address := owners.get(index);
			if (!Runtime.isWitness(addr)) {
				throw "missing signature of "+addr;
			}
		}
	}

	public isOwner(target:address):bool {
		local index:number := 0;
		local count:number := owners.count();

		while (index < count) {
			local addr:address := owners.get(index);
			if (addr == target) {
				return true;
			}
		}

		return false;
	}

	public addOwner(target:address) {
		Runtime.expect(!isOwner(target), "already is owner");
		validateSignatures();
		owners.add(target);
	}

	trigger onSend(from:address, symbol:string, amount:number)
	{
		validateSignatures();
	}
}
```



## NFTs
Showcases how to implement an NFT, showcasing all details including ROM, RAM and token series.

```c#
struct luchador_rom
{
	genes:bytes;
	name:string;
}

struct luchador_ram
{
	experience:number;
	level:number;
}

struct item_rom
{
	kind:number;
	quality:number;
}

struct item_ram
{
	power:number;
	level:number;
}

token NACHO {
	global _owner: address;

	const LUCHADOR_SERIES: number = 1;
	const LUCHADOR_SUPPLY: number = 100000;

	const ITEM_SERIES: number = 2;
	const ITEM_SUPPLY: number = 500000;

	property name: string = "Nachomen";

	property isBurnable: bool = true;
	property isFinite: bool = true;
	property isFungible: bool = false;
	property maxSupply: number = LUCHADOR_SUPPLY + ITEM_SUPPLY;

	nft luchador<luchador_rom, luchador_ram> {

		property name: string {
			return _ROM.name;
		}

		property description: string {
			return "Luchador with level " + _RAM.level;
		}

		property imageURL: string {
			return "https://nacho.men/img/luchador/"+ _TokenID;
		}

		property infoURL: string {
			return "https://nacho.men/api/luchador/"+ _TokenID;
		}
	}

	nft item<item_rom, item_ram> {

		property name: string {
			switch (_ROM.kind)
			{
				case 1: return "Potion";
				case 2: return "Gloves";
				default: return "Item #" + _ROM.kind;
			}
		}

		property description: string {
			return "Item level " + _RAM.level;
		}

		property imageURL: string {
			return "https://nacho.men/img/item/"+ _TokenID;
		}

		property infoURL: string {
			return "https://nacho.men/api/item/"+ _TokenID;
		}
	}	

	constructor (addr:address) {
		_owner := addr;
		// at least one token series must exist, here we create 2 series
		// they don't have to be created in the constructor though, can be created later
		NFT.createSeries(_owner, $THIS_SYMBOL, LUCHADOR_SERIES, LUCHADOR_SUPPLY, TokenSeries.Unique, luchador);
		NFT.createSeries(_owner, $THIS_SYMBOL, ITEM_SERIES, ITEM_SUPPLY, TokenSeries.Unique, item);
	}
}

```

## Call contract from another contract
Example showcasing calling contract storage directly from another contract.<br/>
Basic version where another storage map is called, and this value is incremented and stored in another contract storage.<br/>

```c#
contract test {

	import Map;
	import Storage;
	import Call;

	global counters: storage_map<number, number>;

	private getContractCount(tokenId:number):number {
	
		local count:number := Call.interop<number>("Map.Get",  "OTHERCONTRACT", "_storageMap", tokenId, $TYPE_OF(number));
		return count;
		
	}

	public updateCount(tokenID:number) {
	
		local contractCounter:number := this.getContractCount(tokenID);
		contractCounter += 1;
		counters.set(tokenID, contractCounter);
		
	}

	public getCount(tokenID:number):number {
	
		local temp:number := counters.get(tokenID);
		return temp;
		
	}

}
```

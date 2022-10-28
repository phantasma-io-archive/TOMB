/*
Sample implementation of a pseudo-ERC20 token in Phantasma. 
Note that this contract is not compatible with normal Phantasma tokens yet (some details missing, TODO)
*/

pragma solidity ^0.4.19;

import "Phantasma/Runtime.tomb";

contract ERC20Basic {

    string public constant name = "ERC20Basic";
    string public constant symbol = "BSC";
    uint8 public constant decimals = 18;  

	// NOTE there is no need for a Phantsama token to emit those as they are automatically emmited 
    //event Transfer(address indexed from, address indexed to, uint tokens);
    //event Approval(address indexed tokenOwner, address indexed spender, uint tokens);


    mapping(address => uint256) balances;

    //mapping(address => mapping (address => uint256)) allowed;
    
    uint256 totalSupply_;

   constructor(address owner) public {  
	totalSupply_ = 10000000000;
	balances[owner] = totalSupply_;
    }  

    function totalSupply() public view returns (uint256) {
	return totalSupply_;
    }
    
    function balanceOf(address tokenOwner) public view returns (uint) {
        return balances[tokenOwner];
    }

    function transfer(address sender, address receiver, uint numTokens) public returns (bool) {
		require(Runtime.isWitness(sender));
        require(numTokens <= balances[sender]);
        balances[sender] = balances[sender] - numTokens;
        balances[receiver] = balances[receiver] + numTokens;
        //emit Transfer(msg.sender, receiver, numTokens);
        return true;
    }

/*	NOTE the approve/allowance of ERC20 does not apply to Phantasma tokens 

    function approve(address delegate, uint numTokens) public returns (bool) {
        allowed[msg.sender][delegate] = numTokens;
        //emit Approval(msg.sender, delegate, numTokens);
        return true;
    }

    function allowance(address owner, address delegate) public view returns (uint) {
        return allowed[owner][delegate];
    }

    function transferFrom(address owner, address buyer, uint numTokens) public returns (bool) {
        require(numTokens <= balances[owner]);    
        require(numTokens <= allowed[owner][msg.sender]);
    
        balances[owner] = balances[owner].sub(numTokens);
        allowed[owner][msg.sender] = allowed[owner][msg.sender].sub(numTokens);
        balances[buyer] = balances[buyer].add(numTokens);
        //emit Transfer(owner, buyer, numTokens);
        return true;
    }*/
}

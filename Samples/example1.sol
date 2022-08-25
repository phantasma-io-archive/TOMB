//pragma solidity ^0.4.10;

import "Phantasma/Runtime.sol";
import "Phantasma/Random.sol";

//the very first example
contract example1 {
	uint counter;
	
    function getCounter(uint n) public view returns (uint) {
		Runtime.log("Returning the current value of counter");
        return counter; 
    }
	
	function getRandom() public view returns (uint) {
		Runtime.log("Generating a random number");
		return Random.generate();
	}
	
}

//pragma solidity ^0.4.10;

//the very first example
contract example1 {
	uint counter;
	
    function getCounter(uint n) public view returns (uint) {
        return counter; 
    }
	
}

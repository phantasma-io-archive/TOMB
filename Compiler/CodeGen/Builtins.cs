using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Tomb.CodeGen
{
    public static class Builtins
    {
		// NOTE - The ASM here is obtained by compiling builtins.tomb
		// In a future version this can be done at runtime instead of having to hardcode the asm here.
		public static string GetBuiltinMethodCode(string methodName)
		{
			switch (methodName)
			{
				case "math_sqrt":
					return @"
// ********* math_sqrt Method ***********
@entry_math_sqrt: // 0
ALIAS r1 $n // 1
POP $n // 1
CAST $n $n #Number // 3
// Line 5:		local root:number := n / 2;
	ALIAS r2 $root // 7
	COPY $n r3 // 7
	ALIAS r4 $literalexpression47 // 10
	LOAD $literalexpression47 2 // 10
	DIV r3 $literalexpression47 r5 // 16
	COPY r5 $root // 20
// Line 6:		while (n < root * root) {
	@loop_start_whilestatement51: NOP // 24
	COPY $n r3 // 24
	COPY $root r4 // 27
	LT r3 r4 r5 // 30
	COPY $root r3 // 34
	MUL r5 r3 r4 // 37
		JMPNOT r4 @loop_end_whilestatement51 // 41
// Line 7:			root += n / root;
		COPY $root r3 // 45
		COPY $n r5 // 48
		COPY $root r6 // 51
		DIV r5 r6 r7 // 54
		ADD r3 r7 r5 // 58
		COPY r5 $root // 62
// Line 8:			root /= 2;
		COPY $root r3 // 65
		ALIAS r5 $literalexpression65 // 68
		LOAD $literalexpression65 2 // 68
		DIV r3 $literalexpression65 r6 // 74
		COPY r6 $root // 78
		JMP @loop_start_whilestatement51 // 81
		@loop_end_whilestatement51: NOP // 85
// Line 9:		}
// Line 10:		
// Line 11:		return root;
	COPY $root r3 // 85
	PUSH r3 // 88
	JMP @exit_math_sqrt // 90
@exit_math_sqrt: // 93
RET // 94
";

				default:
					throw new CompilerException("Unknown builtin method name: " + methodName);
			}
		}
	}
}

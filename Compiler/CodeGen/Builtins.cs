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
@entry_tomb_math_sqrt: // 0
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

				case "string_upper":
					return @"
// ********* string_upper Method ***********
@entry_tomb_string_upper: // 95
ALIAS r1 $s // 96
POP $s // 96
// Line 15:	{        
// Line 16:		local my_array: array<number>;		
	ALIAS r2 $my_array // 98
// Line 17:		
// Line 18:		// extract chars from string into an array
// Line 19:		my_array := s.toArray();	
	COPY $s r3 // 98
	CAST r3 r3 #Struct // 101
	COPY r3 $my_array // 105
// Line 20:		
// Line 21:		local length :number := Array.length(my_array);
	ALIAS r3 $length // 108
	COPY $my_array r4 // 108
	COUNT r4 r4 // 111
	COPY r4 $length // 114
// Line 22:		local idx :number := 0;
	ALIAS r4 $idx // 117
	ALIAS r5 $literalexpression80 // 117
	LOAD $literalexpression80 0 // 117
	COPY $literalexpression80 $idx // 122
// Line 23:		
// Line 24:		while (idx < length) {
	@loop_start_whilestatement83: NOP // 126
	COPY $idx r5 // 126
	COPY $length r6 // 129
	LT r5 r6 r7 // 132
		JMPNOT r7 @loop_end_whilestatement83 // 136
// Line 25:			local ch : number := my_array[idx];
		ALIAS r5 $ch // 140
		COPY $idx r8 // 140
		GET $my_array r6 r8 // 143
		COPY r6 $ch // 147
// Line 26:			
// Line 27:			if (ch >= 97) {
		COPY $ch r6 // 150
		ALIAS r8 $literalexpression94 // 153
		LOAD $literalexpression94 97 // 153
		GTE r6 $literalexpression94 r9 // 159
			JMPNOT r9 @then_ifstatement92 // 163
// Line 28:				if (ch <= 122) {				
			COPY $ch r6 // 167
			ALIAS r8 $literalexpression99 // 170
			LOAD $literalexpression99 122 // 170
			LTE r6 $literalexpression99 r10 // 176
				JMPNOT r10 @then_ifstatement97 // 180
// Line 29:					my_array[idx] := ch - 32; 
				COPY $ch r6 // 184
				ALIAS r8 $literalexpression105 // 187
				LOAD $literalexpression105 32 // 187
				SUB r6 $literalexpression105 r11 // 193
				COPY $idx r6 // 197
				PUT r11 $my_array r6 // 200
				@then_ifstatement97: NOP // 205
			@then_ifstatement92: NOP // 206
// Line 30:				}
// Line 31:			}
// Line 32:						
// Line 33:			idx += 1;
		COPY $idx r6 // 206
		ALIAS r8 $literalexpression108 // 209
		LOAD $literalexpression108 1 // 209
		ADD r6 $literalexpression108 r9 // 215
		COPY r9 $idx // 219
		JMP @loop_start_whilestatement83 // 222
		@loop_end_whilestatement83: NOP // 226
// Line 34:		}
// Line 35:				
// Line 36:		// convert the array back into a unicode string
// Line 37:		local result:string := String.fromArray(my_array); 
	ALIAS r5 $result // 226
	COPY $my_array r6 // 226
	CAST r6 r6 #String // 229
	COPY r6 $result // 233
// Line 38:		return result;
	COPY $result r6 // 236
	PUSH r6 // 239
	JMP @exit_string_upper // 241
@exit_string_upper: // 244
RET // 245
";

				case "string_lower":
					return @"

// ********* string_lower Method ***********
@entry_tomb_string_lower: // 246
ALIAS r1 $s // 247
POP $s // 247
// Line 42:	{        
// Line 43:		local my_array: array<number>;		
	ALIAS r2 $my_array // 249
// Line 44:		
// Line 45:		// extract chars from string into an array
// Line 46:		my_array := s.toArray();	
	COPY $s r3 // 249
	CAST r3 r3 #Struct // 252
	COPY r3 $my_array // 256
// Line 47:		
// Line 48:		local length :number := Array.length(my_array);
	ALIAS r3 $length // 259
	COPY $my_array r4 // 259
	COUNT r4 r4 // 262
	COPY r4 $length // 265
// Line 49:		local idx :number := 0;
	ALIAS r4 $idx // 268
	ALIAS r5 $literalexpression132 // 268
	LOAD $literalexpression132 0 // 268
	COPY $literalexpression132 $idx // 273
// Line 50:		
// Line 51:		while (idx < length) {
	@loop_start_whilestatement135: NOP // 277
	COPY $idx r5 // 277
	COPY $length r6 // 280
	LT r5 r6 r7 // 283
		JMPNOT r7 @loop_end_whilestatement135 // 287
// Line 52:			local ch : number := my_array[idx];
		ALIAS r5 $ch // 291
		COPY $idx r8 // 291
		GET $my_array r6 r8 // 294
		COPY r6 $ch // 298
// Line 53:			
// Line 54:			if (ch >= 65) {
		COPY $ch r6 // 301
		ALIAS r8 $literalexpression146 // 304
		LOAD $literalexpression146 65 // 304
		GTE r6 $literalexpression146 r9 // 310
			JMPNOT r9 @then_ifstatement144 // 314
// Line 55:				if (ch <= 90) {				
			COPY $ch r6 // 318
			ALIAS r8 $literalexpression151 // 321
			LOAD $literalexpression151 90 // 321
			LTE r6 $literalexpression151 r10 // 327
				JMPNOT r10 @then_ifstatement149 // 331
// Line 56:					my_array[idx] := ch + 32; 
				COPY $ch r6 // 335
				ALIAS r8 $literalexpression157 // 338
				LOAD $literalexpression157 32 // 338
				ADD r6 $literalexpression157 r11 // 344
				COPY $idx r6 // 348
				PUT r11 $my_array r6 // 351
				@then_ifstatement149: NOP // 356
			@then_ifstatement144: NOP // 357
// Line 57:				}
// Line 58:			}
// Line 59:						
// Line 60:			idx += 1;
		COPY $idx r6 // 357
		ALIAS r8 $literalexpression160 // 360
		LOAD $literalexpression160 1 // 360
		ADD r6 $literalexpression160 r9 // 366
		COPY r9 $idx // 370
		JMP @loop_start_whilestatement135 // 373
		@loop_end_whilestatement135: NOP // 377
// Line 61:		}
// Line 62:				
// Line 63:		// convert the array back into a unicode string
// Line 64:		local result:string := String.fromArray(my_array); 
	ALIAS r5 $result // 377
	COPY $my_array r6 // 377
	CAST r6 r6 #String // 380
	COPY r6 $result // 384
// Line 65:		return result;
	COPY $result r6 // 387
	PUSH r6 // 390
	JMP @exit_string_lower // 392
@exit_string_lower: // 395
RET // 396
";


				default:
					throw new CompilerException("Unknown builtin method name: " + methodName);
			}
		}
	}
}

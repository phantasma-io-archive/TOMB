using Phantasma.Tomb.AST;
using Phantasma.Tomb.AST.Declarations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Tomb.CodeGen
{
	public struct BuiltinArg
    {
		public readonly string Name;
		public readonly VarType Type;

        public BuiltinArg(string name, VarType type)
        {
            Name = name;
            Type = type;
        }
    }

	public readonly struct BuiltinInfo
    {
		public readonly string Alias;
		public readonly string LibraryName;
		public readonly string MethodName;
		public readonly VarKind ReturnType;
		public readonly BuiltinArg[] Args;
		public readonly string Code;

        public BuiltinInfo(string alias, string libraryName, string methodName, VarKind returnType, IEnumerable<BuiltinArg> args, string code)
        {
			Alias = alias;
            LibraryName = libraryName;
            MethodName = methodName;
			ReturnType = returnType;
            Args = args.ToArray();
			Code = code;
        }
    }

	public static class Builtins
    {
		private static Dictionary<string, BuiltinInfo> _builtins = null;

		public static void FillLibrary(LibraryDeclaration libDecl)
        {
			if (_builtins == null)
            {
				Initialize();
            }

			foreach (var entry in _builtins.Values)
            {
				if (entry.LibraryName.Equals(libDecl.Name, StringComparison.OrdinalIgnoreCase))
                {
					var args = entry.Args.Select(x => new MethodParameter(x.Name, x.Type)).ToArray();
					libDecl.AddMethod(entry.MethodName, MethodImplementationType.LocalCall, entry.ReturnType, args).SetAlias(entry.Alias);
				}
			}
        }

		private static readonly string TAG = "// #BUILTIN";

		private static void Initialize()
		{
			_builtins = new Dictionary<string, BuiltinInfo>();

			var lines = BUILTIN_ASM.Split('\n');

			int i = 0;
			while (i < lines.Length)
            {
				if (lines[i].StartsWith(TAG))
                {
					i++;
					var libName = lines[i].Substring(11).Trim();
					i++;
					var methodName = lines[i].Substring(10).Trim();
					i++;
					var tmp = lines[i].Substring(10).Trim();
					var returnType = Enum.Parse<VarKind>(tmp, true);
					i++;
					// skip ARGS line
					i++;
					var args = new List<BuiltinArg>();
					do
					{
						if (!lines[i].StartsWith("//"))
						{
							break;
						}
						tmp = lines[i].Substring(3).Trim();

						var tmp2 = tmp.Split(':');

						var argType = Enum.Parse<VarKind>(tmp2[1].Trim(), true);
						var arg = new BuiltinArg(tmp2[0].Trim(), VarType.Find(argType));
						args.Add(arg);
						i++;
					} while (true);

					var code = new StringBuilder();
					do
					{
						if (i >= lines.Length)
						{
							break;
						}

						if (lines[i].StartsWith(TAG))
						{
							i--;
							break;
						}

						code.AppendLine(lines[i]);
						i++;
					} while (true);

#if DEBUG
					Console.WriteLine($"Detected builtin: {libName}.{methodName}");
#endif

					var alias = ("tomb_" + libName + "_" + methodName).ToLowerInvariant();

					var builtin = new BuiltinInfo(alias, libName, methodName, returnType, args, code.ToString());
					_builtins[alias] = builtin;
				}

				i++;
            }
		}

		// NOTE - The ASM here is obtained by compiling builtins.tomb
		// In a future version this can be done at runtime instead of having to hardcode the asm here.
		public static string GetBuiltinMethodCode(string methodName)
		{
			if (_builtins != null)
            {
				if (_builtins.ContainsKey(methodName))
                {
					return _builtins[methodName].Code;
                }
            }
	
			throw new CompilerException("Unknown builtin method name: " + methodName);
		}

		private static readonly string BUILTIN_ASM = @"
// Line 1:contract builtins {
// Line 2:	import Array;
// Line 3:	
// Line 4:	private tomb_math_sqrt(n:number) : number {

// ********* tomb_math_sqrt Method ***********
// #BUILTIN
// LIBRARY:math
// METHOD:sqrt
// RETURN:Number
// ARGS:
// n:Number
@entry_tomb_math_sqrt: // 0
ALIAS r1 $n // 1
POP $n // 1
CAST $n $n #Number // 3
// Line 5:		local root:number := n / 2;
	ALIAS r2 $root // 7
	COPY $n r3 // 7
	ALIAS r4 $literalexpression48 // 10
	LOAD $literalexpression48 2 // 10
	DIV r3 $literalexpression48 r5 // 16
	COPY r5 $root // 20
// Line 6:		while (n < root * root) {
	@loop_start_whilestatement52: NOP // 24
	COPY $n r3 // 24
	COPY $root r4 // 27
	LT r3 r4 r5 // 30
	COPY $root r3 // 34
	MUL r5 r3 r4 // 37
		JMPNOT r4 @loop_end_whilestatement52 // 41
// Line 7:			root += n / root;
		COPY $root r3 // 45
		COPY $n r5 // 48
		COPY $root r6 // 51
		DIV r5 r6 r7 // 54
		ADD r3 r7 r5 // 58
		COPY r5 $root // 62
// Line 8:			root /= 2;
		COPY $root r3 // 65
		ALIAS r5 $literalexpression66 // 68
		LOAD $literalexpression66 2 // 68
		DIV r3 $literalexpression66 r6 // 74
		COPY r6 $root // 78
		JMP @loop_start_whilestatement52 // 81
		@loop_end_whilestatement52: NOP // 85
// Line 9:		}
// Line 10:		
// Line 11:		return root;
	COPY $root r3 // 85
	PUSH r3 // 88
	JMP @exit_tomb_math_sqrt // 90
@exit_tomb_math_sqrt: // 93
RET // 94
// Line 12:	}
// Line 13:	
// Line 14:	private tomb_string_toUpper(s:string):string

// ********* tomb_string_toUpper Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:toUpper
// RETURN:String
// ARGS:
// s:String
@entry_tomb_string_toUpper: // 95
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
	ALIAS r5 $literalexpression85 // 117
	LOAD $literalexpression85 0 // 117
	COPY $literalexpression85 $idx // 122
// Line 23:		
// Line 24:		while (idx < length) {
	@loop_start_whilestatement88: NOP // 126
	COPY $idx r5 // 126
	COPY $length r6 // 129
	LT r5 r6 r7 // 132
		JMPNOT r7 @loop_end_whilestatement88 // 136
// Line 25:			local ch : number := my_array[idx];
		ALIAS r5 $ch // 140
		COPY $idx r8 // 140
		GET $my_array r6 r8 // 143
		COPY r6 $ch // 147
// Line 26:			
// Line 27:			if (ch >= 97) {
		COPY $ch r6 // 150
		ALIAS r8 $literalexpression99 // 153
		LOAD $literalexpression99 97 // 153
		GTE r6 $literalexpression99 r9 // 159
			JMPNOT r9 @then_ifstatement97 // 163
// Line 28:				if (ch <= 122) {				
			COPY $ch r6 // 167
			ALIAS r8 $literalexpression104 // 170
			LOAD $literalexpression104 122 // 170
			LTE r6 $literalexpression104 r10 // 176
				JMPNOT r10 @then_ifstatement102 // 180
// Line 29:					my_array[idx] := ch - 32; 
				COPY $ch r6 // 184
				ALIAS r8 $literalexpression110 // 187
				LOAD $literalexpression110 32 // 187
				SUB r6 $literalexpression110 r11 // 193
				COPY $idx r6 // 197
				PUT r11 $my_array r6 // 200
				@then_ifstatement102: NOP // 205
			@then_ifstatement97: NOP // 206
// Line 30:				}
// Line 31:			}
// Line 32:						
// Line 33:			idx += 1;
		COPY $idx r6 // 206
		ALIAS r8 $literalexpression113 // 209
		LOAD $literalexpression113 1 // 209
		ADD r6 $literalexpression113 r9 // 215
		COPY r9 $idx // 219
		JMP @loop_start_whilestatement88 // 222
		@loop_end_whilestatement88: NOP // 226
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
	JMP @exit_tomb_string_toUpper // 241
@exit_tomb_string_toUpper: // 244
RET // 245
// Line 39:	}		
// Line 40:
// Line 41:	private tomb_string_toLower(s:string):string 

// ********* tomb_string_toLower Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:toLower
// RETURN:String
// ARGS:
// s:String
@entry_tomb_string_toLower: // 246
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
	ALIAS r5 $literalexpression137 // 268
	LOAD $literalexpression137 0 // 268
	COPY $literalexpression137 $idx // 273
// Line 50:		
// Line 51:		while (idx < length) {
	@loop_start_whilestatement140: NOP // 277
	COPY $idx r5 // 277
	COPY $length r6 // 280
	LT r5 r6 r7 // 283
		JMPNOT r7 @loop_end_whilestatement140 // 287
// Line 52:			local ch : number := my_array[idx];
		ALIAS r5 $ch // 291
		COPY $idx r8 // 291
		GET $my_array r6 r8 // 294
		COPY r6 $ch // 298
// Line 53:			
// Line 54:			if (ch >= 65) {
		COPY $ch r6 // 301
		ALIAS r8 $literalexpression151 // 304
		LOAD $literalexpression151 65 // 304
		GTE r6 $literalexpression151 r9 // 310
			JMPNOT r9 @then_ifstatement149 // 314
// Line 55:				if (ch <= 90) {				
			COPY $ch r6 // 318
			ALIAS r8 $literalexpression156 // 321
			LOAD $literalexpression156 90 // 321
			LTE r6 $literalexpression156 r10 // 327
				JMPNOT r10 @then_ifstatement154 // 331
// Line 56:					my_array[idx] := ch + 32; 
				COPY $ch r6 // 335
				ALIAS r8 $literalexpression162 // 338
				LOAD $literalexpression162 32 // 338
				ADD r6 $literalexpression162 r11 // 344
				COPY $idx r6 // 348
				PUT r11 $my_array r6 // 351
				@then_ifstatement154: NOP // 356
			@then_ifstatement149: NOP // 357
// Line 57:				}
// Line 58:			}
// Line 59:						
// Line 60:			idx += 1;
		COPY $idx r6 // 357
		ALIAS r8 $literalexpression165 // 360
		LOAD $literalexpression165 1 // 360
		ADD r6 $literalexpression165 r9 // 366
		COPY r9 $idx // 370
		JMP @loop_start_whilestatement140 // 373
		@loop_end_whilestatement140: NOP // 377
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
	JMP @exit_tomb_string_toLower // 392
@exit_tomb_string_toLower: // 395
RET // 396
// Line 66:	}		
// Line 67:	
// Line 68:	
// Line 69:	private tomb_string_indexOf(s:string, x:number):number 

// ********* tomb_string_indexOf Method ***********
// #BUILTIN
// LIBRARY:string
// METHOD:indexOf
// RETURN:Number
// ARGS:
// s:String
// x:Number
@entry_tomb_string_indexOf: // 397
ALIAS r1 $s // 398
POP $s // 398
ALIAS r2 $x // 400
POP $x // 400
CAST $x $x #Number // 402
// Line 70:	{        
// Line 71:		local my_array: array<number>;		
	ALIAS r3 $my_array // 406
// Line 72:		
// Line 73:		// extract chars from string into an array
// Line 74:		my_array := s.toArray();	
	COPY $s r4 // 406
	CAST r4 r4 #Struct // 409
	COPY r4 $my_array // 413
// Line 75:		
// Line 76:		local length :number := Array.length(my_array);
	ALIAS r4 $length // 416
	COPY $my_array r5 // 416
	COUNT r5 r5 // 419
	COPY r5 $length // 422
// Line 77:		local idx :number := 0;
	ALIAS r5 $idx // 425
	ALIAS r6 $literalexpression191 // 425
	LOAD $literalexpression191 0 // 425
	COPY $literalexpression191 $idx // 430
// Line 78:		
// Line 79:		while (idx < length) {
	@loop_start_whilestatement194: NOP // 434
	COPY $idx r6 // 434
	COPY $length r7 // 437
	LT r6 r7 r8 // 440
		JMPNOT r8 @loop_end_whilestatement194 // 444
// Line 80:			local ch : number := my_array[idx];
		ALIAS r6 $ch // 448
		COPY $idx r9 // 448
		GET $my_array r7 r9 // 451
		COPY r7 $ch // 455
// Line 81:			
// Line 82:			if (ch == x) {
		COPY $ch r7 // 458
		COPY $x r9 // 461
		EQUAL r7 r9 r10 // 464
			JMPNOT r10 @then_ifstatement203 // 468
// Line 83:				// found, return index
// Line 84:				return idx;
			COPY $idx r7 // 472
			PUSH r7 // 475
			JMP @exit_tomb_string_indexOf // 477
			@then_ifstatement203: NOP // 481
// Line 85:			}
// Line 86:									
// Line 87:			idx += 1;
		COPY $idx r7 // 481
		ALIAS r9 $literalexpression211 // 484
		LOAD $literalexpression211 1 // 484
		ADD r7 $literalexpression211 r10 // 490
		COPY r10 $idx // 494
		JMP @loop_start_whilestatement194 // 497
		@loop_end_whilestatement194: NOP // 501
// Line 88:		}
// Line 89:		
// Line 90:		return -1;		// not found
	ALIAS r6 $literalexpression214 // 501
	LOAD $literalexpression214 1 // 501
	NEGATE $literalexpression214 $literalexpression214 // 507
	PUSH $literalexpression214 // 510
	JMP @exit_tomb_string_indexOf // 512
@exit_tomb_string_indexOf: // 515
RET // 516
";
	}
}

using System;

namespace DXShaderRestorer
{
	[Flags]
	internal enum ShaderInputComponentFlags
	{
		None	= 0x0,
		X		= 0x1,
		Y		= 0x2,
		Z		= 0x4,
		W		= 0x8,
		All		= 0xf
	}
}

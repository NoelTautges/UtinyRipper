using System.IO;

namespace DXShaderRestorer
{
	internal class ShaderInputSignature
	{
		public void Read(BinaryReader reader)
		{
			NameOffset = reader.ReadInt32();
			SemanticIndex = reader.ReadInt32();
			SystemValueType = reader.ReadInt32();
			ComponentType = (ShaderInputComponentType)reader.ReadInt32();
			Register = reader.ReadInt32();
			Mask = (ShaderInputComponentFlags)reader.ReadByte();
			ReadWriteMask = (ShaderInputComponentFlags)reader.ReadByte();
			reader.BaseStream.Position += 2;
		}

		public int NameOffset { get; set; }
		public int SemanticIndex { get; set; }
		public int SystemValueType { get; set; }
		public ShaderInputComponentType ComponentType { get; set; }
		public int Register { get; set; }
		public ShaderInputComponentFlags Mask { get; set; }
		public ShaderInputComponentFlags ReadWriteMask { get; set; }
	}
}

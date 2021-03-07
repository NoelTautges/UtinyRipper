using System.IO;

namespace DXShaderRestorer
{
	internal class ShaderInputSignatureChunk
	{
		public void Read(BinaryReader reader)
		{
			uint inputChunkLength = reader.ReadUInt32();
			uint inputCount = reader.ReadUInt32();
			InputSignatures = new ShaderInputSignature[inputCount];
			uint inputUnknown = reader.ReadUInt32();

			for (int i = 0; i < inputCount; i++)
			{
				InputSignatures[i] = new ShaderInputSignature();
				InputSignatures[i].Read(reader);
			}
		}

		public ShaderInputSignature[] InputSignatures { get; set; }
	}
}

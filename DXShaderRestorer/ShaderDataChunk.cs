using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DXShaderRestorer
{
	internal class ShaderDataChunk
	{
		private void AddInputDeclaration(ShaderInputSignature sig, byte[] inputBytes)
		{
			byte[] inputCopy = (byte[])inputBytes.Clone();
			inputCopy[4] &= 0xf;
			inputCopy[4] |= (byte)((int)sig.Mask << 4);
			inputCopy[8] = (byte)sig.Register;
			InputDeclarations.Add(inputCopy);
			AddedBytes += (uint)inputCopy.Length;
		}

		public void Read(BinaryReader reader, ShaderInputSignature[] inputSignatures)
		{
			ChunkLength = reader.ReadUInt32();
			uint shaderVersion = reader.ReadUInt32();
			DataLength = reader.ReadUInt32();
			int inputSignatureIndex = 0;

			while (reader.BaseStream.Position < reader.BaseStream.Length)
			{
				long pos = reader.BaseStream.Position;
				uint metadata = reader.ReadUInt32();
				uint opcode = metadata & 0x00007ff;
				int tokenLength = (int)((metadata & 0x7f000000) >> 22);

				// OPCODE_DCL_INPUT_PS in HLSLcc
				if (opcode == 98)
				{
					if (InputOffset == 0)
					{
						InputOffset = pos;
					}

					long operandPos = reader.BaseStream.Position;
					reader.BaseStream.Position = pos;
					byte[] inputBytes = reader.ReadBytes(tokenLength);
					reader.BaseStream.Position = operandPos;

					uint operandMetadata = reader.ReadUInt32();
					ShaderInputComponentFlags mask = (ShaderInputComponentFlags)((operandMetadata & 0x000000f0) >> 4);
					uint register = reader.ReadUInt32();

					for (int i = inputSignatureIndex; i < inputSignatures.Length; i++)
					{
						ShaderInputSignature sig = inputSignatures[i];

						if (sig.SystemValueType != 0)
						{
							continue;
						}
						else if (mask == sig.ReadWriteMask && register == sig.Register)
						{
							inputSignatureIndex = i + 1;
							break;
						}

						// Add input declaration modified to fit signature
						AddInputDeclaration(sig, inputBytes);
					}

					InputDeclarations.Add(inputBytes);
				}
				else if (InputOffset != 0 || tokenLength == 0)
				{
					reader.BaseStream.Position -= 4;
					break;
				}

				reader.BaseStream.Position = pos + tokenLength;
			}

			foreach (ShaderInputSignature sig in inputSignatures.Skip(inputSignatureIndex))
			{
				if (sig.SystemValueType != 0 || InputDeclarations.Count == 0)
				{
					break;
				}

				AddInputDeclaration(sig, InputDeclarations.Last());
			}

			InputEnd = reader.BaseStream.Position;
		}

		public uint ChunkLength { get; set; }
		public uint DataLength { get; set; }
		public long InputOffset { get; set; } = 0;
		public long InputEnd { get; set; }
		public List<byte[]> InputDeclarations { get; set; } = new List<byte[]>();
		public uint AddedBytes { get; set; } = 0;
	}
}

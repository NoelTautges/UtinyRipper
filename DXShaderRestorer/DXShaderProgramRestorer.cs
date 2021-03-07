using System.Collections.Generic;
using System.IO;
using System.Linq;
using uTinyRipper;
using uTinyRipper.Classes.Shaders;

using Version = uTinyRipper.Version;

namespace DXShaderRestorer
{
	public static class DXShaderProgramRestorer
	{
		public static byte[] RestoreProgramData(BinaryReader reader, Version version, ref ShaderSubProgram shaderSubProgram)
		{
			using (MemoryStream dest = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(dest))
				{
					uint baseOffset = (uint)reader.BaseStream.Position;
					byte[] magicBytes = reader.ReadBytes(4);
					byte[] checksum = reader.ReadBytes(16);
					uint unknown0 = reader.ReadUInt32();
					uint totalSize = reader.ReadUInt32();
					uint chunkCount = reader.ReadUInt32();
					List<uint> chunkOffsets = new List<uint>();
					for (int i = 0; i < chunkCount; i++)
					{
						chunkOffsets.Add(reader.ReadUInt32());
					}
					uint bodyOffset = (uint)reader.BaseStream.Position;
					ShaderInputSignatureChunk inputSignatureChunk = new ShaderInputSignatureChunk();
					ShaderDataChunk dataChunk = new ShaderDataChunk();

					// Check if shader already has resource chunk and correct input declarations
					foreach (uint chunkOffset in chunkOffsets)
					{
						reader.BaseStream.Position = chunkOffset + baseOffset;
						uint fourCc = reader.ReadUInt32();
						switch (fourCc)
						{
							// RDEF
							case 0x46454452:
								{
									reader.BaseStream.Position = baseOffset;
									byte[] original = reader.ReadBytes((int)reader.BaseStream.Length);
									return original;
								}
							// ISGN
							case 0x4E475349:
								inputSignatureChunk.Read(reader);
								break;
							// SHDR
							case 0x52444853:
								dataChunk.Read(reader, inputSignatureChunk.InputSignatures);
								break;
						}
					}
					reader.BaseStream.Position = bodyOffset;
					byte[] resourceChunkData = GetResourceChunk(version, ref shaderSubProgram);
					//Adjust for new chunk
					totalSize += (uint)resourceChunkData.Length;
					for (int i = 0; i < chunkCount; i++)
					{
						chunkOffsets[i] += (uint)resourceChunkData.Length + 4;
					}
					chunkOffsets.Insert(0, bodyOffset - baseOffset + 4);
					chunkCount += 1;
					totalSize += (uint)resourceChunkData.Length;
					totalSize += dataChunk.AddedBytes;

					writer.Write(magicBytes);
					writer.Write(checksum);
					writer.Write(unknown0);
					writer.Write(totalSize);
					writer.Write(chunkCount);
					foreach (uint chunkOffset in chunkOffsets)
					{
						writer.Write(chunkOffset);
					}
					writer.Write(resourceChunkData);

					if (dataChunk.InputOffset != 0)
					{
						byte[] preInputBytes = reader.ReadBytes((int)dataChunk.InputOffset - (int)reader.BaseStream.Position);
						writer.Write(preInputBytes);

						foreach (byte[] input in dataChunk.InputDeclarations)
						{
							writer.Write(input);
						}

						reader.BaseStream.Position = dataChunk.InputEnd;
						byte[] postInputBytes = reader.ReadBytes((int)reader.BaseStream.Length - (int)dataChunk.InputEnd);
						writer.Write(postInputBytes);

						writer.BaseStream.Position = chunkOffsets.Last() + 4;
						writer.Write(dataChunk.ChunkLength + dataChunk.AddedBytes);
						writer.BaseStream.Position += 4;
						writer.Write(dataChunk.DataLength + dataChunk.AddedBytes / 4);
					}
					else
					{
						byte[] rest = reader.ReadBytes((int)reader.BaseStream.Length - (int)reader.BaseStream.Position);
						writer.Write(rest);
					}

					return dest.ToArray();
				}
			}
		}

		private static byte[] GetResourceChunk(Version version, ref ShaderSubProgram shaderSubprogram)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (EndianWriter writer = new EndianWriter(memoryStream, EndianType.LittleEndian))
				{
					ResourceChunk resourceChunk = new ResourceChunk(version, ref shaderSubprogram);
					resourceChunk.Write(writer);
					//uint size = resourceChunk.Size;
					//if (memoryStream.Length != resourceChunk.Size) throw new Exception("Expected size does not match actual size");
					return memoryStream.ToArray();
				}
			}
		}
	}
}

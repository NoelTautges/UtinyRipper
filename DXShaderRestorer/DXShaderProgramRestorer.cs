using System;
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
					long inputOffset = 0;
					IEnumerable<byte[]> sortedInputs = Enumerable.Empty<byte[]>();
					// Check if shader already has resource chunk and sort input declarations
					foreach (uint chunkOffset in chunkOffsets)
					{
						reader.BaseStream.Position = chunkOffset + baseOffset;
						uint fourCc = reader.ReadUInt32();
						if (fourCc == RDEFFourCC)
						{
							reader.BaseStream.Position = baseOffset;
							byte[] original = reader.ReadBytes((int)reader.BaseStream.Length);
							return original;
						}
						else if (fourCc == SHDRFourCC)
						{
							uint chunkLength = reader.ReadUInt32();
							uint shaderVersion = reader.ReadUInt32();
							uint shaderLength = reader.ReadUInt32();
							List<byte[]> inputs = new List<byte[]>();

							while (reader.BaseStream.Position < reader.BaseStream.Length)
							{
								long pos = reader.BaseStream.Position;
								uint metadata = reader.ReadUInt32();
								uint opcode = metadata & 0x00007ff;
								int tokenLength = (int)((metadata & 0x7f000000) >> 22);

								// OPCODE_DCL_INPUT in HLSLcc
								if ((opcode != 95 && inputOffset != 0) || tokenLength == 0)
								{
									break;
								}
								
								if (opcode == 95)
								{
									if (inputOffset == 0)
									{
										inputOffset = pos;
									}

									reader.BaseStream.Position = pos;
									inputs.Add(reader.ReadBytes(tokenLength));
								}

								reader.BaseStream.Position = pos + tokenLength;
							}

							ShaderBindChannel[] channels = shaderSubProgram.BindChannels.Channels;
							sortedInputs = inputs
								.Select((x, i) => new KeyValuePair<byte[], int>(x, i))
								.OrderBy(pair => channels[pair.Value].Source)
								.Select(pair => pair.Key);
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
					byte[] rest = reader.ReadBytes((int)reader.BaseStream.Length - (int)reader.BaseStream.Position);
					writer.Write(rest);

					writer.BaseStream.Position = inputOffset + resourceChunkData.Length + 4;
					foreach (byte[] input in sortedInputs)
					{
						writer.Write(input);
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

		/// <summary>
		/// 'RDEF' ascii
		/// </summary>
		public const uint RDEFFourCC = 0x46454452;
		/// <summary>
		/// 'SHDR' ascii
		/// </summary>
		public const uint SHDRFourCC = 0x52444853;
	}
}

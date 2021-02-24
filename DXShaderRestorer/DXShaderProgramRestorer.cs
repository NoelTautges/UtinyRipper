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
					uint shaderChunkLength = 0;
					uint shaderDataLength = 0;
					uint addedBytes = 0;
					long shaderInputOffset = 0;
					long shaderInputEnd = 0;
					ShaderInputSignature[] inputSignatures = new ShaderInputSignature[0];
					IEnumerable<byte[]> sortedInputDeclarations = Enumerable.Empty<byte[]>();
					// Check if shader already has resource chunk and sort input declarations
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
								{
									uint inputChunkLength = reader.ReadUInt32();
									uint inputCount = reader.ReadUInt32();
									inputSignatures = new ShaderInputSignature[inputCount];
									uint inputUnknown = reader.ReadUInt32();

									for (int i = 0; i < inputCount; i++)
									{
										inputSignatures[i] = new ShaderInputSignature();
										inputSignatures[i].Read(reader);
									}
								}
								break;
							// SHDR
							case 0x52444853:
								{
									shaderChunkLength = reader.ReadUInt32();
									uint shaderVersion = reader.ReadUInt32();
									shaderDataLength = reader.ReadUInt32();
									List<byte[]> inputDeclarations = new List<byte[]>();
									bool pixelShader = false;
									int inputSignatureIndex = 0;

									while (reader.BaseStream.Position < reader.BaseStream.Length)
									{
										long pos = reader.BaseStream.Position;
										uint metadata = reader.ReadUInt32();
										uint opcode = metadata & 0x00007ff;
										int tokenLength = (int)((metadata & 0x7f000000) >> 22);

										// OPCODE_DCL_INPUT/OPCODE_DCL_INPUT_PS in HLSLcc
										if (opcode == 95 || opcode == 98)
										{
											if (shaderInputOffset == 0)
											{
												shaderInputOffset = pos;
											}

											long operandPos = reader.BaseStream.Position;
											reader.BaseStream.Position = pos;
											byte[] inputBytes = reader.ReadBytes(tokenLength);
											reader.BaseStream.Position = operandPos;

											if (opcode == 95)
											{
												inputDeclarations.Add(inputBytes);
											}
											else if (opcode == 98)
											{
												pixelShader = true;

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
													byte[] inputCopy = (byte[])inputBytes.Clone();
													inputCopy[4] &= 0xf;
													inputCopy[4] |= (byte)((int)inputSignatures[i].Mask << 4);
													inputCopy[8] = (byte)inputSignatures[i].Register;
													inputDeclarations.Add(inputCopy);
													addedBytes += (uint)inputCopy.Length;
												}

												inputDeclarations.Add(inputBytes);
											}
										}
										else if (shaderInputOffset != 0 || tokenLength == 0)
										{
											reader.BaseStream.Position -= 4;
											break;
										}

										reader.BaseStream.Position = pos + tokenLength;
									}

									shaderInputEnd = reader.BaseStream.Position;

									ShaderBindChannel[] channels = shaderSubProgram.BindChannels.Channels;
									sortedInputDeclarations = pixelShader ? inputDeclarations.AsEnumerable() : inputDeclarations
										.Select((x, i) => new KeyValuePair<byte[], int>(x, i))
										.OrderBy(pair => channels[pair.Value].Source)
										.Select(pair => pair.Key);
								}
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
					totalSize += addedBytes;

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

					if (shaderInputOffset != 0)
					{
						byte[] preInputBytes = reader.ReadBytes((int)shaderInputOffset - (int)reader.BaseStream.Position);
						writer.Write(preInputBytes);

						foreach (byte[] input in sortedInputDeclarations)
						{
							writer.Write(input);
						}

						reader.BaseStream.Position = shaderInputEnd;
						byte[] postInputBytes = reader.ReadBytes((int)reader.BaseStream.Length - (int)shaderInputEnd);
						writer.Write(postInputBytes);

						writer.BaseStream.Position = chunkOffsets.Last() + 4;
						writer.Write(shaderChunkLength + addedBytes);
						writer.BaseStream.Position += 4;
						writer.Write(shaderDataLength + addedBytes / 4);
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

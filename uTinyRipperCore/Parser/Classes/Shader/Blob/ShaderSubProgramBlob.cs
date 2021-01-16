using System;
using System.IO;
using System.Linq;
using uTinyRipper.Layout;
using K4os.Compression.LZ4;

namespace uTinyRipper.Classes.Shaders
{
	public struct ShaderSubProgramBlob
	{
		public void Read(AssetLayout layout, MemoryStream memStream, uint[] offsets, uint[] compressedLengths, uint[] decompressedLengths)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				uint offset = offsets[i];
				uint compressedLength = compressedLengths[i];
				uint decompressedLength = decompressedLengths[i];

				memStream.Position = offset;
				ReadBlob(layout, memStream, compressedLength, decompressedLength, i);
			}
		}

		public void Write(AssetLayout layout, MemoryStream memStream, out uint[] offsets, out uint[] compressedLengths, out uint[] decompressedLengths)
		{
			int segmentCount = Entries.Length == 0 ? 0 : Entries.Max(t => t.Segment) + 1;
			offsets = new uint[segmentCount];
			compressedLengths = new uint[segmentCount];
			decompressedLengths = new uint[segmentCount];
			for (int i = 0; i <= segmentCount && segmentCount > 0; i++)
			{
				if (i == segmentCount)
				{
					memStream.Position = offsets[0];
				}

				int index = i == segmentCount ? 0 : i;
				uint offset = (uint)memStream.Position;
				WriteBlob(layout, memStream, out uint compressedLength, out uint decompressedLength, index);

				offsets[index] = offset;
				compressedLengths[index] = compressedLength;
				decompressedLengths[index] = decompressedLength;
			}
		}

		public void Export(ShaderWriter writer, string header)
		{
			int j = 0;
			while (true)
			{
				int index = header.IndexOf(GpuProgramIndexName, j);
				if (index == -1)
				{
					break;
				}

				int length = index - j;
				writer.WriteString(header, j, length);
				j += length + GpuProgramIndexName.Length + 1;

				int subIndex = -1;
				for (int startIndex = j; j < header.Length; j++)
				{
					if (!char.IsDigit(header[j]))
					{
						string numberStr = header.Substring(startIndex, j - startIndex);
						subIndex = int.Parse(numberStr);
						break;
					}
				}

				// we don't know shader type so pass vertex
				SubPrograms[subIndex].Export(writer, ShaderType.Vertex);
			}
			writer.WriteString(header, j, header.Length - j);
		}

		private void ReadBlob(AssetLayout layout, MemoryStream memStream, uint compressedLength, uint decompressedLength, int segment)
		{
			byte[] compressedBuffer = new byte[compressedLength];
			byte[] decompressedBuffer = new byte[decompressedLength];
			memStream.Read(compressedBuffer, 0, (int)compressedLength);
			LZ4Codec.Decode(compressedBuffer, 0, (int)compressedLength, decompressedBuffer, 0, (int)decompressedLength);

			using (MemoryStream blobMem = new MemoryStream(decompressedBuffer))
			{
				using (AssetReader blobReader = new AssetReader(blobMem, EndianType.LittleEndian, layout))
				{
					if (segment == 0)
					{
						Entries = blobReader.ReadAssetArray<ShaderSubProgramEntry>();
						SubPrograms = new ShaderSubProgram[Entries.Length];
					}
					ReadSegment(blobReader, segment);
				}
			}
		}

		private void WriteBlob(AssetLayout layout, MemoryStream memStream, out uint compressedLength, out uint decompressedLength, int segment)
		{
			byte[] decompressedBuffer;

			using (MemoryStream blobMem = new MemoryStream())
			{
				using (AssetWriter blobWriter = new AssetWriter(blobMem, EndianType.LittleEndian, layout))
				{
					if (segment == 0)
					{
						blobWriter.WriteAssetArray(Entries);
					}
					WriteSegment(blobWriter, segment);
				}

				decompressedLength = (uint)blobMem.Length;
				decompressedBuffer = blobMem.ToArray();
			}

			byte[] compressedBuffer = new byte[LZ4Codec.MaximumOutputSize((int)decompressedLength)];
			// Unsure if compression level matters below max (which doesn't work) but I don't feel like trying out every value and level 8HC matches what the Unity Editor produces
			compressedLength = (uint)LZ4Codec.Encode(decompressedBuffer, 0, (int)decompressedLength, compressedBuffer, 0, compressedBuffer.Length, LZ4Level.L08_HC);
			memStream.Write(compressedBuffer, 0, (int)compressedLength);
		}

		private void ReadSegment(AssetReader reader, int segment)
		{
			for (int i = 0; i < Entries.Length; i++)
			{
				ref ShaderSubProgramEntry entry = ref Entries[i];
				if (entry.Segment == segment)
				{
					reader.BaseStream.Position = entry.Offset;
					SubPrograms[i].Read(reader);
					if (reader.BaseStream.Position != entry.Offset + entry.Length)
					{
						throw new Exception($"Read {reader.BaseStream.Position - entry.Offset} less than expected {entry.Length}");
					}
				}
			}
		}

		private void WriteSegment(AssetWriter writer, int segment)
		{
			for (int i = 0; i < Entries.Length; i++)
			{
				ref ShaderSubProgramEntry entry = ref Entries[i];
				if (entry.Segment == segment)
				{
					entry.Offset = (int)writer.BaseStream.Position;
					SubPrograms[i].Write(writer);
					entry.Length = (int)writer.BaseStream.Position - entry.Offset;
				}
			}
		}

		public ShaderSubProgramEntry[] Entries { get; set; }
		public ShaderSubProgram[] SubPrograms { get; set; }

		private const string GpuProgramIndexName = "GpuProgramIndex";
	}
}

using System;
using System.Collections.Generic;

namespace uTinyRipper.Classes.Shaders
{
	public struct ShaderSubProgram : IAssetReadable, IAssetWritable
	{
		/// <summary>
		/// 2019.1 and greater
		/// </summary>
		public static bool HasLocalKeywords(Version version) => version.IsGreaterEqual(2019);
		/// <summary>
		/// 5.5.0 and greater
		/// </summary>
		public static bool HasUAVParameters(Version version) => Shader.IsSerialized(version);
		/// <summary>
		/// 2017.2 and greater
		/// </summary>
		public static bool HasSamplerParameters(Version version) => version.IsGreaterEqual(2017, 1);
		/// <summary>
		/// 2017.3 and greater
		/// </summary>
		public static bool HasMultiSampled(Version version) => version.IsGreaterEqual(2017, 3);
		/// <summary>
		/// 5.5.0 and greater
		/// </summary>
		private static bool HasStatsTempRegister(Version version) => Shader.IsSerialized(version);
		/// <summary>
		/// 5.5.0 and greater
		/// </summary>
		private static bool IsAllParamArgs(Version version) => Shader.IsSerialized(version);
		/// <summary>
		/// 2017.3 and greater
		/// </summary>
		private static bool HasStructParameters(Version version) => version.IsGreaterEqual(2017, 3);
		/// <summary>
		/// 2018.2 and greater
		/// </summary>
		private static bool HasNewTextureParams(Version version) => version.IsGreaterEqual(2018, 2);
		private static int GetExpectedProgramVersion(Version version)
		{
			if (version.IsEqual(5, 3))
			{
				return 201509030;
			}
			else if (version.IsEqual(5, 4))
			{
				return 201510240;
			}
			else if (version.IsEqual(5, 5))
			{
				return 201608170;
			}
			else if (version.IsLess(2017, 3))
			{
				return 201609010;
			}
			else if (version.IsLess(2018, 2))
			{
				return 201708220;
			}
			else if (version.IsLess(2019))
			{
				return 201802150;
			}
			else
			{
				return 201806140;
			}
		}

		public void Read(AssetReader reader)
		{
			int version = reader.ReadInt32();
			if (version != GetExpectedProgramVersion(reader.Version))
			{
				throw new Exception($"Shader program version {version} doesn't match");
			}

			ProgramType = reader.ReadInt32();
			StatsALU = reader.ReadInt32();
			StatsTEX = reader.ReadInt32();
			StatsFlow = reader.ReadInt32();
			if (HasStatsTempRegister(reader.Version))
			{
				StatsTempRegister = reader.ReadInt32();
			}

			GlobalKeywords = reader.ReadStringArray();
			if (HasLocalKeywords(reader.Version))
			{
				LocalKeywords = reader.ReadStringArray();
			}
			ProgramData = reader.ReadByteArray();
			reader.AlignStream();

			int sourceMap = SourceMap = reader.ReadInt32();
			int bindCount = reader.ReadInt32();
			ShaderBindChannel[] channels = new ShaderBindChannel[bindCount];
			for (int i = 0; i < bindCount; i++)
			{
				uint source = reader.ReadUInt32();
				VertexComponent target = (VertexComponent)reader.ReadUInt32();
				ShaderBindChannel channel = new ShaderBindChannel(source, target);
				channels[i] = channel;
				sourceMap |= 1 << (int)source;
			}
			BindChannels = new ParserBindChannels(channels, sourceMap);

			List<VectorParameter> vectors = new List<VectorParameter>();
			List<MatrixParameter> matrices = new List<MatrixParameter>();
			List<TextureParameter> textures = new List<TextureParameter>();
			List<VectorParameter> structVectors = new List<VectorParameter>();
			List<MatrixParameter> structMatrices = new List<MatrixParameter>();
			List<BufferBinding> buffers = new List<BufferBinding>();
			List<UAVParameter> uavs = HasUAVParameters(reader.Version) ? new List<UAVParameter>() : null;
			List<SamplerParameter> samplers = HasSamplerParameters(reader.Version) ? new List<SamplerParameter>() : null;
			List<BufferBinding> constBindings = new List<BufferBinding>();
			List<StructParameter> structs = new List<StructParameter>();

			int paramGroupCount = reader.ReadInt32();
			ConstantBuffers = new ConstantBuffer[paramGroupCount - 1];
			for (int i = 0; i < paramGroupCount; i++)
			{
				vectors.Clear();
				matrices.Clear();
				structs.Clear();

				string name = reader.ReadString();
				int usedSize = reader.ReadInt32();
				int paramCount = reader.ReadInt32();
				for (int j = 0; j < paramCount; j++)
				{
					string paramName = reader.ReadString();
					ShaderParamType paramType = (ShaderParamType)reader.ReadInt32();
					int rows = reader.ReadInt32();
					int columns = reader.ReadInt32();
					bool isMatrix = reader.ReadInt32() > 0;
					int arraySize = reader.ReadInt32();
					int index = reader.ReadInt32();

					if (isMatrix)
					{
						MatrixParameter matrix = IsAllParamArgs(reader.Version) ?
							new MatrixParameter(paramName, paramType, index, arraySize, rows, columns) :
							new MatrixParameter(paramName, paramType, index, rows, columns);
						matrices.Add(matrix);
					}
					else
					{
						VectorParameter vector = IsAllParamArgs(reader.Version) ?
							new VectorParameter(paramName, paramType, index, arraySize, columns) :
							new VectorParameter(paramName, paramType, index, columns);
						vectors.Add(vector);
					}
				}

				if (HasStructParameters(reader.Version))
				{
					int structCount = reader.ReadInt32();
					for (int j = 0; j < structCount; j++)
					{
						structVectors.Clear();
						structMatrices.Clear();

						string structName = reader.ReadString();
						int index = reader.ReadInt32();
						int arraySize = reader.ReadInt32();
						int structSize = reader.ReadInt32();

						int strucParamCount = reader.ReadInt32();
						for (int k = 0; k < strucParamCount; k++)
						{
							string paramName = reader.ReadString();
							paramName = $"{structName}.{paramName}";
							ShaderParamType paramType = (ShaderParamType)reader.ReadInt32();
							int rows = reader.ReadInt32();
							int columns = reader.ReadInt32();
							bool isMatrix = reader.ReadInt32() > 0;
							int vectorArraySize = reader.ReadInt32();
							int paramIndex = reader.ReadInt32();

							if (isMatrix)
							{
								MatrixParameter matrix = IsAllParamArgs(reader.Version) ?
									new MatrixParameter(paramName, paramType, paramIndex, vectorArraySize, rows, columns) :
									new MatrixParameter(paramName, paramType, paramIndex, rows, columns);
								structMatrices.Add(matrix);
							}
							else
							{
								VectorParameter vector = IsAllParamArgs(reader.Version) ?
									new VectorParameter(paramName, paramType, paramIndex, vectorArraySize, columns) :
									new VectorParameter(paramName, paramType, paramIndex, columns);
								structVectors.Add(vector);
							}
						}

						StructParameter @struct = new StructParameter(structName, index, arraySize, structSize, structVectors.ToArray(), structMatrices.ToArray());
						structs.Add(@struct);
					}
				}
				if (i == 0)
				{
					VectorParameters = vectors.ToArray();
					MatrixParameters = matrices.ToArray();
					StructParameters = structs.ToArray();
				}
				else
				{
					ConstantBuffer constBuffer = new ConstantBuffer(name, matrices.ToArray(), vectors.ToArray(), structs.ToArray(), usedSize);
					ConstantBuffers[i - 1] = constBuffer;
				}
			}

			int paramGroup2Count = reader.ReadInt32();
			for (int i = 0; i < paramGroup2Count; i++)
			{
				string name = reader.ReadString();
				int type = reader.ReadInt32();
				int index = reader.ReadInt32();
				int extraValue = reader.ReadInt32();

				if (type == 0)
				{
					TextureParameter texture;
					if (HasNewTextureParams(reader.Version))
					{
						uint textureExtraValue = reader.ReadUInt32();
						bool isMultiSampled = (textureExtraValue & 1) == 1;
						byte dimension = (byte)(textureExtraValue >> 1);
						int samplerIndex = extraValue;
						texture = new TextureParameter(name, index, dimension, samplerIndex, isMultiSampled);
					}
					else if (HasMultiSampled(reader.Version))
					{
						uint textureExtraValue = reader.ReadUInt32();
						bool isMultiSampled = textureExtraValue == 1;
						byte dimension = unchecked((byte)extraValue);
						int samplerIndex = extraValue >> 8;
						if (samplerIndex == 0xFFFFFF)
						{
							samplerIndex = -1;
						}
						texture = new TextureParameter(name, index, dimension, samplerIndex, isMultiSampled);
					}
					else
					{
						byte dimension = unchecked((byte)extraValue);
						int samplerIndex = extraValue >> 8;
						if (samplerIndex == 0xFFFFFF)
						{
							samplerIndex = -1;
						}
						texture = new TextureParameter(name, index, dimension, samplerIndex);
					}
					textures.Add(texture);
				}
				else if (type == 1)
				{
					BufferBinding binding = new BufferBinding(name, index);
					constBindings.Add(binding);
				}
				else if (type == 2)
				{
					BufferBinding buffer = new BufferBinding(name, index);
					buffers.Add(buffer);
				}
				else if (type == 3 && HasUAVParameters(reader.Version))
				{
					UAVParameter uav = new UAVParameter(name, index, extraValue);
					uavs.Add(uav);
				}
				else if (type == 4 && HasSamplerParameters(reader.Version))
				{
					SamplerParameter sampler = new SamplerParameter((uint)extraValue, index);
					samplers.Add(sampler);
				}
				else
				{
					throw new Exception($"Unupported parameter type {type}");
				}
			}
			TextureParameters = textures.ToArray();
			BufferParameters = buffers.ToArray();
			if (HasUAVParameters(reader.Version))
			{
				UAVParameters = uavs.ToArray();
			}
			if (HasSamplerParameters(reader.Version))
			{
				SamplerParameters = samplers.ToArray();
			}
			ConstantBufferBindings = constBindings.ToArray();
			if (HasStructParameters(reader.Version))
			{
				StructParameters = structs.ToArray();
			}
		}

		public void Write(AssetWriter writer)
		{
			writer.Write(GetExpectedProgramVersion(writer.Version));

			writer.Write(ProgramType);
			writer.Write(StatsALU);
			writer.Write(StatsTEX);
			writer.Write(StatsFlow);
			if (HasStatsTempRegister(writer.Version))
			{
				writer.Write(StatsTempRegister);
			}

			writer.WriteArray(GlobalKeywords);
			if (HasLocalKeywords(writer.Version))
			{
				writer.WriteArray(LocalKeywords);
			}
			writer.WriteArray(ProgramData);
			writer.AlignStream();

			writer.Write(SourceMap);
			int bindCount = BindChannels.Channels.Length;
			writer.Write(bindCount);
			for (int i = 0; i < bindCount; i++)
			{
				ShaderBindChannel channel = BindChannels.Channels[i];
				writer.Write((uint)channel.Source);
				writer.Write((uint)channel.Target);
			}

			VectorParameter[] vectors;
			MatrixParameter[] matrices;
			StructParameter[] structs;

			int paramGroupCount = ConstantBuffers.Length + 1;
			writer.Write(paramGroupCount);
			for (int i = 0; i < paramGroupCount; i++)
			{
				if (i == 0)
				{
					vectors = VectorParameters;
					matrices = MatrixParameters;
					structs = StructParameters;

					writer.Write("");
					writer.Write(0);

				}
				else
				{
					ConstantBuffer constBuffer = ConstantBuffers[i - 1];
					vectors = constBuffer.VectorParams;
					matrices = constBuffer.MatrixParams;
					structs = constBuffer.StructParams;

					writer.Write(constBuffer.Name);
					writer.Write(constBuffer.Size);
				}

				writer.Write(vectors.Length + matrices.Length);
				foreach (MatrixParameter matrix in matrices)
				{
					writer.Write(matrix.Name);
					writer.Write((int)matrix.Type);
					writer.Write((int)matrix.RowCount);
					writer.Write((int)matrix.ColumnCount);
					writer.Write(1);
					writer.Write(IsAllParamArgs(writer.Version) ? matrix.ArraySize : 0);
					writer.Write(matrix.Index);
				}
				foreach (VectorParameter vector in vectors)
				{
					writer.Write(vector.Name);
					writer.Write((int)vector.Type);
					writer.Write(0);
					writer.Write((int)vector.Dim);
					writer.Write(0);
					writer.Write(IsAllParamArgs(writer.Version) ? vector.ArraySize : 0);
					writer.Write(vector.Index);
				}

				if (HasStructParameters(writer.Version))
				{
					writer.Write(structs.Length);
					foreach (StructParameter @struct in structs)
					{
						writer.Write(@struct.Name);
						writer.Write(@struct.Index);
						writer.Write(@struct.ArraySize);
						writer.Write(@struct.StructSize);

						writer.Write(@struct.VectorMembers.Length + @struct.MatrixMembers.Length);
						foreach (MatrixParameter matrix in @struct.MatrixMembers)
						{
							string[] nameParts = matrix.Name.Split('.');
							writer.Write(nameParts[nameParts.Length - 1]);
							writer.Write((int)matrix.Type);
							writer.Write(matrix.RowCount);
							writer.Write(matrix.ColumnCount);
							writer.Write(1);
							writer.Write(IsAllParamArgs(writer.Version) ? matrix.ArraySize : 0);
							writer.Write(matrix.Index);
						}
						foreach (VectorParameter vector in @struct.VectorMembers)
						{
							string[] nameParts = vector.Name.Split('.');
							writer.Write(nameParts[nameParts.Length - 1]);
							writer.Write((int)vector.Type);
							writer.Write(0);
							writer.Write((int)vector.Dim);
							writer.Write(1);
							writer.Write(IsAllParamArgs(writer.Version) ? vector.ArraySize : 0);
							writer.Write(vector.Index);
						}
					}
				}
			}

			writer.Write(TextureParameters.Length +
				BufferParameters.Length +
				(HasUAVParameters(writer.Version) ? UAVParameters.Length : 0) +
				(HasSamplerParameters(writer.Version) ? SamplerParameters.Length : 0) +
				ConstantBufferBindings.Length +
				(HasStructParameters(writer.Version) ? StructParameters.Length : 0));
			foreach (TextureParameter texture in TextureParameters)
			{
				writer.Write(texture.Name);
				writer.Write(0);
				writer.Write(texture.Index);
				if (HasNewTextureParams(writer.Version))
				{
					writer.Write(texture.SamplerIndex);
					writer.Write((uint)((texture.Dim << 1) & (texture.MultiSampled ? 1 : 0)));
				}
				else if (HasMultiSampled(writer.Version))
				{
					writer.Write(texture.SamplerIndex << 8 & texture.Dim);
					writer.Write((uint)(texture.MultiSampled ? 1 : 0));
				}
				else
				{
					writer.Write((texture.SamplerIndex == -1 ? 0xFFFFFF : texture.SamplerIndex) << 8 & texture.Dim);
				}
			}
			foreach (BufferBinding binding in ConstantBufferBindings)
			{
				writer.Write(binding.Name);
				writer.Write(1);
				writer.Write(binding.Index);
				writer.Write(0);
			}
			foreach (BufferBinding buffer in BufferParameters)
			{
				writer.Write(buffer.Name);
				writer.Write(2);
				writer.Write(buffer.Index);
				writer.Write(0);
			}
			if (HasUAVParameters(writer.Version))
			{
				foreach (UAVParameter uav in UAVParameters)
				{
					writer.Write(uav.Name);
					writer.Write(3);
					writer.Write(uav.Index);
					writer.Write(uav.OriginalIndex);
				}
			}
			if (HasSamplerParameters(writer.Version))
			{
				foreach (SamplerParameter sampler in SamplerParameters)
				{
					writer.Write("");
					writer.Write(4);
					writer.Write(sampler.BindPoint);
					writer.Write((int)sampler.Sampler);
				}
			}
		}

		public void Export(ShaderWriter writer, ShaderType type)
		{
			if (GlobalKeywords.Length > 0)
			{
				writer.Write("Keywords { ");
				foreach (string keyword in GlobalKeywords)
				{
					writer.Write("\"{0}\" ", keyword);
				}
				if (HasLocalKeywords(writer.Version))
				{
					foreach (string keyword in LocalKeywords)
					{
						writer.Write("\"{0}\" ", keyword);
					}
				}
				writer.Write("}\n");
				writer.WriteIndent(5);
			}

#warning TODO: convertion (DX to HLSL)
			ShaderGpuProgramType programType = GetProgramType(writer.Version);
			writer.Write("\"{0}", programType.ToProgramDataKeyword(writer.Platform, type));
			if (ProgramData.Length > 0)
			{
				writer.Write("\n");
				writer.WriteIndent(5);

				writer.WriteShaderData(ref this);
			}
			writer.Write('"');
		}

		public ShaderGpuProgramType GetProgramType(Version version)
		{
			if (ShaderGpuProgramTypeExtensions.GpuProgramType55Relevant(version))
			{
				return ((ShaderGpuProgramType55)ProgramType).ToGpuProgramType();
			}
			else
			{
				return ((ShaderGpuProgramType53)ProgramType).ToGpuProgramType();
			}
		}

		public int ProgramType { get; set; }
		public int StatsALU { get; set; }
		public int StatsTEX { get; set; }
		public int StatsFlow { get; set; }
		public int StatsTempRegister { get; set; }
		public string[] GlobalKeywords { get; set; }
		public string[] LocalKeywords { get; set; }
		public byte[] ProgramData { get; set; }
		public int SourceMap { get; set; }
		public VectorParameter[] VectorParameters { get; set; }
		public MatrixParameter[] MatrixParameters { get; set; }
		public TextureParameter[] TextureParameters { get; set; }
		public BufferBinding[] BufferParameters { get; set; }
		public UAVParameter[] UAVParameters { get; set; }
		public SamplerParameter[] SamplerParameters { get; set; }
		public ConstantBuffer[] ConstantBuffers { get; set; }
		public BufferBinding[] ConstantBufferBindings { get; set; }
		public StructParameter[] StructParameters { get; set; }

		public ParserBindChannels BindChannels;
	}
}

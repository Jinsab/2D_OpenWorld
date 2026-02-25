#if MEMORYPACK
using MemoryPack;
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Serializable data structure for mesh references and procedural mesh data.
	/// Supports both asset-based mesh references (via AssetProvider) and procedurally modified meshes.
	/// </summary>
	[MemoryPackable]
	public partial class MeshData
	{
		/// <summary>
		/// The name/path used to load the mesh via AssetProvider.
		/// If empty or null, the mesh is considered procedural.
		/// </summary>
		public string MeshAssetPath { get; set; }

		/// <summary>
		/// Whether this mesh contains procedural data that should be restored.
		/// </summary>
		public bool IsProceduralMesh { get; set; }

		/// <summary>
		/// Procedural mesh vertex positions (flattened: x,y,z,x,y,z...).
		/// </summary>
		public float[] Vertices { get; set; }

		/// <summary>
		/// Procedural mesh normals (flattened: x,y,z,x,y,z...).
		/// </summary>
		public float[] Normals { get; set; }

		/// <summary>
		/// Procedural mesh tangents (flattened: x,y,z,w,x,y,z,w...).
		/// </summary>
		public float[] Tangents { get; set; }

		/// <summary>
		/// Procedural mesh UV coordinates (flattened: u,v,u,v...).
		/// </summary>
		public float[] UV { get; set; }

		/// <summary>
		/// Procedural mesh UV2 coordinates (flattened: u,v,u,v...).
		/// </summary>
		public float[] UV2 { get; set; }

		/// <summary>
		/// Procedural mesh UV3 coordinates (flattened: u,v,u,v...).
		/// </summary>
		public float[] UV3 { get; set; }

		/// <summary>
		/// Procedural mesh UV4 coordinates (flattened: u,v,u,v...).
		/// </summary>
		public float[] UV4 { get; set; }

		/// <summary>
		/// Procedural mesh vertex colors (flattened: r,g,b,a,r,g,b,a...).
		/// </summary>
		public float[] Colors { get; set; }

		/// <summary>
		/// Procedural mesh bone weights for skinned meshes.
		/// Each entry contains 8 floats: 4 bone indices followed by 4 weights.
		/// </summary>
		public float[] BoneWeights { get; set; }

		/// <summary>
		/// Procedural mesh bind poses (flattened 4x4 matrices).
		/// </summary>
		public float[] BindPoses { get; set; }

		/// <summary>
		/// Number of submeshes in the mesh.
		/// </summary>
		public int SubMeshCount { get; set; }

		/// <summary>
		/// Submesh triangle indices. Each array element contains triangle indices for one submesh.
		/// </summary>
		public int[][] SubMeshTriangles { get; set; }

		/// <summary>
		/// Mesh bounds center (x, y, z).
		/// </summary>
		public float[] BoundsCenter { get; set; }

		/// <summary>
		/// Mesh bounds size (x, y, z).
		/// </summary>
		public float[] BoundsSize { get; set; }

		/// <summary>
		/// The name of the mesh.
		/// </summary>
		public string MeshName { get; set; }

		public MeshData() { }

		/// <summary>
		/// Creates MeshData from a mesh reference path (for asset-based meshes).
		/// </summary>
		public static MeshData FromAssetReference(string assetPath, string meshName)
		{
			return new MeshData
			{
				MeshAssetPath = assetPath,
				MeshName = meshName,
				IsProceduralMesh = false
			};
		}

		/// <summary>
		/// Creates MeshData from a Unity Mesh object (for procedural meshes).
		/// </summary>
		public static MeshData FromProceduralMesh(Mesh mesh, bool includeUV2 = false, bool includeUV3 = false, bool includeUV4 = false, bool includeColors = false, bool includeTangents = false, bool includeBoneWeights = false, bool includeBindPoses = false)
		{
			if (mesh == null)
			{
				return null;
			}

			MeshData data = new MeshData
			{
				MeshName = mesh.name,
				IsProceduralMesh = true,
				MeshAssetPath = string.Empty
			};

			// Vertices (required)
			Vector3[] vertices = mesh.vertices;
			data.Vertices = Vector3ArrayToFloatArray(vertices);

			// Normals
			Vector3[] normals = mesh.normals;
			if (normals != null && normals.Length > 0)
			{
				data.Normals = Vector3ArrayToFloatArray(normals);
			}

			// Tangents (optional)
			if (includeTangents)
			{
				Vector4[] tangents = mesh.tangents;
				if (tangents != null && tangents.Length > 0)
				{
					data.Tangents = Vector4ArrayToFloatArray(tangents);
				}
			}

			// UV (required for most meshes)
			Vector2[] uv = mesh.uv;
			if (uv != null && uv.Length > 0)
			{
				data.UV = Vector2ArrayToFloatArray(uv);
			}

			// UV2 (optional)
			if (includeUV2)
			{
				Vector2[] uv2 = mesh.uv2;
				if (uv2 != null && uv2.Length > 0)
				{
					data.UV2 = Vector2ArrayToFloatArray(uv2);
				}
			}

			// UV3 (optional)
			if (includeUV3)
			{
				Vector2[] uv3 = mesh.uv3;
				if (uv3 != null && uv3.Length > 0)
				{
					data.UV3 = Vector2ArrayToFloatArray(uv3);
				}
			}

			// UV4 (optional)
			if (includeUV4)
			{
				Vector2[] uv4 = mesh.uv4;
				if (uv4 != null && uv4.Length > 0)
				{
					data.UV4 = Vector2ArrayToFloatArray(uv4);
				}
			}

			// Colors (optional)
			if (includeColors)
			{
				Color[] colors = mesh.colors;
				if (colors != null && colors.Length > 0)
				{
					data.Colors = ColorArrayToFloatArray(colors);
				}
			}

			// Bone weights (optional, for skinned meshes)
			if (includeBoneWeights)
			{
				var allBoneWeights = mesh.GetAllBoneWeights();
				var bonesPerVertex = mesh.GetBonesPerVertex();
				if (allBoneWeights.Length > 0 && bonesPerVertex.Length > 0)
				{
					data.BoneWeights = BoneWeightsToFloatArray(allBoneWeights, bonesPerVertex);
				}
			}

			// Bind poses (optional, for skinned meshes)
			if (includeBindPoses)
			{
				Matrix4x4[] bindPoses = mesh.bindposes;
				if (bindPoses != null && bindPoses.Length > 0)
				{
					data.BindPoses = Matrix4x4ArrayToFloatArray(bindPoses);
				}
			}

			// Submeshes
			data.SubMeshCount = mesh.subMeshCount;
			data.SubMeshTriangles = new int[mesh.subMeshCount][];
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				data.SubMeshTriangles[i] = mesh.GetTriangles(i);
			}

			// Bounds
			Bounds bounds = mesh.bounds;
			data.BoundsCenter = new float[] { bounds.center.x, bounds.center.y, bounds.center.z };
			data.BoundsSize = new float[] { bounds.size.x, bounds.size.y, bounds.size.z };

			return data;
		}

		/// <summary>
		/// Applies this MeshData to a Unity Mesh object.
		/// </summary>
		public Mesh ToMesh()
		{
			if (!IsProceduralMesh)
			{
				return null;
			}

			Mesh mesh = new Mesh();
			mesh.name = MeshName ?? "ProceduralMesh";

			// Vertices
			if (Vertices != null && Vertices.Length > 0)
			{
				mesh.vertices = FloatArrayToVector3Array(Vertices);
			}

			// Normals
			if (Normals != null && Normals.Length > 0)
			{
				mesh.normals = FloatArrayToVector3Array(Normals);
			}

			// Tangents
			if (Tangents != null && Tangents.Length > 0)
			{
				mesh.tangents = FloatArrayToVector4Array(Tangents);
			}

			// UV
			if (UV != null && UV.Length > 0)
			{
				mesh.uv = FloatArrayToVector2Array(UV);
			}

			// UV2
			if (UV2 != null && UV2.Length > 0)
			{
				mesh.uv2 = FloatArrayToVector2Array(UV2);
			}

			// UV3
			if (UV3 != null && UV3.Length > 0)
			{
				mesh.uv3 = FloatArrayToVector2Array(UV3);
			}

			// UV4
			if (UV4 != null && UV4.Length > 0)
			{
				mesh.uv4 = FloatArrayToVector2Array(UV4);
			}

			// Colors
			if (Colors != null && Colors.Length > 0)
			{
				mesh.colors = FloatArrayToColorArray(Colors);
			}

			// Submeshes
			if (SubMeshTriangles != null && SubMeshTriangles.Length > 0)
			{
				mesh.subMeshCount = SubMeshTriangles.Length;
				for (int i = 0; i < SubMeshTriangles.Length; i++)
				{
					if (SubMeshTriangles[i] != null)
					{
						mesh.SetTriangles(SubMeshTriangles[i], i);
					}
				}
			}

			// Bounds
			if (BoundsCenter != null && BoundsCenter.Length >= 3 && BoundsSize != null && BoundsSize.Length >= 3)
			{
				mesh.bounds = new Bounds(
					new Vector3(BoundsCenter[0], BoundsCenter[1], BoundsCenter[2]),
					new Vector3(BoundsSize[0], BoundsSize[1], BoundsSize[2])
				);
			}

			// Recalculate if normals were not provided
			if (Normals == null || Normals.Length == 0)
			{
				mesh.RecalculateNormals();
			}

			return mesh;
		}

		#region Conversion Helpers

		private static float[] Vector3ArrayToFloatArray(Vector3[] vectors)
		{
			if (vectors == null) return null;
			float[] result = new float[vectors.Length * 3];
			for (int i = 0; i < vectors.Length; i++)
			{
				result[i * 3] = vectors[i].x;
				result[i * 3 + 1] = vectors[i].y;
				result[i * 3 + 2] = vectors[i].z;
			}
			return result;
		}

		private static Vector3[] FloatArrayToVector3Array(float[] floats)
		{
			if (floats == null || floats.Length < 3) return null;
			Vector3[] result = new Vector3[floats.Length / 3];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = new Vector3(floats[i * 3], floats[i * 3 + 1], floats[i * 3 + 2]);
			}
			return result;
		}

		private static float[] Vector4ArrayToFloatArray(Vector4[] vectors)
		{
			if (vectors == null) return null;
			float[] result = new float[vectors.Length * 4];
			for (int i = 0; i < vectors.Length; i++)
			{
				result[i * 4] = vectors[i].x;
				result[i * 4 + 1] = vectors[i].y;
				result[i * 4 + 2] = vectors[i].z;
				result[i * 4 + 3] = vectors[i].w;
			}
			return result;
		}

		private static Vector4[] FloatArrayToVector4Array(float[] floats)
		{
			if (floats == null || floats.Length < 4) return null;
			Vector4[] result = new Vector4[floats.Length / 4];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = new Vector4(floats[i * 4], floats[i * 4 + 1], floats[i * 4 + 2], floats[i * 4 + 3]);
			}
			return result;
		}

		private static float[] Vector2ArrayToFloatArray(Vector2[] vectors)
		{
			if (vectors == null) return null;
			float[] result = new float[vectors.Length * 2];
			for (int i = 0; i < vectors.Length; i++)
			{
				result[i * 2] = vectors[i].x;
				result[i * 2 + 1] = vectors[i].y;
			}
			return result;
		}

		private static Vector2[] FloatArrayToVector2Array(float[] floats)
		{
			if (floats == null || floats.Length < 2) return null;
			Vector2[] result = new Vector2[floats.Length / 2];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = new Vector2(floats[i * 2], floats[i * 2 + 1]);
			}
			return result;
		}

		private static float[] ColorArrayToFloatArray(Color[] colors)
		{
			if (colors == null) return null;
			float[] result = new float[colors.Length * 4];
			for (int i = 0; i < colors.Length; i++)
			{
				result[i * 4] = colors[i].r;
				result[i * 4 + 1] = colors[i].g;
				result[i * 4 + 2] = colors[i].b;
				result[i * 4 + 3] = colors[i].a;
			}
			return result;
		}

		private static Color[] FloatArrayToColorArray(float[] floats)
		{
			if (floats == null || floats.Length < 4) return null;
			Color[] result = new Color[floats.Length / 4];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = new Color(floats[i * 4], floats[i * 4 + 1], floats[i * 4 + 2], floats[i * 4 + 3]);
			}
			return result;
		}

		private static float[] Matrix4x4ArrayToFloatArray(Matrix4x4[] matrices)
		{
			if (matrices == null) return null;
			float[] result = new float[matrices.Length * 16];
			for (int i = 0; i < matrices.Length; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					result[i * 16 + j] = matrices[i][j];
				}
			}
			return result;
		}

		private static Matrix4x4[] FloatArrayToMatrix4x4Array(float[] floats)
		{
			if (floats == null || floats.Length < 16) return null;
			Matrix4x4[] result = new Matrix4x4[floats.Length / 16];
			for (int i = 0; i < result.Length; i++)
			{
				Matrix4x4 matrix = new Matrix4x4();
				for (int j = 0; j < 16; j++)
				{
					matrix[j] = floats[i * 16 + j];
				}
				result[i] = matrix;
			}
			return result;
		}

		private static float[] BoneWeightsToFloatArray(Unity.Collections.NativeArray<BoneWeight1> allBoneWeights, Unity.Collections.NativeArray<byte> bonesPerVertex)
		{
			// Store as: [vertexCount, bonesPerVertex[0], boneIndex0, weight0, ..., bonesPerVertex[1], ...]
			int totalFloats = 1 + bonesPerVertex.Length; // vertexCount + bonesPerVertex array
			for (int i = 0; i < bonesPerVertex.Length; i++)
			{
				totalFloats += bonesPerVertex[i] * 2; // Each bone weight has index + weight
			}

			float[] result = new float[totalFloats];
			result[0] = bonesPerVertex.Length; // Vertex count

			int floatIndex = 1;
			int boneWeightIndex = 0;

			for (int v = 0; v < bonesPerVertex.Length; v++)
			{
				result[floatIndex++] = bonesPerVertex[v]; // Number of bones for this vertex
				for (int b = 0; b < bonesPerVertex[v]; b++)
				{
					BoneWeight1 bw = allBoneWeights[boneWeightIndex++];
					result[floatIndex++] = bw.boneIndex;
					result[floatIndex++] = bw.weight;
				}
			}

			return result;
		}

		#endregion
	}
}
#endif

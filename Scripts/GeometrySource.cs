//Standard Shader Inspector Code:
//https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/StandardShaderGUI.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

using uid = System.UInt64;

namespace avs
{
	public enum NodeDataType : byte
	{
		Invalid,
		None,
		Mesh,
		Light,
		Bone
	};

	public enum NodeDataSubtype : byte
	{
		Invalid,
		None,
		Body,
		LeftHand,
		RightHand
	};

	public enum PrimitiveMode
	{
		POINTS, LINES, TRIANGLES, LINE_STRIP, TRIANGLE_STRIP
	};

	//! The standard glTF attribute semantics.
	public enum AttributeSemantic
	{
		//Name				Accessor Type(s)	Component Type(s)					Description
		POSITION,			//"VEC3"			5126 (FLOAT)						XYZ vertex positions
		NORMAL,				//"VEC3"			5126 (FLOAT)						Normalized XYZ vertex normals
		TANGENT,			//"VEC4"			5126 (FLOAT)						XYZW vertex tangents where the w component is a sign value(-1 or +1) indicating handedness of the tangent basis
		TEXCOORD_0,			//"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the first set
		TEXCOORD_1,			//"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		COLOR_0,			//"VEC3"
							//"VEC4"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	RGB or RGBA vertex color
		JOINTS_0,			//"VEC4"			5121 (UNSIGNED_BYTE)
							//					5123 (UNSIGNED_SHORT)				See Skinned Mesh Attributes
		WEIGHTS_0,			//"VEC4"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized
		TANGENTNORMALXZ,	//"VEC2"			UNSIGNED_INT						Simul: implements packed tangent-normal xz. Actually two VEC4's of BYTE.
							//					SIGNED_SHORT
		COUNT				//														This is the number of elements in enum class AttributeSemantic{};
							//														Must always be the last element in this enum class. 
	};

	public enum MaterialExtensionIdentifier : UInt32
	{
		SIMPLE_GRASS_WIND
	}

	public struct Attribute
	{
		public AttributeSemantic semantic;
		public uid accessor;
	};

	public struct PrimitiveArray
	{
		public UInt64 attributeCount;
		public Attribute[] attributes;
		public uid indices_accessor;
		public uid material;
		public PrimitiveMode primitiveMode;
	};

	public struct Accessor
	{
		public enum DataType
		{
			SCALAR,
			VEC2,
			VEC3,
			VEC4
		};
		public enum ComponentType
		{
			FLOAT,
			DOUBLE,
			HALF,
			UINT,
			USHORT,
			UBYTE,
			INT,
			SHORT,
			BYTE
		};
		public DataType type;
		public ComponentType componentType;
		public UInt64 count;
		public uid bufferView;
		public UInt64 byteOffset;
	};

	public struct BufferView
	{
		public uid buffer;
		public UInt64 byteOffset;
		public UInt64 byteLength;
		public UInt64 byteStride;
	};

	public struct GeometryBuffer
	{
		public UInt64 byteLength;
		public byte[] data;
	};

	public struct MaterialExtension
	{

	}

	//Just copied the Unreal texture formats, this will likely need changing.
	public enum TextureFormat
	{
		INVALID,
		G8,
		BGRA8,
		BGRE8,
		RGBA16,
		RGBA16F,
		RGBA8,
		RGBE8,
		D16F,
		D24F,
		D32F,
		MAX
	}

	public enum TextureCompression
	{
		UNCOMPRESSED = 0,
		BASIS_COMPRESSED
	}
	public enum RoughnessMode : byte
	{
		CONSTANT = 0,
		MULTIPLY_SMOOTHNESS,
		MULTIPLY_ROUGHNESS
	}

	[StructLayout(LayoutKind.Sequential)]
	public class TextureAccessor
	{
		public uid index = 0;
		public uid texCoord = 0;

		public Vector2 tiling = new Vector2(1.0f, 1.0f);
		public float strength = 1.0f;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class PBRMetallicRoughness
	{
		public TextureAccessor baseColorTexture = new TextureAccessor();
		public Vector4 baseColorFactor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

		public TextureAccessor metallicRoughnessTexture = new TextureAccessor();
		public float metallicFactor = 1.0f;
		public float roughOrSmoothMultiplier=-1.0f;
		public float roughOffset = 1.0f;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Node
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Transform transform;
		public NodeDataType dataType;
		public NodeDataSubtype dataSubtype;
		public uid parentID;
		public uid dataID;
		public uid skinID;

		public Vector4 lightColour;
		public Vector3 lightDirection;
		public float lightRadius;
		public byte lightType;

		public UInt64 animationAmount;
		public uid[] animationIDs;

		public UInt64 materialAmount;
		public uid[] materialIDs;

		public UInt64 childAmount;
		public uid[] childIDs;
	}

	public class Mesh
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Int64 primitiveArrayAmount;
		public PrimitiveArray[] primitiveArrays;

		public Int64 accessorAmount;
		public uid[] accessorIDs;
		public Accessor[] accessors;

		public Int64 bufferViewAmount;
		public uid[] bufferViewIDs;
		public BufferView[] bufferViews;

		public Int64 bufferAmount;
		public uid[] bufferIDs;
		public GeometryBuffer[] buffers;
	}

	public struct Skin
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Int64 bindMatrixAmount;
		public avs.Mat4x4[] inverseBindMatrices;

		public Int64 jointAmount;
		public uid[] jointIDs;

		public Transform rootTransform;
	}

	public struct Texture
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public uint width;
		public uint height;
		public uint depth;
		public uint bytesPerPixel;
		public uint arrayCount;
		public uint mipCount;

		public TextureFormat format;
		public TextureCompression compression;

		public uint dataSize;
		public IntPtr data;

		public uid samplerID;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public class Material
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public PBRMetallicRoughness pbrMetallicRoughness = new PBRMetallicRoughness();
		public TextureAccessor normalTexture = new TextureAccessor();
		public TextureAccessor occlusionTexture = new TextureAccessor();
		public TextureAccessor emissiveTexture = new TextureAccessor();
		public Vector3 emissiveFactor = new Vector3(1.0f, 1.0f, 1.0f);

		public UInt64 extensionAmount;
		[MarshalAs(UnmanagedType.ByValArray)]
		public MaterialExtensionIdentifier[] extensionIDs;
		[MarshalAs(UnmanagedType.ByValArray)]
		public MaterialExtension[] extensions;
	}
}

namespace teleport
{
	public struct TextureExtractionData
	{
		public uid id;
		public Texture unityTexture;
		public avs.Texture textureData;
	}

	public class GeometrySource : ScriptableObject, ISerializationCallbackReceiver
	{
		//Meta data on a resource loaded from disk.
		private struct LoadedResource
		{
			public uid oldID; //ID of the resource as it was loaded from disk; needs to be replaced.
			public IntPtr guid; //ID string of the asset that this resource relates to.
			public IntPtr name; //Name of asset that this resource relates to.
			public Int64 lastModified;
		}

		//Resources we have confirmed to still exist, and have assigned a new ID to.
		private struct ReaffirmedResource
		{
			public uid oldID;
			public uid newID;
		}

		#region DLLImports
		[DllImport("SimulCasterServer")]
		private static extern void DeleteUnmanagedArray(in IntPtr unmanagedArray);

		[DllImport("SimulCasterServer")]
		private static extern uid GenerateID();

		[DllImport("SimulCasterServer")]
		private static extern void SaveGeometryStore();
		[DllImport("SimulCasterServer")]
		private static extern void LoadGeometryStore(out UInt64 meshAmount, out IntPtr loadedMeshes, out UInt64 textureAmount, out IntPtr loadedTextures, out UInt64 materialAmount, out IntPtr loadedMaterials);
		//Tell the geometry store which resources still exist, and their new IDs.
		[DllImport("SimulCasterServer")]
		private static extern void ReaffirmResources(int meshAmount, ReaffirmedResource[] reaffirmedMeshes, int textureAmount, ReaffirmedResource[] reaffirmedTextures, int materialAmount, ReaffirmedResource[] reaffirmedMaterials);
		[DllImport("SimulCasterServer")]
		private static extern void ClearGeometryStore();

		[DllImport("SimulCasterServer")]
		private static extern void StoreNode(uid id, avs.Node node);
		[DllImport("SimulCasterServer")]
		private static extern void StoreSkin(uid id, avs.Skin skin);
		[DllImport("SimulCasterServer")]
		private static extern void StoreMesh(uid id,
												[MarshalAs(UnmanagedType.BStr)] string guid,
												Int64 lastModified,
												[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(MeshMarshaler))] avs.Mesh mesh,
												avs.AxesStandard extractToStandard);
		[DllImport("SimulCasterServer")]
		private static extern void StoreMaterial(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Material material);
		[DllImport("SimulCasterServer")]
		private static extern void StoreTexture(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Texture texture, string basisFileLocation);
		[DllImport("SimulCasterServer")]
		private static extern void StoreShadowMap(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Texture shadowMap);

		[DllImport("SimulCasterServer")]
		private static extern bool IsNodeStored(uid id);
		[DllImport("SimulCasterServer")]
		private static extern bool IsSkinStored(uid id);
		[DllImport("SimulCasterServer")]
		private static extern bool IsMeshStored(uid id);
		[DllImport("SimulCasterServer")]
		private static extern bool IsMaterialStored(uid id);
		[DllImport("SimulCasterServer")]
		private static extern bool IsTextureStored(uid id);

		[DllImport("SimulCasterServer")]
		private static extern void RemoveNode(uid id);

		[DllImport("SimulCasterServer")]
		private static extern UInt64 GetAmountOfTexturesWaitingForCompression();
		[DllImport("SimulCasterServer")]
		[return: MarshalAs(UnmanagedType.BStr)]
		private static extern string GetMessageForNextCompressedTexture(UInt64 textureIndex, UInt64 totalTextures);
		[DllImport("SimulCasterServer")]
		private static extern void CompressNextTexture();
		#endregion

		#region CustomSerialisation
		//Dictionaries aren't serialised, so to serialise them I am saving the data to key value arrays. 
		public UnityEngine.Object[] processedResources_keys = new UnityEngine.Object[0];
		public uid[] processedResources_values = new uid[0];
		#endregion

		public List<TextureExtractionData> texturesWaitingForExtraction = new List<TextureExtractionData>();
		public string compressedTexturesFolderPath;
		// <GameObject, ID of extracted data in native plug-in>
		private readonly Dictionary<UnityEngine.Object, uid> processedResources = new Dictionary<UnityEngine.Object, uid>();
		private bool isAwake = false;

		private static GeometrySource geometrySource = null;

		[SerializeField, HideInInspector] private SceneReferenceManager sceneReferenceManager = null;

		private Dictionary<uid, UnityEngine.Object> resourceMap = new Dictionary<uid, UnityEngine.Object>();

		// We always store the settings in this path:
		public const string k_GeometrySourcePath = "TeleportVR/GeometrySource";
		public const string k_GeometryFilename = "GeometrySource";

		public static GeometrySource GetGeometrySource()
		{
			if (geometrySource == null)
			{
				geometrySource = Resources.Load<GeometrySource>(k_GeometrySourcePath + "/" + k_GeometryFilename);
			}

#if UNITY_EDITOR
			if (geometrySource == null)
			{
				TeleportSettings.EnsureAssetPath("Assets/Resources/" + k_GeometrySourcePath);
				string assetPath = "Assets/Resources/" + k_GeometrySourcePath + "/" + k_GeometryFilename + ".asset";

				geometrySource = CreateInstance<GeometrySource>();
				AssetDatabase.CreateAsset(geometrySource, assetPath);
				AssetDatabase.SaveAssets();

				ClearGeometryStore();
				Debug.LogWarning("Created Geometry Source at: " + assetPath);
			}	
#endif

			return geometrySource;
		}

		public void OnBeforeSerialize()
		{
			processedResources_keys = processedResources.Keys.ToArray();
			processedResources_values = processedResources.Values.ToArray();
			processedResources.Clear();
		}

		public void OnAfterDeserialize()
		{
			//Don't run during boot.
			if (isAwake)
			{
				for (int i = 0; i < processedResources_keys.Length; i++)
				{
					processedResources[processedResources_keys[i]] = processedResources_values[i];
					//Debug.Log("Restoring resource " + processedResources_values[i]);// ;
				}
			}
		}

		public void Awake()
		{
			//We already have a GeometrySource, don't load from disk again and break the IDs.
			if(geometrySource != null)
			{
				return;
			}

			LoadFromDisk();

			isAwake = true;
		}

		public void OnEnable()
		{
			//Initialise static instance in GeometrySource when it is enabled after a hot-reload.
			GetGeometrySource();

			compressedTexturesFolderPath = Application.persistentDataPath + "/Basis Universal/";

			//Remove nodes that have been lost due to level change.
			var pairsToDelete = processedResources.Where(pair => pair.Key == null).ToArray();
			foreach (var pair in pairsToDelete)
			{
				RemoveNode(pair.Value);
				processedResources.Remove(pair.Key);
				resourceMap.Remove(pair.Value);
			}

			if(sceneReferenceManager == null)
			{
				sceneReferenceManager = CreateInstance<SceneReferenceManager>();
			}
		}

		public void SaveToDisk()
		{
			SaveGeometryStore();
		}

		public void LoadFromDisk()
		{
			processedResources.Clear();
			resourceMap.Clear();

			//Load data from files.
			LoadGeometryStore(out UInt64 meshAmount, out IntPtr loadedMeshes, out UInt64 textureAmount, out IntPtr loadedTextures, out UInt64 materialAmount, out IntPtr loadedMaterials);

			//Confirm resources loaded from disk still exists, and assign new IDs.
			List<ReaffirmedResource> reaffirmedMeshes = ReaffirmLoadedResources<Mesh>((int)meshAmount, loadedMeshes);
			List<ReaffirmedResource> reaffirmedTextures = ReaffirmLoadedResources<Texture>((int)textureAmount, loadedTextures);
			List<ReaffirmedResource> reaffirmedMaterials = ReaffirmLoadedResources<Material>((int)materialAmount, loadedMaterials);

			//Inform geometry store about resources that still exist, and pass new IDs.
			ReaffirmResources(reaffirmedMeshes.Count, reaffirmedMeshes.ToArray(), reaffirmedTextures.Count, reaffirmedTextures.ToArray(), reaffirmedMaterials.Count, reaffirmedMaterials.ToArray());

			//Delete unmanaged memory.
			DeleteUnmanagedArray(loadedMeshes);
			DeleteUnmanagedArray(loadedTextures);
			DeleteUnmanagedArray(loadedMaterials);
		}

		public void ClearData()
		{
			processedResources.Clear();
			texturesWaitingForExtraction.Clear();
			sceneReferenceManager.Clear();
			ClearGeometryStore();
		}

		//Returns the ID of the resource if it has been processed, or zero if the resource has not been processed or was passed in null.
		public uid FindResourceID(UnityEngine.Object resource)
		{
			if (!resource)
			{
				return 0;
			}

			processedResources.TryGetValue(resource, out uid nodeID);
			return nodeID;
		}

		public UnityEngine.Object FindResource(uid nodeID)
		{
			return (nodeID == 0) ? null : processedResources.FirstOrDefault(x => x.Value == nodeID).Key;
		}

		//If the passed collision layer is streamed.
		public bool IsCollisionLayerStreamed(int layer)
		{
			return (TeleportSettings.GetOrCreateSettings().LayersToStream & (1 << layer)) != 0;
		}

		//If the GameObject has been marked correctly to be streamed; i.e. on streamed collision layer and has the correct tag.
		public bool IsGameObjectMarkedForStreaming(GameObject gameObject)
		{
			string streamingTag = TeleportSettings.GetOrCreateSettings().TagToStream;
			return (streamingTag.Length == 0 || gameObject.CompareTag(streamingTag)) && IsCollisionLayerStreamed(gameObject.layer);
		}

		public List<GameObject> GetStreamableObjects()
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();

			//Find all GameObjects in open scenes that have the correct tag and collision layer to be streamed.
			List<GameObject> streamedObjects = new List<GameObject>(teleportSettings.TagToStream.Length > 0 ? GameObject.FindGameObjectsWithTag(teleportSettings.TagToStream) : FindObjectsOfType<GameObject>());
			for(int i = streamedObjects.Count - 1; i >= 0; i--)
			{
				GameObject gameObject = streamedObjects[i];
				if(!IsCollisionLayerStreamed(gameObject.layer))
				{
					streamedObjects.RemoveAt(i);
				}
			}

			//Add player body parts to list of streamed objects.
			CasterMonitor casterMonitor = CasterMonitor.GetCasterMonitor();
			List<GameObject> playerParts = casterMonitor.GetPlayerBodyParts();
			foreach(GameObject playerPart in playerParts)
			{
				//We don't want to duplicate it, so we remove it first; which only affects performance.
				streamedObjects.Remove(playerPart);
				streamedObjects.Add(playerPart);
			}

			return streamedObjects;
		}

		public uid AddNode(GameObject node, bool forceUpdate = false)
		{
			if(!node)
			{
				Debug.LogError("Failed to extract node from GameObject! Passed GameObject was null!");
				return 0;
			}

			//Just return the ID; if we have already processed the GameObject, the node can be found on the unmanaged side, and we are not forcing an update.
			if(processedResources.TryGetValue(node, out uid nodeID) && IsNodeStored(nodeID) && !forceUpdate)
			{
				return nodeID;
			}

			nodeID = nodeID == 0 ? GenerateID() : nodeID;
			processedResources[node] = nodeID;
			resourceMap[nodeID] = node;

			avs.Node extractedNode = new avs.Node();
			extractedNode.name = Marshal.StringToBSTR(node.name);
			extractedNode.transform = avs.Transform.FromLocalUnityTransform(node.transform);

			ExtractNodeHierarchy(node, ref extractedNode, forceUpdate);
			ExtractNodeSubType(node, ref extractedNode, forceUpdate);

			extractedNode.dataType = avs.NodeDataType.None;
			if(extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeMeshData(node, ref extractedNode, forceUpdate);
			}
			if (extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeSkinnedMeshData(node, ref extractedNode, forceUpdate);
			}
			if(extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeLightData(node, ref extractedNode, forceUpdate);
			}

			//Store extracted node.
			StoreNode(nodeID, extractedNode);
			return nodeID;
		}

		public uid AddMesh(Mesh mesh)
		{
			if(!mesh)
			{
				Debug.LogError("Mesh extraction failure! Null mesh passed to AddMesh(...) in GeometrySource!");
				return 0;
			}

			//We can't extract an unreadable mesh.
			if(!mesh.isReadable)
			{
				Debug.LogWarning($"Failed to extract mesh \"{mesh.name}\"! Mesh is unreadable!");
				return 0;
			}

			//Just return the ID; if we have already processed the mesh and the mesh can be found on the unmanaged side.
			if(processedResources.TryGetValue(mesh, out uid meshID) && IsMeshStored(meshID))
			{
				return meshID;
			}

			meshID = meshID == 0 ? GenerateID() : meshID;
				processedResources[mesh] = meshID;

				ExtractMeshData(avs.AxesStandard.EngineeringStyle, mesh, meshID);
				ExtractMeshData(avs.AxesStandard.GlStyle, mesh, meshID);

			return meshID;
		}

		public uid AddMaterial(Material material, bool forceUpdate = false)
		{
			if(!material)
			{
				return 0;
			}

			//Just return the ID; if we have already processed the material, the material can be found on the unmanaged side, and we are not forcing an update.
			if(processedResources.TryGetValue(material, out uid materialID) && IsMaterialStored(materialID) && !forceUpdate)
			{
				return materialID;
			}

			materialID = materialID == 0 ? GenerateID() : materialID;
			processedResources[material] = materialID;

				avs.Material extractedMaterial = new avs.Material();
				extractedMaterial.name = Marshal.StringToBSTR(material.name);

				extractedMaterial.pbrMetallicRoughness.baseColorTexture.index = AddTexture(material.mainTexture);
				extractedMaterial.pbrMetallicRoughness.baseColorTexture.tiling = material.mainTextureScale;
				if (material.HasProperty("_Color"))
				extractedMaterial.pbrMetallicRoughness.baseColorFactor = material.color;
				else
					extractedMaterial.pbrMetallicRoughness.baseColorFactor =new Color(1.0F,1.0F,1.0F,1.0F);

				Texture metallicRoughness=null;
				if (material.HasProperty("_MetallicGlossMap"))
					metallicRoughness = material.GetTexture("_MetallicGlossMap");
				float glossMapScale=1.0f;
				if (material.HasProperty("_GlossMapScale"))
					glossMapScale =material.GetFloat("_GlossMapScale");
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index = AddTexture(metallicRoughness);
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.tiling = material.mainTextureScale;

				extractedMaterial.pbrMetallicRoughness.metallicFactor = metallicRoughness ? 1.0f : (material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 1.0f); //Unity doesn't use the factor when the texture is set.

				float smoothness = metallicRoughness ? glossMapScale : (material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 1.0f);
				extractedMaterial.pbrMetallicRoughness.roughOrSmoothMultiplier = -smoothness;
				extractedMaterial.pbrMetallicRoughness.roughOffset = 1.0F;
				Texture normal=null;
				if (material.HasProperty("_BumpMap"))
				{
					normal = material.GetTexture("_BumpMap");
				extractedMaterial.normalTexture.index = AddTexture(normal);
				}
				extractedMaterial.normalTexture.tiling = material.mainTextureScale;
				if(material.HasProperty("_BumpScale"))
				{
					extractedMaterial.normalTexture.strength = material.GetFloat("_BumpScale");
				}
				else
				{
					extractedMaterial.normalTexture.strength = 1.0F;
				}

				Texture occlusion = null;
				if (material.HasProperty("_OcclusionMap"))
				{
					occlusion = material.GetTexture("_OcclusionMap");
				extractedMaterial.occlusionTexture.index = AddTexture(occlusion);
				}
				extractedMaterial.occlusionTexture.tiling = material.mainTextureScale;
				if(material.HasProperty("_OcclusionStrength"))
				{
					extractedMaterial.occlusionTexture.strength = material.GetFloat("_OcclusionStrength");
				}
				else
				{
					extractedMaterial.occlusionTexture.strength = 1.0F;
				}

				//Extract emission properties only if emission is active.
				if(!material.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack))
				{
					Texture emission = material.GetTexture("_EmissionMap");
					extractedMaterial.emissiveTexture.index = AddTexture(emission);
					extractedMaterial.emissiveTexture.tiling = material.mainTextureScale;
					if(material.HasProperty("_BumpScale"))
					{
						extractedMaterial.emissiveFactor = material.GetColor("_EmissionColor");
					}
					else
					{
						extractedMaterial.emissiveFactor = new avs.Vector3(0, 0, 0);
					}

				}

				extractedMaterial.extensionAmount = 0;
				extractedMaterial.extensionIDs = null;
				extractedMaterial.extensions = null;

#if UNITY_EDITOR
				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long _);
				StoreMaterial(materialID, guid, GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid)), extractedMaterial);
#endif

			return materialID;
		}

		public uid AddShadowMap(Texture shadowMap)
		{
			if(!shadowMap) return 0;

			throw new NotImplementedException();
		}

		public void AddTextureData(Texture texture, avs.Texture textureData)
		{
			if(!processedResources.TryGetValue(texture, out uid textureID))
			{
				Debug.LogError(texture.name + " had its data extracted, but is not in the dictionary of processed resources!");
				return;
			}

#if UNITY_EDITOR
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long _);

			string textureAssetPath = AssetDatabase.GetAssetPath(texture);
			long lastModified = GetAssetWriteTimeUTC(textureAssetPath);

			string basisFileLocation = "";
			//Basis Universal compression won't be used if the file location is left empty.
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			if(teleportSettings.casterSettings.useCompressedTextures)
			{
				string folderPath = compressedTexturesFolderPath;
				//Create directiory if it doesn't exist.
				if(!Directory.Exists(folderPath))
				{
					Directory.CreateDirectory(folderPath);
				}

				basisFileLocation = textureAssetPath; //Use editor file location as unique name; this won't work out of the Unity Editor.
				basisFileLocation = basisFileLocation.Replace("/", "#"); //Replace forward slashes with hashes.
				int idx = basisFileLocation.LastIndexOf('.');
				if(idx >= 0) basisFileLocation = basisFileLocation.Remove(idx); //Remove file extension.
				basisFileLocation = folderPath + basisFileLocation + ".basis"; //Combine folder path, unique name, and basis file extension to create basis file path and name.
			}

			StoreTexture(textureID, guid, lastModified, textureData, basisFileLocation);
#endif
		}

		//Extract bone and add to processed resources; will also add parent and child bones.
		//	bone : The bone we are extracting.
		//	boneList : Array of bones; usually taken from skinned mesh renderer.
		public uid AddBone(Transform bone, Transform[] boneList)
		{
			if(!boneList.Contains(bone))
			{
				return 0;
			}

			//Just return the ID; if we have already processed the transform and the bone can be found on the unmanaged side.
			if(processedResources.TryGetValue(bone, out uid boneID) && IsNodeStored(boneID))
			{
				return boneID;
			}

			//Add to processedResources first to prevent a stack overflow from recursion. 
			boneID = boneID == 0 ? GenerateID() : boneID;
			processedResources[bone] = boneID;

			//Avoid stack overflow by finding parent ID first; we don't get the chance to store the node in unmanaged code before we link up the bone hierarchy.
			uid parentID = FindResourceID(bone.parent);
			parentID = parentID == 0 ? AddBone(bone.parent, boneList) : parentID;

			avs.Node boneNode = new avs.Node();
			boneNode.name = Marshal.StringToBSTR(bone.name);
			boneNode.parentID = parentID;
			boneNode.transform = avs.Transform.FromLocalUnityTransform(bone);
			boneNode.dataType = avs.NodeDataType.Bone;

			boneNode.childAmount = (ulong)bone.childCount;
			boneNode.childIDs = new uid[boneNode.childAmount];

			for(int i = 0; i < bone.childCount; i++)
			{
				boneNode.childIDs[i] = AddBone(bone.GetChild(i), boneList);
			}

			StoreNode(boneID, boneNode);
			return boneID;
		}

		public void CompressTextures()
		{
			UInt64 totalTexturesToCompress = GetAmountOfTexturesWaitingForCompression();

#if UNITY_EDITOR
			for(UInt64 i = 0; i < totalTexturesToCompress; i++)
			{
				string compressionMessage = GetMessageForNextCompressedTexture(i, totalTexturesToCompress);

				bool cancelled = UnityEditor.EditorUtility.DisplayCancelableProgressBar("Compressing Textures", compressionMessage, i / (float)totalTexturesToCompress);
				if(cancelled) break;

				CompressNextTexture();
			}

			UnityEditor.EditorUtility.ClearProgressBar();
#endif
		}

		private void ExtractNodeHierarchy(GameObject source, ref avs.Node extractTo, bool forceUpdate)
		{
			if(source.transform.parent)
			{
				// Roderick: let's not do this. If a parent is to be streamed, let's make that explicit.
				// otherwise, we're proliferating huge numbers of streamables.
				//extractTo.parentID = FindResourceID(source.transform.parent.gameObject);
			}

			//Extract children of node, through transform hierarchy.
			List<uid> childIDs = new List<uid>();
			for(int i = 0; i < source.transform.childCount; i++)
			{
				uid childID = AddNode(source.transform.GetChild(i).gameObject, true);

				if(childID != 0)
				{
					childIDs.Add(childID);
				}
				else
				{
					Debug.LogWarning("Received 0 for ID of child on game object: " + source.name);
				}
			}
			extractTo.childAmount = (ulong)childIDs.Count;
			extractTo.childIDs = childIDs.ToArray();
		}

		private void ExtractNodeSubType(GameObject source, ref avs.Node extractTo, bool forceUpdate)
		{
			extractTo.dataSubtype = CasterMonitor.GetCasterMonitor().GetGameObjectBodyPart(source);
		}

		private void ExtractNodeMaterials(Material[] sourceMaterials, ref avs.Node extractTo, bool forceUpdate)
		{
			List<uid> materialIDs = new List<uid>();
			foreach(Material material in sourceMaterials)
			{
				uid materialID = AddMaterial(material, forceUpdate);

				if(materialID != 0)
				{
					materialIDs.Add(materialID);
					
					//Check GeometryStore to see if material actually exists, if it doesn't then there is a mismatch between the mananged code data and the unmanaged code data.
					if(!IsMaterialStored(materialID))
					{
						Debug.LogError($"Missing material {material.name}({materialID}), which was added to \"{extractTo.name}\".");
		}
				}
				else
				{
					Debug.LogWarning($"Received 0 for ID of material on game object: {extractTo.name}");
				}
			}
			extractTo.materialAmount = (ulong)materialIDs.Count;
			extractTo.materialIDs = materialIDs.ToArray();
		}

		private bool ExtractNodeMeshData(GameObject source, ref avs.Node extractTo, bool forceUpdate)
		{
			MeshFilter meshFilter = source.GetComponent<MeshFilter>();
			MeshRenderer meshRenderer = source.GetComponent<MeshRenderer>();
			if(!meshFilter || !meshRenderer || !meshRenderer.enabled)
			{
				return false;
			}

			//Extract mesh used on node.
			Mesh mesh = sceneReferenceManager.GetGameObjectMesh(source);
			extractTo.dataID = AddMesh(mesh);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				Debug.LogError($"Failed to extract node mesh data! Failed extraction of mesh \"{(mesh ? mesh.name : "NULL")}\" from GameObject \"{source.name}\"!");
				return false;
			}
			extractTo.dataType = avs.NodeDataType.Mesh;

			ExtractNodeMaterials(meshRenderer.sharedMaterials, ref extractTo, forceUpdate);

			return true;
		}

		private bool ExtractNodeSkinnedMeshData(GameObject source, ref avs.Node extractTo, bool forceUpdate)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = source.GetComponent<SkinnedMeshRenderer>();
			if(!skinnedMeshRenderer || !skinnedMeshRenderer.enabled || !skinnedMeshRenderer.rootBone)
			{
				return false;
			}

			//Extract mesh used on node.
			Mesh mesh = sceneReferenceManager.GetGameObjectMesh(source);
			extractTo.dataID = AddMesh(mesh);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				Debug.LogError($"Failed to extract skinned mesh data from GameObject: {source.name}");
				return false;
			}
			extractTo.dataType = avs.NodeDataType.Mesh;

			extractTo.skinID = AddSkin(skinnedMeshRenderer);

			//Animator component usually appears on the parent GameObject, so we need to use that instead for searching the children.
			GameObject animatorSource = (source.transform.parent) ? source.transform.parent.gameObject : source.transform.gameObject;
			Animator animator = animatorSource.GetComponentInChildren<Animator>();
			if(animator)
			{
				extractTo.animationIDs = AnimationExtractor.AddAnimations(animator);
				extractTo.animationAmount = (ulong)extractTo.animationIDs.Length;
			}
			else
			{
				extractTo.animationIDs = null;
				extractTo.animationAmount = 0;
			}

			ExtractNodeMaterials(skinnedMeshRenderer.sharedMaterials, ref extractTo, forceUpdate);

			return true;
			}

		private bool ExtractNodeLightData(GameObject source, ref avs.Node extractTo, bool forceUpdate)
		{
			Light light = source.GetComponent<Light>();
			if(!light || !light.isActiveAndEnabled)
			{
				return false;
			}

			if(!processedResources.TryGetValue(light, out extractTo.dataID))
			{
				extractTo.dataID = GenerateID();
			}
			processedResources[light] = extractTo.dataID;
			extractTo.dataType = avs.NodeDataType.Light;

			Color lightColour = light.color;
			extractTo.lightColour = (PlayerSettings.colorSpace == ColorSpace.Linear) ? lightColour.linear * light.intensity : lightColour.gamma * light.intensity;
			extractTo.lightType = (byte)light.type;
			extractTo.lightRadius = light.range / 5.0F;
			// For Unity, lights point along the X-axis, so:
			extractTo.lightDirection = new avs.Vector3(0, 0, 1.0F);

			return true;
		}

		private uid AddSkin(SkinnedMeshRenderer skinnedMeshRenderer)
		{
			//Just return the ID; if we have already processed the skin and the skin can be found on the unmanaged side.
			if(processedResources.TryGetValue(skinnedMeshRenderer, out uid skinID) && IsSkinStored(skinID))
			{
				return skinID;
			}

			skinID = skinID == 0 ? GenerateID() : skinID;
			processedResources[skinnedMeshRenderer] = skinID;

			avs.Skin skin = new avs.Skin();
			skin.name = Marshal.StringToBSTR(skinnedMeshRenderer.name);

			skin.inverseBindMatrices = ExtractInverseBindMatrices(skinnedMeshRenderer);
			skin.bindMatrixAmount = skin.inverseBindMatrices.Count();

			List<uid> jointIDs = new List<uid>();
			foreach(Transform bone in skinnedMeshRenderer.bones)
			{
				jointIDs.Add(AddBone(bone, skinnedMeshRenderer.bones));
			}

			skin.jointIDs = jointIDs.ToArray();
			skin.jointAmount = jointIDs.Count;

			skin.rootTransform = avs.Transform.FromLocalUnityTransform(skinnedMeshRenderer.rootBone.parent);

			StoreSkin(skinID, skin);
			return skinID;
		}

		private avs.Mat4x4[] ExtractInverseBindMatrices(SkinnedMeshRenderer skinnedMeshRenderer)
		{
			Mesh mesh = skinnedMeshRenderer.sharedMesh;

			avs.Mat4x4[] bindMatrices = new avs.Mat4x4[mesh.bindposes.Length];

			for(int i = 0; i < mesh.bindposes.Length; i++)
			{
				bindMatrices[i] = mesh.bindposes[i];
			}

			return bindMatrices;
		}

		private void ExtractMeshData(avs.AxesStandard extractToBasis, Mesh mesh, uid meshID)
		{
			avs.PrimitiveArray[] primitives = new avs.PrimitiveArray[mesh.subMeshCount];
			Dictionary<uid, avs.Accessor> accessors = new Dictionary<uid, avs.Accessor>(6);
			Dictionary<uid, avs.BufferView> bufferViews = new Dictionary<uid, avs.BufferView>(6);
			Dictionary<uid, avs.GeometryBuffer> buffers = new Dictionary<uid, avs.GeometryBuffer>(5);

			uid positionAccessorID = GenerateID();
			uid normalAccessorID = GenerateID();
			uid tangentAccessorID = GenerateID();
			uid uv0AccessorID = GenerateID();
			uid uv2AccessorID = mesh.uv2.Length != 0 ? GenerateID() : 0;
			uid jointAccessorID = 0;
			uid weightAccessorID = 0;

			//Generate IDs for joint and weight accessors if the model has bones.
			if(mesh.boneWeights.Length != 0)
			{
				jointAccessorID = GenerateID();
				weightAccessorID = GenerateID();
			}

			//Position Buffer:
			{
				CreateMeshBufferAndView(extractToBasis, mesh.vertices, buffers, bufferViews, out uid positionViewID);

				accessors.Add
				(
					positionAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC3,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.vertexCount,
						bufferView = positionViewID,
						byteOffset = 0
					}
				);
			}

			//Normal Buffer
			{
				CreateMeshBufferAndView(extractToBasis, mesh.normals, buffers, bufferViews, out uid normalViewID);

				accessors.Add
				(
					normalAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC3,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.normals.Length,
						bufferView = normalViewID,
						byteOffset = 0
					}
				);
			}

			//Tangent Buffer
			{
				CreateMeshBufferAndView(extractToBasis, mesh.tangents, buffers, bufferViews, out uid tangentViewID);

				accessors.Add
				(
					tangentAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC4,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.tangents.Length,
						bufferView = tangentViewID,
						byteOffset = 0
					}
				);
			}

			//UVs/Tex-Coords Buffer (All UVs are combined into a single buffer, with a view for each UV):
			{
				//Two floats per Vector2 * four bytes per float.
				int stride = 2 * 4;

				avs.GeometryBuffer uvBuffer = new avs.GeometryBuffer();
				uvBuffer.byteLength = (ulong)((mesh.uv.Length + mesh.uv2.Length) * stride);
				uvBuffer.data = new byte[uvBuffer.byteLength];

				//Get byte data from first UV channel.
				for(int i = 0; i < mesh.uv.Length; i++)
				{
					BitConverter.GetBytes(mesh.uv[i].x).CopyTo(uvBuffer.data, i * stride + 0);
					BitConverter.GetBytes(mesh.uv[i].y).CopyTo(uvBuffer.data, i * stride + 4);
				}

				int uv0Size = mesh.uv.Length * stride;
				//Get byte data from second UV channel.
				for(int i = 0; i < mesh.uv2.Length; i++)
				{
					BitConverter.GetBytes(mesh.uv2[i].x).CopyTo(uvBuffer.data, uv0Size + i * stride + 0);
					BitConverter.GetBytes(mesh.uv2[i].y).CopyTo(uvBuffer.data, uv0Size + i * stride + 4);
				}

				uid uvBufferID = GenerateID();
				uid uv0ViewID = GenerateID();
				uid uv2ViewID = mesh.uv2.Length != 0 ? GenerateID() : 0;

				buffers.Add(uvBufferID, uvBuffer);

				//Buffer view for first UV channel.
				bufferViews.Add
				(
					uv0ViewID,
					new avs.BufferView
					{
						buffer = uvBufferID,
						byteOffset = 0,
						byteLength = (ulong)(mesh.uv.Length * stride),
						byteStride = (ulong)stride
					}
				);

				if(mesh.uv2.Length != 0)
				{
					//Buffer view for second UV channel.
					bufferViews.Add
					(
						uv2ViewID,
						new avs.BufferView
						{
							buffer = uvBufferID,
							byteOffset = (ulong)(mesh.uv.Length * stride), //Offset is length of first UV channel.
							byteLength = (ulong)(mesh.uv2.Length * stride),
							byteStride = (ulong)stride
						}
					);
				}

				//Accessor for first UV channel.
				accessors.Add
				(
					uv0AccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC2,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.uv.Length,
						bufferView = uv0ViewID,
						byteOffset = 0
					}
				);

				if(mesh.uv2.Length != 0)
				{
					//Accessor for second UV channel.
					accessors.Add
					(
						uv2AccessorID,
						new avs.Accessor
						{
							type = avs.Accessor.DataType.VEC2,
							componentType = avs.Accessor.ComponentType.FLOAT,
							count = (ulong)mesh.uv2.Length,
							bufferView = uv2ViewID,
							byteOffset = 0
						}
					);
				}
			}

			//Joint and Weight Buffers
			if(mesh.boneWeights.Length != 0)
			{
				//Four bytes per int * four ints per BoneWeight.
				int jointStride = 4 * 4;
				//Four bytes per float * four floats per boneweight
				int weightStride = 4 * 4;

				///Buffers
				avs.GeometryBuffer jointBuffer = new avs.GeometryBuffer();
				jointBuffer.byteLength = (ulong)(mesh.boneWeights.Length * jointStride);
				jointBuffer.data = new byte[jointBuffer.byteLength];

				avs.GeometryBuffer weightBuffer = new avs.GeometryBuffer();
				weightBuffer.byteLength = (ulong)(mesh.boneWeights.Length * weightStride);
				weightBuffer.data = new byte[weightBuffer.byteLength];

				for(int i = 0; i < mesh.boneWeights.Length; i++)
				{
					BoneWeight boneWeight = mesh.boneWeights[i];

					BitConverter.GetBytes((float)boneWeight.boneIndex0).CopyTo(jointBuffer.data, i * jointStride + 00 * 4);
					BitConverter.GetBytes((float)boneWeight.boneIndex1).CopyTo(jointBuffer.data, i * jointStride + 01 * 4);
					BitConverter.GetBytes((float)boneWeight.boneIndex2).CopyTo(jointBuffer.data, i * jointStride + 02 * 4);
					BitConverter.GetBytes((float)boneWeight.boneIndex3).CopyTo(jointBuffer.data, i * jointStride + 03 * 4);

					BitConverter.GetBytes(boneWeight.weight0).CopyTo(weightBuffer.data, i * weightStride + 00 * 4);
					BitConverter.GetBytes(boneWeight.weight1).CopyTo(weightBuffer.data, i * weightStride + 01 * 4);
					BitConverter.GetBytes(boneWeight.weight2).CopyTo(weightBuffer.data, i * weightStride + 02 * 4);
					BitConverter.GetBytes(boneWeight.weight3).CopyTo(weightBuffer.data, i * weightStride + 03 * 4);
				}

				uid jointBufferID = GenerateID();
				uid weightBufferID = GenerateID();
				buffers.Add(jointBufferID, jointBuffer);
				buffers.Add(weightBufferID, weightBuffer);

				///Buffer Views
				avs.BufferView jointBufferView = new avs.BufferView
				{
					buffer = jointBufferID,
					byteOffset = 0,
					byteLength = jointBuffer.byteLength,
					byteStride = (ulong)jointStride
				};

				avs.BufferView weightBufferView = new avs.BufferView
				{
					buffer = weightBufferID,
					byteOffset = 0,
					byteLength = weightBuffer.byteLength,
					byteStride = (ulong)weightStride
				};

				uid jointBufferViewID = GenerateID();
				uid weightBufferViewID = GenerateID();
				bufferViews.Add(jointBufferViewID, jointBufferView);
				bufferViews.Add(weightBufferViewID, weightBufferView);

				///Accessors
				avs.Accessor jointAccessor = new avs.Accessor
				{
					type = avs.Accessor.DataType.VEC4,
					componentType = avs.Accessor.ComponentType.INT,
					count = (ulong)mesh.boneWeights.Length,
					bufferView = jointBufferViewID,
					byteOffset = 0
				};

				avs.Accessor weightAccessor = new avs.Accessor
				{
					type = avs.Accessor.DataType.VEC4,
					componentType = avs.Accessor.ComponentType.FLOAT,
					count = (ulong)mesh.boneWeights.Length,
					bufferView = weightBufferViewID,
					byteOffset = 0
				};

				accessors.Add(jointAccessorID, jointAccessor);
				accessors.Add(weightAccessorID, weightAccessor);
			}

			//Oculus Quest OVR rendering uses USHORTs for non-instanced meshes.
			int indexBufferStride = (extractToBasis == avs.AxesStandard.GlStyle || mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16) ? 2 : 4;

			//Index Buffer
			CreateIndexBufferAndView(indexBufferStride, mesh.triangles, buffers, bufferViews, out uid indexViewID);

			//Create Attributes
			for(int i = 0; i < primitives.Length; i++)
			{
				primitives[i].attributeCount = (ulong)(4 + (mesh.uv2.Length != 0 ? 1 : 0) + (mesh.boneWeights.Length != 0 ? 2 : 0));
				primitives[i].attributes = new avs.Attribute[primitives[i].attributeCount];
				primitives[i].primitiveMode = avs.PrimitiveMode.TRIANGLES;

				primitives[i].attributes[0] = new avs.Attribute { accessor = positionAccessorID, semantic = avs.AttributeSemantic.POSITION };
				primitives[i].attributes[1] = new avs.Attribute { accessor = normalAccessorID, semantic = avs.AttributeSemantic.NORMAL };
				primitives[i].attributes[2] = new avs.Attribute { accessor = tangentAccessorID, semantic = avs.AttributeSemantic.TANGENT };
				primitives[i].attributes[3] = new avs.Attribute { accessor = uv0AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_0 };

				int additionalAttributes = 0;
				if(mesh.uv2.Length != 0)
				{
					primitives[i].attributes[4 + additionalAttributes] = new avs.Attribute { accessor = uv2AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_1 };
					additionalAttributes += 1;
				}

				if(mesh.boneWeights.Length != 0)
				{
					primitives[i].attributes[4 + additionalAttributes] = new avs.Attribute { accessor = jointAccessorID, semantic = avs.AttributeSemantic.JOINTS_0 };
					primitives[i].attributes[5 + additionalAttributes] = new avs.Attribute { accessor = weightAccessorID, semantic = avs.AttributeSemantic.WEIGHTS_0 };
					additionalAttributes += 2;
				}

				primitives[i].indices_accessor = GenerateID();
				accessors.Add
				(
					primitives[i].indices_accessor,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.SCALAR,
						componentType = (indexBufferStride == 2) ? avs.Accessor.ComponentType.USHORT : avs.Accessor.ComponentType.UINT,
						count = mesh.GetIndexCount(i),
						bufferView = indexViewID,
						byteOffset = (UInt64)mesh.GetIndexStart(i) * (UInt64)indexBufferStride
					}
				);
			}

#if UNITY_EDITOR
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long _);
			long last_modified=GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid));
			StoreMesh
			(
				meshID,
				guid,
				last_modified,
				new avs.Mesh
				{
					name = Marshal.StringToBSTR(mesh.name),

					primitiveArrayAmount = primitives.Length,
					primitiveArrays = primitives,

					accessorAmount = accessors.Count,
					accessorIDs = accessors.Keys.ToArray(),
					accessors = accessors.Values.ToArray(),

					bufferViewAmount = bufferViews.Count,
					bufferViewIDs = bufferViews.Keys.ToArray(),
					bufferViews = bufferViews.Values.ToArray(),

					bufferAmount = buffers.Count,
					bufferIDs = buffers.Keys.ToArray(),
					buffers = buffers.Values.ToArray()
				},
				extractToBasis
			);
#endif
		}

		private void CreateIndexBufferAndView(int stride, in int[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, out uid bufferViewID)
		{
			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Convert to ushort for stride 2.
			if(stride == 2)
			{
				for(int i = 0; i < data.Length; i++)
				{
					BitConverter.GetBytes((ushort)data[i]).CopyTo(newBuffer.data, i * stride);
				}
			}
			//Convert to uint for stride 4.
			else
			{
				if(stride != 4) Debug.LogError($"CreateIndexBufferAndView(...) received invalid stride of {stride}! Extracting as uint!");

				for(int i = 0; i < data.Length; i++)
				{
					BitConverter.GetBytes((uint)data[i]).CopyTo(newBuffer.data, i * stride);
				}
			}

			uid bufferID = GenerateID();
			bufferViewID = GenerateID();

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private void CreateMeshBufferAndView(avs.AxesStandard extractToBasis, in Vector3[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, out uid bufferViewID)
		{
			//Three floats per Vector3 * four bytes per float.
			int stride = 3 * 4;

			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Get byte data from each Vector3, and copy into buffer.
			switch(extractToBasis)
			{
				case avs.AxesStandard.GlStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(-data[i].z).CopyTo(newBuffer.data, i * stride + 8);
					}

					break;
				case avs.AxesStandard.EngineeringStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 8);
					}

					break;
				default:
					Debug.LogError("Attempted to extract mesh buffer data with unsupported axes standard of:" + extractToBasis);
					break;
			}

			uid bufferID = GenerateID();
			bufferViewID = GenerateID();

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private void CreateMeshBufferAndView(avs.AxesStandard extractToBasis, in Vector4[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, out uid bufferViewID)
		{
			//Four floats per Vector4 * four bytes per float.
			int stride = 4 * 4;

			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Get byte data from each Vector4, and copy into buffer.
			switch(extractToBasis)
			{
				case avs.AxesStandard.GlStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(-data[i].z).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes(data[i].w).CopyTo(newBuffer.data, i * stride + 12);
					}

					break;
				case avs.AxesStandard.EngineeringStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes(data[i].w).CopyTo(newBuffer.data, i * stride + 12);
					}

					break;
				default:
					Debug.LogError("Attempted to extract mesh buffer data with unsupported axes standard of:" + extractToBasis);
					break;
			}

			uid bufferID = GenerateID();
			bufferViewID = GenerateID();

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private static uint GetBytesPerPixel(TextureFormat format)
		{
			switch(format)
			{
				case TextureFormat.Alpha8: return 1;
				case TextureFormat.ARGB4444: return 2;
				case TextureFormat.RGB24: return 3;
				case TextureFormat.RGBA32: return 4;
				case TextureFormat.ARGB32: return 4;
				case TextureFormat.RGB565: return 2;
				case TextureFormat.R16: return 2;
				case TextureFormat.DXT1: return 1; //0.5 = 4bits
				case TextureFormat.DXT5: return 1;
				case TextureFormat.RGBA4444: return 2;
				case TextureFormat.BGRA32: return 4;
				case TextureFormat.RHalf: return 2;
				case TextureFormat.RGHalf: return 4;
				case TextureFormat.RGBAHalf: return 8;
				case TextureFormat.RFloat: return 4;
				case TextureFormat.RGFloat: return 8;
				case TextureFormat.RGBAFloat: return 16;
				case TextureFormat.YUY2: return 2;
				case TextureFormat.RGB9e5Float: return 4;
				case TextureFormat.BC4: return 1; //0.5 = 4bits
				case TextureFormat.BC5: return 1;
				case TextureFormat.BC6H: return 1;
				case TextureFormat.BC7: return 1;
				case TextureFormat.DXT1Crunched: return 1; //0.5 = 4bits
				case TextureFormat.DXT5Crunched: return 1;
				case TextureFormat.PVRTC_RGB2: return 1; //0.25 = 2bits
				case TextureFormat.PVRTC_RGBA2: return 1; //0.25 = 2bits
				case TextureFormat.PVRTC_RGB4: return 1; //0.5 = 4bits
				case TextureFormat.PVRTC_RGBA4: return 1; //0.5 = 4bits
				case TextureFormat.ETC_RGB4: return 1; //0.5 = 4bits
				case TextureFormat.EAC_R: return 1; //0.5 = 4bits
				case TextureFormat.EAC_R_SIGNED: return 1; //0.5 = 4bits
				case TextureFormat.EAC_RG: return 1;
				case TextureFormat.EAC_RG_SIGNED: return 1;
				case TextureFormat.ETC2_RGB: return 1; //0.5 = 4bits
				case TextureFormat.ETC2_RGBA1: return 1; //0.5 = 4bits
				case TextureFormat.ETC2_RGBA8: return 1;
				//These are duplicates of ASTC_RGB_0x0, and not implemented in Unity 2018:
				//case TextureFormat.ASTC_4x4: return 8;
				//case TextureFormat.ASTC_5x5: return 8;
				//case TextureFormat.ASTC_6x6: return 8;
				//case TextureFormat.ASTC_8x8: return 8;
				//case TextureFormat.ASTC_10x10: return 8;
				//case TextureFormat.ASTC_12x12: return 8;
				case TextureFormat.RG16: return 2;
				case TextureFormat.R8: return 1;
				case TextureFormat.ETC_RGB4Crunched: return 1; //0.5 = 4bits
				case TextureFormat.ETC2_RGBA8Crunched: return 1;
#if UNITY_2019_0_OR_NEWER
			//These are not implemented in Unity 2018:
			case TextureFormat.ASTC_HDR_4x4: return 8;
			case TextureFormat.ASTC_HDR_5x5: return 8;
			case TextureFormat.ASTC_HDR_6x6: return 8;
			case TextureFormat.ASTC_HDR_8x8: return 8;
			case TextureFormat.ASTC_HDR_10x10: return 8;
			case TextureFormat.ASTC_HDR_12x12: return 8;
#endif
				case TextureFormat.ASTC_4x4: return 8;
				case TextureFormat.ASTC_5x5: return 8;
				case TextureFormat.ASTC_6x6: return 8;
				case TextureFormat.ASTC_8x8: return 8;
				case TextureFormat.ASTC_10x10: return 8;
				case TextureFormat.ASTC_12x12: return 8;
#if UNITY_2019_3_OR_OLDER
				case TextureFormat.ASTC_4x4: return 8;
				case TextureFormat.ASTC_5x5: return 8;
				case TextureFormat.ASTC_6x6: return 8;
				case TextureFormat.ASTC_8x8: return 8;
				case TextureFormat.ASTC_10x10: return 8;
				case TextureFormat.ASTC_12x12: return 8;
#endif
				default:
					Debug.LogWarning("Defaulting to <color=red>8</color> bytes per pixel as format is unsupported: " + format);
					return 8;
			}
		}

		private uid AddTexture(Texture texture)
		{
			if(!texture)
			{
				return 0;
			}

			//Just return the ID; if we have already processed the texture and the texture can be found on the unmanaged side.
			if(processedResources.TryGetValue(texture, out uid textureID) && IsTextureStored(textureID))
			{
				return textureID;
			}

			//We can't extract textures in play-mode.
				if(Application.isPlaying)
				{
					Debug.LogWarning("Texture <b>" + texture.name + "</b> has not been extracted, but is being used on streamed geometry!");
					return 0;
				}

			textureID = textureID == 0 ? GenerateID() : textureID;
			processedResources[texture] = textureID;

				avs.Texture extractedTexture = new avs.Texture()
				{
					name = Marshal.StringToBSTR(texture.name),

					width = (uint)texture.width,
					height = (uint)texture.height,
					mipCount = 1,

					format = avs.TextureFormat.INVALID, //Assumed
					compression = avs.TextureCompression.UNCOMPRESSED, //Assumed

					samplerID = 0
				};

				switch(texture)
				{
					case Texture2D texture2D:
						extractedTexture.depth = 1;
						extractedTexture.arrayCount = 1;
						extractedTexture.mipCount = (uint)texture2D.mipmapCount;
						break;
					case Texture2DArray texture2DArray:
						extractedTexture.depth = 1;
						extractedTexture.arrayCount = (uint)texture2DArray.depth;
						extractedTexture.bytesPerPixel = GetBytesPerPixel(texture2DArray.format);
						break;
					case Texture3D texture3D:
						extractedTexture.depth = (uint)texture3D.depth;
						extractedTexture.arrayCount = 1;
						extractedTexture.bytesPerPixel = GetBytesPerPixel(texture3D.format);
						break;
					default:
						Debug.LogError("Passed texture was unsupported type: " + texture.GetType() + "!");
						return 0;
				}

				texturesWaitingForExtraction.Add(new TextureExtractionData { id = textureID, unityTexture = texture, textureData = extractedTexture });
			return textureID;
		}

		private long GetAssetWriteTimeUTC(string filePath)
		{
			try
			{
				return File.GetLastWriteTimeUtc(filePath).ToFileTimeUtc();
			}
			catch(Exception e)
			{
				Debug.LogError("Failed to get last write time for "+filePath);
				return 0;
			}
		}

		//Confirms resources loaded from disk of a certain Unity asset type still exist, and creates a pairing of their old IDs and their newly assigned IDs.
		//  resourceAmount : Amount of resources in loadedResources.
		//  loadedResources : LoadedResource array that was created in unmanaged memory.
		//Returns list of resources that have been confirmed to exist, with their new ID assigned.
		private List<ReaffirmedResource> ReaffirmLoadedResources<UnityAsset>(int resourceAmount, in IntPtr loadedResources) where UnityAsset : UnityEngine.Object
		{
			List<ReaffirmedResource> reaffirmedResources = new List<ReaffirmedResource>();

			int resourceSize = Marshal.SizeOf<LoadedResource>();
			//Go through each resource, confirm it still exists, and create a new reaffirmed resource with their new ID if it does.
			for(int i = 0; i < resourceAmount; i++)
			{
				//Create new pointer to the memory location of the LoadedResource for this index.
				IntPtr resourcePtr = new IntPtr(loadedResources.ToInt64() + i * resourceSize);

				//Marshal data to usuable types.
				LoadedResource metaResource = Marshal.PtrToStructure<LoadedResource>(resourcePtr);
				string guid = Marshal.PtrToStringBSTR(metaResource.guid);
				string name = Marshal.PtrToStringBSTR(metaResource.name);

#if UNITY_EDITOR
				//Asset we found in the database.
				UnityAsset asset = null;

				//Attempt to find asset.
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);
				UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
				foreach(UnityEngine.Object unityObject in assetsAtPath)
				{ 
					if((unityObject.GetType() == typeof(UnityAsset) || unityObject.GetType().IsSubclassOf(typeof(UnityAsset))) && unityObject.name == name)
					{
						asset = (UnityAsset)unityObject;
						break;
					}
				}

				if(asset)
				{
					long lastModified = GetAssetWriteTimeUTC(assetPath);

					//Use the asset as is, if it has not been modified since it was saved.
					if(metaResource.lastModified >= lastModified)
					{
						uid newID = GenerateID();

						reaffirmedResources.Add(new ReaffirmedResource { oldID = metaResource.oldID, newID = newID });
						// Not spamming here unless debugging this feature:
						//Debug.Log("Reaffirmed resource was " + metaResource.oldID + " now " + newID + " loaded from disk, " + assetPath);
						processedResources[asset] = newID;
						resourceMap[newID] = asset;
					}
				}
				else
				{
					Debug.Log($"Disposed of missing {typeof(UnityAsset).FullName} \"{name}\" with GUID \"{guid}\"!");
				}
#endif
			}

			return reaffirmedResources;
		}
	}
}

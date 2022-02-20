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
			SCALAR=1,
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
		RGBAFloat,
		MAX
	}

	public enum TextureCompression
	{
		UNCOMPRESSED = 0,
		BASIS_COMPRESSED,
		PNG
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
		public byte texCoord = 0;

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

	/// <summary>
	/// Node properties for sending from C# to C++ dll.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct Node
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Transform localTransform;
		public Transform globalTransform;

		[MarshalAs(UnmanagedType.I1)]
		public bool stationary;
		public uid ownerClientId;
		public NodeDataType dataType;
		public uid parentID;
		public uid dataID;
		public uid skinID;

		public Vector4 lightColour;
		public Vector3 lightDirection;
		public float lightRadius;
		public float lightRange;
		public byte lightType;

		public UInt64 numAnimations;
		public uid[] animationIDs;

		public UInt64 numMaterials;
		public uid[] materialIDs;

		public NodeRenderState renderState;

		public UInt64 numChildren;
		public uid[] childIDs;

		public int priority;
	}

	public class Mesh
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Int64 numPrimitiveArrays;
		public PrimitiveArray[] primitiveArrays;

		public Int64 numAccessors;
		public uid[] accessorIDs;
		public Accessor[] accessors;

		public Int64 numBufferViews;
		public uid[] bufferViewIDs;
		public BufferView[] bufferViews;

		public Int64 numBuffers;
		public uid[] bufferIDs;
		public GeometryBuffer[] buffers;
	}

	public struct Skin
	{
		[MarshalAs(UnmanagedType.BStr)]
		public IntPtr name;

		public Int64 numInverseBindMatrices;
		public avs.Mat4x4[] inverseBindMatrices;

		public Int64 numBones;
		public uid[] boneIDs;

		public Int64 numJoints;
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

		public float valueScale;
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
		public Vector3 emissiveFactor = new Vector3(0.0f,0.0f,0.0f);

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
			public uid id;		//ID of the resource generated by the dll on load.
			public IntPtr guid; //ID string of the asset that this resource relates to.
			public IntPtr name; //Name of asset that this resource relates to.
			public Int64 lastModified;
		}

		[Flags]
		public enum ForceExtractionMask
		{
			FORCE_NOTHING = 0,

			FORCE_NODES = 1,
			FORCE_HIERARCHIES = 2,
			FORCE_SUBRESOURCES = 4,
			FORCE_UNCOMPRESSED = 8,
			FORCE_TEXTURES = 16,

			FORCE_EVERYTHING = -1,

			//Combinations
			FORCE_NODES_AND_HIERARCHIES = FORCE_NODES | FORCE_HIERARCHIES,
			FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES = FORCE_NODES | FORCE_HIERARCHIES | FORCE_SUBRESOURCES | FORCE_TEXTURES
		}

		#region DLLImports

		[DllImport("TeleportServer")]
		private static extern void SetCachePath(string name);
		[DllImport("TeleportServer")]
		private static extern void DeleteUnmanagedArray(in IntPtr unmanagedArray);

		[DllImport("TeleportServer")]
		private static extern uid GenerateID();

		[DllImport("TeleportServer")]
		private static extern void SaveGeometryStore();
		[DllImport("TeleportServer")]
		private static extern void LoadGeometryStore(out UInt64 meshAmount, out IntPtr loadedMeshes, out UInt64 textureAmount, out IntPtr loadedTextures, out UInt64 numMaterials, out IntPtr loadedMaterials);
	
		[DllImport("TeleportServer")]
		private static extern void ClearGeometryStore();

		[DllImport("TeleportServer")]
		private static extern void StoreNode(uid id, avs.Node node);
		[DllImport("TeleportServer")]
		private static extern void StoreSkin(uid id, avs.Skin skin);
		[DllImport("TeleportServer")]
		private static extern void StoreMesh(uid id,
												[MarshalAs(UnmanagedType.BStr)] string guid,
												Int64 lastModified,
												[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(MeshMarshaler))] avs.Mesh mesh,
												avs.AxesStandard extractToStandard, [MarshalAs(UnmanagedType.I1)] bool compress, [MarshalAs(UnmanagedType.I1)] bool verify);
		[DllImport("TeleportServer")]
		private static extern void StoreMaterial(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Material material);
		[DllImport("TeleportServer")]
		private static extern void StoreTexture(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Texture texture, string compressedFilePath
			, [MarshalAs(UnmanagedType.I1)] bool genMips
			, [MarshalAs(UnmanagedType.I1)] bool highQualityUASTC
			, [MarshalAs(UnmanagedType.I1)] bool forceOverwrite
			);
		[DllImport("TeleportServer")]
		private static extern void StoreShadowMap(uid id, [MarshalAs(UnmanagedType.BStr)] string guid, Int64 lastModified, avs.Texture shadowMap);

		[DllImport("TeleportServer")]
		private static extern bool IsNodeStored(uid id);
		[DllImport("TeleportServer")]
		private static extern bool IsSkinStored(uid id);
		[DllImport("TeleportServer")]
		private static extern bool IsMeshStored(uid id);
		[DllImport("TeleportServer")]
		private static extern bool IsMaterialStored(uid id);
		[DllImport("TeleportServer")]
		private static extern bool IsTextureStored(uid id);

		[DllImport("TeleportServer")]
		private static extern void RemoveNode(uid id);

		[DllImport("TeleportServer")]
		private static extern UInt64 GetAmountOfTexturesWaitingForCompression();
		[DllImport("TeleportServer")]
		[return: MarshalAs(UnmanagedType.BStr)]
		private static extern string GetMessageForNextCompressedTexture(UInt64 textureIndex, UInt64 totalTextures);
		[DllImport("TeleportServer")]
		private static extern void CompressNextTexture();
		[DllImport("TeleportServer")]
		private static extern void SetCompressionLevels(byte compressionStrength, byte compressionQuality);
		#endregion

		#region CustomSerialisation
		//Dictionaries aren't serialised, so to serialise them I am saving the data to key value arrays. 
		public UnityEngine.Object[] processedResources_keys = new UnityEngine.Object[0];
		public uid[] processedResources_values = new uid[0];
		#endregion

		public List<TextureExtractionData> texturesWaitingForExtraction = new List<TextureExtractionData>();
		[HideInInspector] public string compressedTexturesFolderPath;

		private const string RESOURCES_PATH = "Assets/Resources/";
		private const string TELEPORT_VR_PATH = "TeleportVR/";

		private readonly Dictionary<UnityEngine.Object, uid> processedResources = new Dictionary<UnityEngine.Object, uid>(); //<Resource, ResourceID>

		private bool isAwake = false;

		[SerializeField]
		private List<AnimationClip> processedAnimations = new List<AnimationClip>(); //AnimationClips stored in the GeometrySource that we need to add an event to.
		private AnimationEvent teleportAnimationHook = new AnimationEvent(); //Animation event that is added to animation clips, so we can detect when a new animation starts playing.

		[SerializeField] private SceneReferenceManager sceneReferenceManager = null;

		private static GeometrySource geometrySource = null;

		public static GeometrySource GetGeometrySource()
		{
			if (geometrySource == null)
			{
				geometrySource = Resources.Load<GeometrySource>(TELEPORT_VR_PATH + nameof(GeometrySource));
			}

#if UNITY_EDITOR
			if (geometrySource == null)
			{
				TeleportSettings.EnsureAssetPath(RESOURCES_PATH + TELEPORT_VR_PATH);
				string assetPath = RESOURCES_PATH + TELEPORT_VR_PATH + nameof(GeometrySource) + ".asset";

				geometrySource = CreateInstance<GeometrySource>();
				AssetDatabase.CreateAsset(geometrySource, assetPath);
				AssetDatabase.SaveAssets();

				ClearGeometryStore();
				Debug.LogWarning($"Geometry Source asset created with path \"{assetPath}\"!");
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
			// Do this ASAP to prevent infinite loop.
			geometrySource=this;
			LoadFromDisk();

			CreateAnimationHook();

			isAwake = true;
		}

		public void OnEnable()
		{
			//Initialise static instance in GeometrySource when it is enabled after a hot-reload.
			GetGeometrySource();
			var teleportSettings= TeleportSettings.GetOrCreateSettings();
			compressedTexturesFolderPath = teleportSettings.cachePath + "/basis_textures/";

			//Remove nodes that have been lost due to level change.
			var pairsToDelete = processedResources.Where(pair => pair.Key == null).ToArray();
			foreach (var pair in pairsToDelete)
			{
				RemoveNode(pair.Value);
				processedResources.Remove(pair.Key);
			}

			sceneReferenceManager = SceneReferenceManager.GetSceneReferenceManager();
		}

		public void SaveToDisk()
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			SetCachePath(teleportSettings.cachePath);
			sceneReferenceManager.SaveToDisk();
			SaveGeometryStore();
		}

		public void LoadFromDisk()
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			SetCachePath(teleportSettings.cachePath);
			processedResources.Clear();

			//Load data from files.
			LoadGeometryStore(out UInt64 numMeshes, out IntPtr loadedMeshes, out UInt64 numTextures, out IntPtr loadedTextures, out UInt64 numMaterials, out IntPtr loadedMaterials);
	
			//Confirm resources loaded from disk still exists, and assign new IDs.
			AddToProcessedResources<Mesh>((int)numMeshes, loadedMeshes);
			AddToProcessedResources<Texture>((int)numTextures, loadedTextures);
			AddToProcessedResources<Material>((int)numMaterials, loadedMaterials);

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

		//Add a resource to the GeometrySource; for external extractors.
		//	resource : Resource that will be tracked by the GeometrySource.
		//	resourceID : ID of the resource on the unmanaged side.
		//Returns whether the operation succeeded; it will fail if 'resource' is null or 'resourceID' is zero.
		public bool AddResource(UnityEngine.Object resource, uid resourceID)
		{
			if(resource == null)
			{
				Debug.LogError("Failed to add the resource to the GeometrySource! Attempted to add a null resource!");
				return false;
			}

			if(resourceID == 0)
			{
				Debug.LogError($"Failed to add the resource \"{resource.name}\" to the GeometrySource! Attempted to add a resource with an ID of zero!");
				return false;
			}

			//We only want to add the animation clip to the list once.
			if(resource is AnimationClip && !processedResources.TryGetValue(resource, out uid _))
			{
				AnimationClip animation = (AnimationClip)resource;
				processedAnimations.Add(animation);

				//If this animation was extracted in play mode then we need to add the event.
				if(Application.isPlaying)
				{
					teleportAnimationHook.objectReferenceParameter = animation;
					animation.AddEvent(teleportAnimationHook);
				}
			}

			processedResources[resource] = resourceID;
			return true;
		}

		//Returns whether the GeometrySource has processed the resource.
		public bool HasResource(UnityEngine.Object resource)
		{
			return processedResources.ContainsKey(resource);
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

		//Returns the ID of the resource if it has been processed, or zero if the resource has not been processed or was passed in null.
		public uid[] FindResourceIDs(UnityEngine.Object[] resources)
		{
			if (resources==null||resources.Length==0)
			{
				return null;;
			}
			uid [] uids=new uid[resources.Length];
			for(int i=0;i<resources.Length;i++) 
			{
				processedResources.TryGetValue(resources[i], out uids[i]);
			}
			return uids;
		}

		public UnityEngine.Object FindResource(uid resourceID)
		{
			return (resourceID == 0) ? null : processedResources.FirstOrDefault(x => x.Value == resourceID).Key;
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
		public bool IsObjectStreamable(GameObject gameObject)
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (teleportSettings.TagToStream.Length > 0)
				if (!gameObject.CompareTag(teleportSettings.TagToStream))
					return false;
			if (!IsCollisionLayerStreamed(gameObject.layer))
			{
				return false;
			}
			return true;
		}
		public List<GameObject> GetStreamableObjects()
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();

			//Find all GameObjects in open scenes that have the streamed tag.
			List<GameObject> streamableObjects;
			if (teleportSettings.TagToStream.Length > 0)
			{
				GameObject[] taggedGameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.CompareTag(teleportSettings.TagToStream)).ToArray();
				streamableObjects = new List<GameObject>(taggedGameObjects);
			}
			else
			{
				streamableObjects = new List<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>());
			}

			//Remove GameObjects not on a streamed collision layer.
			for(int i = streamableObjects.Count - 1; i >= 0; i--)
			{
				GameObject gameObject = streamableObjects[i];
				if(!IsCollisionLayerStreamed(gameObject.layer))
				{
					streamableObjects.RemoveAt(i);
				}
			}

			return streamableObjects;
		}

		//Adds animations events to all extracted AnimationClips, so we can detect when they start playing.
		public void AddAnimationEventHooks()
		{
			foreach(AnimationClip clip in processedAnimations)
			{
				teleportAnimationHook.objectReferenceParameter = clip;
				clip.AddEvent(teleportAnimationHook);
			}
		}

		///GEOMETRY EXTRACTION

		public uid AddNode(GameObject gameObject, ForceExtractionMask forceMask = ForceExtractionMask.FORCE_NOTHING, bool isChildExtraction = false,bool verify=false)
		{
			if(!gameObject)
			{
				Debug.LogError("Failed to extract node from GameObject! Passed GameObject was null!");
				return 0;
			}

			//Just return the ID; if we have already processed the GameObject, the node can be found on the unmanaged side,
			//we are not forcing an extraction of nodes, and we are not forcing an extraction on the hierarchy of a node.
			if
			(
				processedResources.TryGetValue(gameObject, out uid nodeID) &&
				IsNodeStored(nodeID) &&
				(
					!isChildExtraction && (forceMask & ForceExtractionMask.FORCE_NODES) == ForceExtractionMask.FORCE_NOTHING ||
					(isChildExtraction && (forceMask & ForceExtractionMask.FORCE_HIERARCHIES) == ForceExtractionMask.FORCE_NOTHING)
				)
			)
			{
				return nodeID;
			}

			nodeID = nodeID == 0 ? GenerateID() : nodeID;
			processedResources[gameObject] = nodeID;

			avs.Node extractedNode = new avs.Node();
			StreamableProperties streamableProperties=gameObject.GetComponent<StreamableProperties>();
			if (streamableProperties != null)
			{
				extractedNode.priority = streamableProperties.priority;
			}
			else
			{
				extractedNode.priority = 0;
			}
			Teleport_Streamable teleport_Streamable = gameObject.GetComponent<Teleport_Streamable>();
			extractedNode.name = Marshal.StringToBSTR(gameObject.name);
			extractedNode.stationary = (gameObject.isStatic);
			
			extractedNode.ownerClientId = teleport_Streamable!=null? teleport_Streamable.OwnerClient:0;
			ExtractNodeHierarchy(gameObject, ref extractedNode, forceMask, verify);
			extractedNode.localTransform = avs.Transform.FromLocalUnityTransform(gameObject.transform);
			extractedNode.globalTransform=avs.Transform.FromGlobalUnityTransform(gameObject.transform);

			extractedNode.dataType = avs.NodeDataType.None;
			if(extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeMeshData(gameObject, ref extractedNode, forceMask, verify);
			}
			if (extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeSkinnedMeshData(gameObject, ref extractedNode, forceMask,verify);
			}
			if(extractedNode.dataType == avs.NodeDataType.None)
			{
				ExtractNodeLightData(gameObject, ref extractedNode, forceMask);
			}
			//Store extracted node.
			StoreNode(nodeID, extractedNode);
			return nodeID;
		}

		public uid AddMesh(Mesh mesh, ForceExtractionMask forceMask,bool verify)
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

			//Just return the ID; if we have already processed the mesh, the mesh can be found on the unmanaged side, and we are not forcing extraction.
			if(processedResources.TryGetValue(mesh, out uid meshID))
			{
				if (IsMeshStored(meshID))
				{
					if ((forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
					{
						return meshID;
					}
				}
			}


				bool running = Application.isPlaying;
			// only compress if not running - too slow...
			// Actually, let's ONLY extract offline. 
			if (!running)
			{
				meshID = meshID == 0 ? GenerateID() : meshID;
				processedResources[mesh] = meshID;
				bool enable_compression = (forceMask & ForceExtractionMask.FORCE_UNCOMPRESSED) != ForceExtractionMask.FORCE_UNCOMPRESSED;
				ExtractMeshData(avs.AxesStandard.EngineeringStyle, mesh, meshID, enable_compression && !running,verify);
				ExtractMeshData(avs.AxesStandard.GlStyle, mesh, meshID, enable_compression && !running, verify);
				return meshID;
			}
			else
			{
				if (!processedResources.TryGetValue(mesh, out meshID))
				{
					Debug.LogError("Mesh missing! Mesh " + mesh.name + " was not in processedResources.");
					return 0;
				}
				if(!IsMeshStored(meshID))
				{
					Debug.LogError("Mesh missing! Mesh "+mesh.name+" was not stored dll-side.");
					IsMeshStored(meshID);
					return 0;
				}
			}
			return 0;
		}

		public uid AddMaterial(Material material, ForceExtractionMask forceMask)
		{
			if(!material)
			{
				return 0;
			}

			//Just return the ID; if we have already processed the material, the material can be found on the unmanaged side, and we are not forcing an update.
			if(processedResources.TryGetValue(material, out uid materialID) && IsMaterialStored(materialID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
			{
				return materialID;
			}

			materialID = materialID == 0 ? GenerateID() : materialID;
			processedResources[material] = materialID;

			avs.Material extractedMaterial = new avs.Material();
			extractedMaterial.name = Marshal.StringToBSTR(material.name);

			//Albedo/Diffuse

			extractedMaterial.pbrMetallicRoughness.baseColorTexture.index = AddTexture(material.mainTexture, forceMask);
			extractedMaterial.pbrMetallicRoughness.baseColorTexture.tiling = material.mainTextureScale;
			extractedMaterial.pbrMetallicRoughness.baseColorFactor = material.HasProperty("_Color") ? material.color.linear: new Color(1.0f, 1.0f, 1.0f, 1.0f);

			//Metallic-Roughness

			Texture metallicRoughness = null;
			if(material.HasProperty("_MetallicGlossMap"))
			{
				metallicRoughness = material.GetTexture("_MetallicGlossMap");
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index = AddTexture(metallicRoughness, forceMask);
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.tiling = material.mainTextureScale;
			}
			extractedMaterial.pbrMetallicRoughness.metallicFactor = metallicRoughness ? 1.0f : (material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 1.0f); //Unity doesn't use the factor when the texture is set.

			float glossMapScale = material.HasProperty("_GlossMapScale") ? glossMapScale = material.GetFloat("_GlossMapScale") : 1.0f;
			float smoothness = metallicRoughness ? glossMapScale : (material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 1.0f);
			extractedMaterial.pbrMetallicRoughness.roughOrSmoothMultiplier = -smoothness;
			extractedMaterial.pbrMetallicRoughness.roughOffset = 1.0f;
			
			//Normal

			if(material.HasProperty("_BumpMap"))
			{
				Texture normal = material.GetTexture("_BumpMap");
				extractedMaterial.normalTexture.index = AddTexture(normal, forceMask);
				extractedMaterial.normalTexture.tiling = material.mainTextureScale;
			}
			extractedMaterial.normalTexture.strength = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1.0f;

			//Occlusion

			if(material.HasProperty("_OcclusionMap"))
			{
				Texture occlusion = material.GetTexture("_OcclusionMap");
				extractedMaterial.occlusionTexture.index = AddTexture(occlusion, forceMask);
				extractedMaterial.occlusionTexture.tiling = material.mainTextureScale;
			}
			extractedMaterial.occlusionTexture.strength = material.HasProperty("_OcclusionStrength") ? material.GetFloat("_OcclusionStrength") : 1.0f;

			//Emission

			//Extract emission properties only if emission is active.
			if(!material.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack))
			{
				if(material.HasProperty("_EmissionMap"))
				{
					Texture emission = material.GetTexture("_EmissionMap");
					if(emission!=null)
					{ 
						extractedMaterial.emissiveTexture.index = AddTexture(emission, forceMask);
						extractedMaterial.emissiveTexture.tiling = material.mainTextureScale;
					}
				}
				extractedMaterial.emissiveFactor = material.HasProperty("_BumpScale") ? (avs.Vector3)material.GetColor("_EmissionColor").linear : new avs.Vector3(0.0f, 0.0f, 0.0f);
			}

			//Extensions

			extractedMaterial.extensionAmount = 0;
			extractedMaterial.extensionIDs = null;
			extractedMaterial.extensions = null;

#if UNITY_EDITOR
			long fileId=0;
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(material, out string guid);
			Debug.Log("GUID for "+material.name+" is "+guid+", fileID is "+ fileId);
 			StoreMaterial(materialID, guid, GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid.Substring(0,32))), extractedMaterial);
#endif

			return materialID;
		}

		public string GenerateCompressedFilePath(string textureAssetPath,avs.TextureCompression textureCompression)
		{
			string compressedFilePath = textureAssetPath; //Use editor file location as unique name; this won't work out of the Unity Editor.
			compressedFilePath = compressedFilePath.Replace("/", "#"); //Replace forward slashes with hashes.
			int idx = compressedFilePath.LastIndexOf('.');
			if (idx >= 0)
				compressedFilePath = compressedFilePath.Remove(idx); //Remove file extension.
			string folderPath = compressedTexturesFolderPath;
			//Create directiory if it doesn't exist.
			if (!Directory.Exists(folderPath))
			{
				Directory.CreateDirectory(folderPath);
			}
			compressedFilePath = folderPath + compressedFilePath;
			if(textureCompression ==avs.TextureCompression.BASIS_COMPRESSED)
				compressedFilePath+=".basis"; //Combine folder path, unique name, and basis file extension to create basis file path and name.
			else if (textureCompression == avs.TextureCompression.PNG)
				compressedFilePath += ".png";
			else return "";
			return compressedFilePath;
		}
		public void AddTextureData(Texture texture, avs.Texture textureData, bool highQualityUASTC, bool forceOverwrite )
		{
			if(!processedResources.TryGetValue(texture, out uid textureID))
			{
				Debug.LogError(texture.name + " had its data extracted, but is not in the dictionary of processed resources!");
				return;
			}

#if UNITY_EDITOR
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(texture, out string guid);

			string textureAssetPath = AssetDatabase.GetAssetPath(texture);
			long lastModified = GetAssetWriteTimeUTC(textureAssetPath);

			string compressedFilePath = "";
			//Basis Universal compression won't be used if the file location is left empty.
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			compressedFilePath = GenerateCompressedFilePath(textureAssetPath, textureData.compression);
			bool genMips=false;
			textureData.mipCount=1;
			StoreTexture(textureID, guid, lastModified, textureData, compressedFilePath,  genMips, highQualityUASTC, forceOverwrite);
#endif
		}

		//Extract bone and add to processed resources; will also add parent and child bones.
		//	bone : The bone we are extracting.
		//	boneList : Array of bones; usually taken from skinned mesh renderer.
		public void CreateBone(Transform bone, ForceExtractionMask forceMask, Dictionary<Transform,uid> boneIDs, List<uid> jointIDs,List<avs.Node> avsNodes)
		{
			uid boneID=0;
			processedResources.TryGetValue(bone, out boneID);
			if (boneID!=0&&jointIDs.Contains(boneID))
				return ;
			if (boneID == 0)
            {

                boneID = boneIDs[bone];
            }
            else
            {
				if(boneID!=boneIDs[bone])
                {
					Debug.LogError("Bone id's don't match");
					return;
                }
            }
            if (boneIDs.ContainsKey(bone))
            {
                //Just return the ID; if we have already processed the transform and the bone can be found on the unmanaged side.
                if (boneID != 0 && IsNodeStored(boneID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
                {
                    return ;
                }

                //Add to processedResources first to prevent a stack overflow from recursion. 
                boneID = boneID == 0 ? GenerateID() : boneID;
                processedResources[bone] = boneID;

                // The parent of the bone might NOT be in the skeleton! Unity's avatar system can skip objects in the hierarchy.
                // So we must skip transforms to find the parent that's actually in the list of bones!
                uid parentID = 0;
				avs.Transform avsTransform = avs.Transform.FromLocalUnityTransform(bone);

				if (bone != boneIDs.First().Key)
                {
                    Transform parent = bone.parent;
                    while (parent.parent != null && parent.parent != boneIDs.First().Key && !boneIDs.ContainsKey(parent))
					{
						// We want the transform of the bone relative to its skeleton parent, not its Unity GameObject parent.
						avsTransform.rotation = parent.localRotation * avsTransform.rotation;
						avsTransform.position = parent.localRotation * avsTransform.position + parent.localPosition;
						parent = parent.parent;
					}
                    parentID = FindResourceID(parent);
                    if (parentID == 0)
                    {
                        Debug.Log("Unable to extract bone: parent not found for " + bone.name);
                        return;
                    }
				}
                // There should be only ONE bone with parent id 0.
                //parentID = parentID == 0 ? AddBone(parent,  forceMask, bones, jointIDs) : parentID;

                avs.Node boneNode = new avs.Node();
                boneNode.priority = 0;
                boneNode.name = Marshal.StringToBSTR(bone.name);
                boneNode.parentID = parentID;
                boneNode.localTransform = avsTransform;

                boneNode.dataType = avs.NodeDataType.Bone;

                boneNode.numChildren = (ulong)bone.childCount;
                boneNode.childIDs = new uid[boneNode.numChildren];
                // don't fill the children yet. We probably don't have their id's.
				avsNodes.Add(boneNode);
                //StoreNode(boneID, boneNode);
                processedResources[bone] = boneID;
                jointIDs.Add(boneID);
            }
			return;
		}
		public void BuildBoneNodeList(Transform bone, Dictionary<Transform, uid> boneIDs,List<avs.Node> avsNodes)
		{
			processedResources.TryGetValue(bone.parent,out uid parentID);
			processedResources.TryGetValue(bone, out uid boneID);
            if (boneID == 0)
            {
				boneID=GenerateID();
                processedResources[bone] = boneID;
            }
			boneIDs[bone]=boneID;

			avs.Transform avsTransform = avs.Transform.FromLocalUnityTransform(bone);
			avs.Node boneNode = new avs.Node();
			boneNode.priority = 0;
			boneNode.name = Marshal.StringToBSTR(bone.name);
			boneNode.parentID = parentID;
			boneNode.localTransform = avsTransform;

			boneNode.dataType = avs.NodeDataType.Bone;

			boneNode.numChildren = (ulong)bone.childCount;
			boneNode.childIDs = new uid[boneNode.numChildren];
			avsNodes.Add(boneNode);
			for (uint i = 0; i < boneNode.numChildren; i++)
            {
				var child= bone.GetChild((int)i);
				BuildBoneNodeList(child, boneIDs,avsNodes);
				boneNode.childIDs[i]=boneIDs[child];
			}
		}
		public void CompressTextures()
		{
			UInt64 totalTexturesToCompress = GetAmountOfTexturesWaitingForCompression();

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			SetCompressionLevels(teleportSettings.casterSettings.compressionLevel, teleportSettings.casterSettings.qualityLevel);
#if UNITY_EDITOR
			for (UInt64 i = 0; i < totalTexturesToCompress; i++)
			{
				string compressionMessage = GetMessageForNextCompressedTexture(i, totalTexturesToCompress);

				bool cancelled = EditorUtility.DisplayCancelableProgressBar("Compressing Textures", compressionMessage, (float)(i + 1) / totalTexturesToCompress);
				if(cancelled) break;

				CompressNextTexture();
			}

			EditorUtility.ClearProgressBar();
#endif
		}

		private void ExtractNodeHierarchy(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask,bool verify)
		{
			if(gameObject.transform.parent)
			{
				extractTo.parentID = FindResourceID(gameObject.transform.parent.gameObject);
			}

			//Extract children of node, through transform hierarchy.
			uid[] childIDs = new uid[gameObject.transform.childCount];
			for(int i = 0; i < gameObject.transform.childCount; i++)
			{
				GameObject child = gameObject.transform.GetChild(i).gameObject;
				uid childID = AddNode(child, forceMask, true, verify);

				childIDs[i] = childID;
			}
			extractTo.numChildren = (UInt64)gameObject.transform.childCount;
			extractTo.childIDs = childIDs.ToArray();
		}

		private void ExtractNodeMaterials(Material[] sourceMaterials, ref avs.Node extractTo, ForceExtractionMask forceMask)
		{
			List<uid> materialIDs = new List<uid>();
			foreach(Material material in sourceMaterials)
			{
				uid materialID = AddMaterial(material, forceMask);

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
			extractTo.numMaterials = (ulong)materialIDs.Count;
			extractTo.materialIDs = materialIDs.ToArray();
		}

		private bool ExtractNodeMeshData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask, bool verify)
		{
			MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
			MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
			if(!meshFilter || !meshRenderer || !meshRenderer.enabled)
			{
				return false;
			}
			extractTo.renderState.lightmapScaleOffset=meshRenderer.lightmapScaleOffset;
			Texture[] giTextures = GlobalIlluminationExtractor.GetTextures();
			if(meshRenderer.lightmapIndex>=0)
			{
				// If this index not found? These errors are not fatal, but should not occur.
				if(meshRenderer.lightmapIndex>=giTextures.Length)
				{
					Debug.LogError($"In sceneReferenceManager.GetGameObjectMesh for GameObject \"{gameObject.name}\", lightmap "+ meshRenderer.lightmapIndex + " was not listed in GlobalIlluminationExtractor!");
				}
				else
				{
					extractTo.renderState.globalIlluminationTextureUid = FindResourceID(giTextures[meshRenderer.lightmapIndex]);
					if (extractTo.renderState.globalIlluminationTextureUid == 0)
					{
						Debug.LogError($"In sceneReferenceManager.GetGameObjectMesh for GameObject \"{gameObject.name}\", lightmap " + meshRenderer.lightmapIndex + " was not found in GeometrySource!");
					}
				}
			}
			//Extract mesh used on node.
			Mesh mesh = sceneReferenceManager.GetMeshFromGameObject(gameObject);
			if (mesh == null)
			{
				Debug.LogError($"Failed sceneReferenceManager.GetGameObjectMesh for GameObject \"{gameObject.name}\"!");
				return false;
			}
			extractTo.dataID = AddMesh(mesh, forceMask, verify);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				Debug.LogError($"Failed to extract node mesh data! Failed extraction of mesh \"{(mesh ? mesh.name : "NULL")}\" from GameObject \"{gameObject.name}\"!");
				return false;
			}
			extractTo.dataType = avs.NodeDataType.Mesh;

			ExtractNodeMaterials(meshRenderer.sharedMaterials, ref extractTo, forceMask);
			
			return true;
		}

		private bool ExtractNodeSkinnedMeshData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask, bool verify)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
			if(!skinnedMeshRenderer || !skinnedMeshRenderer.enabled || !skinnedMeshRenderer.rootBone)
			{
				return false;
			}
			extractTo.renderState.lightmapScaleOffset = skinnedMeshRenderer.lightmapScaleOffset;
			var giTextures = GlobalIlluminationExtractor.GetTextures();
			if (skinnedMeshRenderer.lightmapIndex >= 0 && skinnedMeshRenderer.lightmapIndex < giTextures.Length)
				extractTo.renderState.globalIlluminationTextureUid = FindResourceID(giTextures[skinnedMeshRenderer.lightmapIndex]);
			//Extract mesh used on node.
			Mesh mesh = sceneReferenceManager.GetMeshFromGameObject(gameObject);
			extractTo.dataID = AddMesh(mesh, forceMask,verify);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				Debug.LogError($"Failed to extract skinned mesh data from GameObject: {gameObject.name}");
				return false;
			}
			extractTo.dataType = avs.NodeDataType.Mesh;

			extractTo.skinID = AddSkin(skinnedMeshRenderer, forceMask);

			//Animator component usually appears on the parent GameObject, so we need to use that instead for searching the children.
			GameObject animatorSource = (gameObject.transform.parent) ? gameObject.transform.parent.gameObject : gameObject.transform.gameObject;
			Animator animator = animatorSource.GetComponentInChildren<Animator>();
			#if UNITY_EDITOR
			if(animator)
			{
				extractTo.animationIDs = AnimationExtractor.AddAnimations(animator, forceMask);
				extractTo.numAnimations = (ulong)extractTo.animationIDs.Length;
			}
			else
			{
				extractTo.animationIDs = null;
				extractTo.numAnimations = 0;
			}
			#endif
			ExtractNodeMaterials(skinnedMeshRenderer.sharedMaterials, ref extractTo, forceMask);

			return true;
		}

		private bool ExtractNodeLightData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask)
		{
			Light light = gameObject.GetComponent<Light>();
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
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			extractTo.lightColour = (teleportSettings.colorSpace == ColorSpace.Linear) ? lightColour.linear * light.intensity : lightColour.gamma * light.intensity;
			extractTo.lightType = (byte)light.type;
			extractTo.lightRange = light.range ;
			extractTo.lightRadius = light.range / 5.0F;
			// For Unity, lights point along the X-axis, so:
			extractTo.lightDirection = new avs.Vector3(0, 0, 1.0F);

			return true;
		}

		private uid AddSkin(SkinnedMeshRenderer skinnedMeshRenderer, ForceExtractionMask forceMask)
		{
			//Just return the ID; if we have already processed the skin and the skin can be found on the unmanaged side.
			if(processedResources.TryGetValue(skinnedMeshRenderer, out uid skinID) && IsSkinStored(skinID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
			{
				return skinID;
			}

			skinID = skinID == 0 ? GenerateID() : skinID;
			processedResources[skinnedMeshRenderer] = skinID;

			avs.Skin skin = new avs.Skin();
			skin.name = Marshal.StringToBSTR(skinnedMeshRenderer.name);

			skin.inverseBindMatrices = ExtractInverseBindMatrices(skinnedMeshRenderer);
			skin.numInverseBindMatrices = skin.inverseBindMatrices.Count();

			List<uid> jointIDs = new List<uid>();
			HashSet<Transform> done=new HashSet<Transform>();
			Dictionary<Transform,uid> boneIDs=new Dictionary<Transform, uid>();
			// First, make sure every bone has a uid.
			List<avs.Node> avsNodes=new List<avs.Node>();
			// Note: we can end up here with MORE joint ID's than there are "bones" in the Skinned Mesh Renderer, because
			// Unity is allowed to skip over non-animated nodes. We must obtain the full hierarchy.
			//foreach (Transform bone in skinnedMeshRenderer.bones)
			{
				//CreateBone(bone, forceMask, boneIDs,jointIDs, avsNodes);
			}
			BuildBoneNodeList(skinnedMeshRenderer.rootBone, boneIDs,avsNodes);
			for (int i = 0; i < avsNodes.Count; i++)
			{
				avs.Node avsNode = avsNodes[i];
				uid id = boneIDs.Values.ElementAt(i);
				StoreNode(id, avsNode);
			}

			// Now, we've stored all the nodes in the object hierarchy. This may be more nodes than the actual skeleton has:

			foreach (Transform bone in skinnedMeshRenderer.bones)
			{
				uid id=boneIDs[bone];
				jointIDs.Add(id);
			}
			// TODO: uid's may be a bad way to do this because it only applies to one skeleton...
			skin.boneIDs = boneIDs.Values.ToArray();
			skin.numBones = boneIDs.Count;
			skin.jointIDs = jointIDs.ToArray();
			skin.numJoints = jointIDs.Count;

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

		private void ExtractMeshData(avs.AxesStandard extractToBasis, Mesh mesh, uid meshID,bool compress,bool verify)
		{
			avs.PrimitiveArray[] primitives = new avs.PrimitiveArray[mesh.subMeshCount];
			Dictionary<uid, avs.Accessor> accessors = new Dictionary<uid, avs.Accessor>(6);
			Dictionary<uid, avs.BufferView> bufferViews = new Dictionary<uid, avs.BufferView>(6);
			Dictionary<uid, avs.GeometryBuffer> buffers = new Dictionary<uid, avs.GeometryBuffer>(5);

			uid positionAccessorID = GenerateID();
			uid normalAccessorID = GenerateID();
			uid tangentAccessorID = GenerateID();
			uid uv0AccessorID = GenerateID();
			// Let's always generate an accessor for uv2. But if there are no uv's there we'll just point it at uv0.
			uid uv2AccessorID = GenerateID();//mesh.uv2.Length != 0 ? GenerateID() : 0;
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
			bool have_uv2 = false;

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
				if (mesh.uv2.Length != 0)
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
					have_uv2 = true;
				}
				else
				{
					// If not present, use the regular uv's for channel 2:
					accessors.Add
					(
						uv2AccessorID,
						new avs.Accessor
						{
							type = avs.Accessor.DataType.VEC2,
							componentType = avs.Accessor.ComponentType.FLOAT,
							count = (ulong)mesh.uv.Length,
							bufferView = uv0ViewID,
							byteOffset = 0
						}
					);
					have_uv2=true;
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

					BitConverter.GetBytes((int)boneWeight.boneIndex0).CopyTo(jointBuffer.data, i * jointStride + 00 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex1).CopyTo(jointBuffer.data, i * jointStride + 01 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex2).CopyTo(jointBuffer.data, i * jointStride + 02 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex3).CopyTo(jointBuffer.data, i * jointStride + 03 * 4);

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

				// Accessors
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
				primitives[i].attributeCount = (ulong)(4 + (have_uv2 ? 1 : 0) + (mesh.boneWeights.Length != 0 ? 2 : 0));
				primitives[i].attributes = new avs.Attribute[primitives[i].attributeCount];
				primitives[i].primitiveMode = avs.PrimitiveMode.TRIANGLES;

				primitives[i].attributes[0] = new avs.Attribute { accessor = positionAccessorID, semantic = avs.AttributeSemantic.POSITION };
				primitives[i].attributes[1] = new avs.Attribute { accessor = normalAccessorID, semantic = avs.AttributeSemantic.NORMAL };
				primitives[i].attributes[2] = new avs.Attribute { accessor = tangentAccessorID, semantic = avs.AttributeSemantic.TANGENT };
				primitives[i].attributes[3] = new avs.Attribute { accessor = uv0AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_0 };

				int additionalAttributes = 0;
				if(have_uv2)//mesh.uv2.Length != 0) always add uv2s.
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
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(mesh, out string guid);
			long last_modified=0;
			if (!guid.Contains("0000000000000000e000000000000000"))
			{
				last_modified = GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid.Substring(0,32)));
			}
			StoreMesh
			(
				meshID,
				guid,
				last_modified,
				new avs.Mesh
				{
					name = Marshal.StringToBSTR(mesh.name),

					numPrimitiveArrays = primitives.Length,
					primitiveArrays = primitives,

					numAccessors = accessors.Count,
					accessorIDs = accessors.Keys.ToArray(),
					accessors = accessors.Values.ToArray(),

					numBufferViews = bufferViews.Count,
					bufferViewIDs = bufferViews.Keys.ToArray(),
					bufferViews = bufferViews.Values.ToArray(),

					numBuffers = buffers.Count,
					bufferIDs = buffers.Keys.ToArray(),
					buffers = buffers.Values.ToArray()
				},
				extractToBasis
				,compress
				,verify
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

		public uid AddTexture(Texture texture, ForceExtractionMask forceMask)
		{
			if(!texture)
			{
				return 0;
			}

			//Just return the ID; if we have already processed the texture and the texture can be found on the unmanaged side.
			if(processedResources.TryGetValue(texture, out uid textureID) && IsTextureStored(textureID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
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
			catch(Exception)
			{
				Debug.LogError("Failed to get last write time for "+filePath);
				return 0;
			}
		}

		//Confirms resources loaded from disk of a certain Unity asset type still exist, and creates a pairing of their old IDs and their newly assigned IDs.
		//  numResources : Amount of resources in loadedResources.
		//  loadedResources : LoadedResource array that was created in unmanaged memory.
		//Returns list of resources that have been confirmed to exist, with their new ID assigned.
		private void AddToProcessedResources<UnityAsset>(int numResources, in IntPtr loadedResources) where UnityAsset : UnityEngine.Object
		{
			int resourceSize = Marshal.SizeOf<LoadedResource>();
			for(int i = 0; i<numResources; i++)
			{
				//Create new pointer to the memory location of the LoadedResource for this index.
				IntPtr resourcePtr = new IntPtr(loadedResources.ToInt64() + i * resourceSize);

				//Marshal data to manaaged types.
				LoadedResource metaResource = Marshal.PtrToStructure<LoadedResource>(resourcePtr);
				string name = Marshal.PtrToStringBSTR(metaResource.name);
				string guid = Marshal.PtrToStringBSTR(metaResource.guid);

#if UNITY_EDITOR
				//Asset we found in the database.
				UnityAsset asset = null;
				if (guid.Length < 32)
                {
					Debug.LogError(name+": guid too short: " + guid);
					continue;
                }
				//Attempt to find asset.
				string assetPath = AssetDatabase.GUIDToAssetPath(guid.Substring(0,32));
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
						processedResources[asset] = metaResource.id;
					}
					else
					{
						Debug.Log($"Asset {typeof(UnityAsset).FullName} \"{name}\" with GUID \"{guid}\" has been modified.");
					}
				}
				else
				{
					Debug.LogWarning($"Can't find asset {typeof(UnityAsset).FullName} \"{name}\" with GUID \"{guid}\"!");
				}
#endif
			}
		}

		private void CreateAnimationHook()
		{
			teleportAnimationHook.functionName = "SetPlayingAnimation";
			teleportAnimationHook.time = 0.0f;
			//Animation clip may be attached to an object that isn't streamed, and we don't want error messages when it hits the event and doesn't have a receiver.
			teleportAnimationHook.messageOptions = SendMessageOptions.DontRequireReceiver;
		}
	}
}

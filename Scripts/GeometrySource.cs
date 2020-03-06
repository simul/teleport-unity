//Standard Shader Inspector Code:
//https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/StandardShaderGUI.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

using uid = System.UInt64;

namespace avs
{
    public enum NodeDataType : byte
    {
        Mesh = 0,
		Camera,
		Scene,
		ShadowMap,
		Hand
    };

    public enum PrimitiveMode
    {
        POINTS, LINES, TRIANGLES, LINE_STRIP, TRIANGLE_STRIP
    };

    //! The standard glTF attribute semantics.
    public enum AttributeSemantic
    {
        //Name              Accessor Type(s)    Component Type(s)				    Description
        POSITION,           //"VEC3"            5126 (FLOAT)						XYZ vertex positions
		NORMAL,		        //"VEC3"            5126 (FLOAT)						Normalized XYZ vertex normals
		TANGENT,	        //"VEC4"            5126 (FLOAT)						XYZW vertex tangents where the w component is a sign value(-1 or +1) indicating handedness of the tangent basis
		TEXCOORD_0,	        //"VEC2"            5126 (FLOAT)
					        //	                5121 (UNSIGNED_BYTE) normalized
					        //                  5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the first set
		TEXCOORD_1,	        //"VEC2"            5126 (FLOAT)
					        //                  5121 (UNSIGNED_BYTE) normalized
					        //                  5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		COLOR_0,	        //"VEC3"
					        //"VEC4"	        5126 (FLOAT)
					        //			        5121 (UNSIGNED_BYTE) normalized
					        //			        5123 (UNSIGNED_SHORT) normalized	RGB or RGBA vertex color
		JOINTS_0,	        //"VEC4"	        5121 (UNSIGNED_BYTE)
					        //			        5123 (UNSIGNED_SHORT)				See Skinned Mesh Attributes
		WEIGHTS_0,	        //"VEC4"	        5126 (FLOAT)
					        //			        5121 (UNSIGNED_BYTE) normalized
					        //			        5123 (UNSIGNED_SHORT) normalized
		TANGENTNORMALXZ,    // VEC2             UNSIGNED_INT					    Simul: implements packed tangent-normal xz. Actually two VEC4's of BYTE.
		                    //		            SIGNED_SHORT
		COUNT               //                                                      This is the number of elements in enum class AttributeSemantic{};
                            //                                                      Must always be the last element in this enum class. 
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
        public float roughnessFactor = 1.0f;
    }

    public struct Node
    {
        public Transform transform;
        public uid dataID;
        public NodeDataType dataType;

        public UInt64 materialAmount;
        public uid[] materialIDs;

        public UInt64 childAmount;
        public uid[] childIDs;
    }

    public class Mesh
    {
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

    public struct Texture
    {
        public UInt64 nameLength;
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;

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
        public UInt64 nameLength;
        [MarshalAs(UnmanagedType.LPStr)]
        public string name;

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
        #region DLLImports
        [DllImport("SimulCasterServer")]
        private static extern uid GenerateID();
        [DllImport("SimulCasterServer")]
        private static extern void ClearGeometryStore();

        [DllImport("SimulCasterServer")]
        private static extern void StoreNode(uid id, avs.Node node);
        [DllImport("SimulCasterServer")]
        private static extern void StoreMesh(uid id, avs.AxesStandard extractToStandard, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(MeshMarshaler))]avs.Mesh mesh);
        [DllImport("SimulCasterServer")]
        private static extern void StoreMaterial(uid id, [Out]avs.Material material);
        [DllImport("SimulCasterServer")]
        private static extern void StoreTexture(uid id, avs.Texture texture, Int64 lastModified, string basisFileLocation);
        [DllImport("SimulCasterServer")]
        private static extern void StoreShadowMap(uid id, avs.Texture shadowMap);

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

        private readonly Dictionary<UnityEngine.Object, uid> processedResources = new Dictionary<UnityEngine.Object, uid>(); // <GameObject, ID of extracted data in native plug-in>

        private bool isAwake = false;

        public static GeometrySource GetGeometrySource()
        {
            string[] sourceGUIDs = UnityEditor.AssetDatabase.FindAssets("t:GeometrySource");

            string assetPath;
            if(sourceGUIDs.Length != 0)
            {
                assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(sourceGUIDs[0]);
            }
            else //Create new Geometry Source asset above scripts folder.
            {
                ClearGeometryStore();

                assetPath = UnityEditor.AssetDatabase.FindAssets(nameof(GeometrySource) + ".cs")[0];
                assetPath = assetPath.Remove(assetPath.IndexOf("Scripts/"));
                assetPath += "Geometry Source.asset";

                UnityEditor.AssetDatabase.CreateAsset(CreateInstance<GeometrySource>(), assetPath);
                Debug.LogWarning("No Geometry Source found. Created at: " + assetPath);
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<GeometrySource>(assetPath);
        }

        public void OnBeforeSerialize()
        {
            processedResources_keys = processedResources.Keys.ToArray();
            processedResources_values = processedResources.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            //Don't run during boot.
            if(isAwake)
            {
                for(int i = 0; i < processedResources_keys.Length; i++)
                {
                    processedResources[processedResources_keys[i]] = processedResources_values[i];
                }
            }
        }

        public void Awake()
        {   
            //Clear resources on boot.
            processedResources.Clear();

            isAwake = true;
        }

        public void OnEnable()
        {
            compressedTexturesFolderPath = Application.persistentDataPath + "/Basis Universal/";

            //Remove nodes that have been lost due to level change.
            var pairsToDelete = processedResources.Where(pair => pair.Key == null).ToArray();
            foreach(var pair in pairsToDelete)
            {
                RemoveNode(pair.Value);
                processedResources.Remove(pair.Key);
            }
        }

        public void ClearData()
        {
            processedResources.Clear();
            texturesWaitingForExtraction.Clear();
            ClearGeometryStore();
        }

        public uid AddNode(GameObject node, bool forceUpdate = false)
        {
            if(!node) return 0;

            processedResources.TryGetValue(node, out uid nodeID);

            if(forceUpdate || nodeID == 0)
            {
                MeshFilter meshFilter = node.GetComponent<MeshFilter>();

                if(meshFilter != null)
                {
                    //Only stream mesh nodes that have their mesh renderer enabled.
                    MeshRenderer meshRenderer = node.GetComponent<MeshRenderer>();
                    if(meshRenderer && meshRenderer.enabled)
                    {
                        nodeID = AddMeshNode(meshFilter, nodeID, forceUpdate);
                    }
                }
                else
                {
                    Debug.LogWarning(node.name + " was marked as streamable, but has no streamable component attached.");
                }
            }

            return nodeID;
        }

        public uid AddMesh(Mesh mesh)
        {
            if(!mesh) return 0;

            if(!processedResources.TryGetValue(mesh, out uid meshID))
            {
                meshID = GenerateID();
                processedResources[mesh] = meshID;

                ExtractMeshData(avs.AxesStandard.EngineeringStyle, mesh, meshID);
                ExtractMeshData(avs.AxesStandard.GlStyle, mesh, meshID);
            }

            return meshID;
        }

        public uid AddMaterial(Material material, bool forceUpdate = false)
        {
            if(!material) return 0;

            processedResources.TryGetValue(material, out uid materialID);
            if(forceUpdate || materialID == 0)
            {
                avs.Material extractedMaterial = new avs.Material();
                extractedMaterial.nameLength = (ulong)material.name.Length;
                extractedMaterial.name = material.name;

                extractedMaterial.pbrMetallicRoughness.baseColorTexture.index = AddTexture(material.mainTexture);
                extractedMaterial.pbrMetallicRoughness.baseColorTexture.tiling = material.mainTextureScale;
                extractedMaterial.pbrMetallicRoughness.baseColorFactor = material.color;

                Texture metallicRoughness = material.GetTexture("_MetallicGlossMap");
                extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index = AddTexture(metallicRoughness);
                extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.tiling = material.mainTextureScale;
                extractedMaterial.pbrMetallicRoughness.metallicFactor = metallicRoughness ? 1.0f : material.GetFloat("_Metallic"); //Unity doesn't use the factor when the texture is set.
                extractedMaterial.pbrMetallicRoughness.roughnessFactor = 1 - material.GetFloat("_GlossMapScale");

                Texture normal = material.GetTexture("_BumpMap");
                extractedMaterial.normalTexture.index = AddTexture(normal);
                extractedMaterial.normalTexture.tiling = material.mainTextureScale;
                extractedMaterial.normalTexture.strength = material.GetFloat("_BumpScale");

                Texture occlusion = material.GetTexture("_OcclusionMap");
                extractedMaterial.occlusionTexture.index = AddTexture(occlusion);
                extractedMaterial.occlusionTexture.tiling = material.mainTextureScale;
                extractedMaterial.occlusionTexture.strength = material.GetFloat("_OcclusionStrength");

                //Extract emission properties only if emission is active.
                if(!material.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack))
                {
                    Texture emission = material.GetTexture("_EmissionMap");
                    extractedMaterial.emissiveTexture.index = AddTexture(emission);
                    extractedMaterial.emissiveTexture.tiling = material.mainTextureScale;
                    extractedMaterial.emissiveFactor = material.GetColor("_EmissionColor");
                }

                if(materialID == 0) materialID = GenerateID();
                processedResources[material] = materialID;
                StoreMaterial(materialID, extractedMaterial);
            }

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

            string textureAssetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);

            string basisFileLocation = "";
            //Basis Universal compression won't be used if the file location is left empty.
            if(CasterMonitor.GetCasterMonitor().casterSettings.useCompressedTextures)
            {
                string folderPath = compressedTexturesFolderPath;
                //Create directiory if it doesn't exist.
                if(!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                basisFileLocation = textureAssetPath; //Use editor file location as unique name; this won't work out of the Unity Editor.
                basisFileLocation = basisFileLocation.Replace("/", "#"); //Replace forward slashes with hashes.
                basisFileLocation = basisFileLocation.Remove(basisFileLocation.LastIndexOf('.')); //Remove file extension.
                basisFileLocation = folderPath + basisFileLocation + ".basis"; //Combine folder path, unique name, and basis file extension to create basis file path and name.
            }

            long lastModified = File.GetLastWriteTime(textureAssetPath).ToFileTime();

            StoreTexture(textureID, textureData, lastModified, basisFileLocation);
        }

        public void CompressTextures()
        {
            UInt64 totalTexturesToCompress = GetAmountOfTexturesWaitingForCompression();

            for(UInt64 i = 0; i < totalTexturesToCompress; i++)
            {
                string compressionMessage = GetMessageForNextCompressedTexture(i, totalTexturesToCompress);

                UnityEditor.EditorUtility.DisplayProgressBar("Compressing Textures", compressionMessage, i / (float)totalTexturesToCompress);
                CompressNextTexture();
            }

            UnityEditor.EditorUtility.ClearProgressBar();
        }

        private uid AddMeshNode(MeshFilter meshFilter, uid oldID, bool forceUpdate = false)
        {
            GameObject node = meshFilter.gameObject;
            avs.Node extractedNode = new avs.Node();

            //Extract mesh used on node.
            extractedNode.dataID = AddMesh(meshFilter.sharedMesh);
            extractedNode.dataType = avs.NodeDataType.Mesh;

            //Can't create a node with no data.
            if(extractedNode.dataID == 0)
            {
                Debug.LogError("Failed to extract mesh data from game object: " + node.name);
                return 0;
            }

            //Extract materials used on node.
            List<uid> materialIDs = new List<uid>();
            foreach(Material material in node.GetComponent<MeshRenderer>().sharedMaterials)
            {
                uid materialID = AddMaterial(material, forceUpdate);

                if(materialID == 0) Debug.LogWarning("Received 0 for ID of material on game object: " + node.name);
                else materialIDs.Add(materialID);
            }
            extractedNode.materialAmount = (ulong)materialIDs.Count;
            extractedNode.materialIDs = materialIDs.ToArray();

            //Extract children of node, through transform hierarchy.
            List<uid> childIDs = new List<uid>();
            for(int i = 0; i < node.transform.childCount; i++)
            {
                uid childID = AddNode(node.transform.GetChild(i).gameObject);

                if(childID == 0) Debug.LogWarning("Received 0 for ID of child on game object: " + node.name);
                else childIDs.Add(childID);
            }
            extractedNode.childAmount = (ulong)childIDs.Count;
            extractedNode.childIDs = childIDs.ToArray();

            extractedNode.transform = new avs.Transform
            {
                position = node.transform.position,
                rotation = node.transform.rotation,
                scale = node.transform.localScale
            };

            //Store extracted node.
            uid nodeID = oldID == 0 ? GenerateID() : oldID;
            processedResources[node] = nodeID;
            StoreNode(nodeID, extractedNode);

            return nodeID;
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

            //Index Buffer
            CreateIndexBufferAndView(mesh.triangles, buffers, bufferViews, out uid indexViewID);

            for(int i = 0; i < primitives.Length; i++)
            {
                primitives[i].attributeCount = (ulong)(mesh.uv2.Length != 0 ? 5 : 4);
                primitives[i].attributes = new avs.Attribute[primitives[i].attributeCount];
                primitives[i].primitiveMode = avs.PrimitiveMode.TRIANGLES;

                primitives[i].attributes[0] = new avs.Attribute { accessor = positionAccessorID, semantic = avs.AttributeSemantic.POSITION };
                primitives[i].attributes[1] = new avs.Attribute { accessor = normalAccessorID, semantic = avs.AttributeSemantic.NORMAL };
                primitives[i].attributes[2] = new avs.Attribute { accessor = tangentAccessorID, semantic = avs.AttributeSemantic.TANGENT };
                primitives[i].attributes[3] = new avs.Attribute { accessor = uv0AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_0 };
                if(mesh.uv2.Length != 0) primitives[i].attributes[4] = new avs.Attribute { accessor = uv2AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_1 };

                primitives[i].indices_accessor = GenerateID();
                accessors.Add
                (
                    primitives[i].indices_accessor,
                    new avs.Accessor
                    {
                        type = avs.Accessor.DataType.SCALAR,
                        componentType = avs.Accessor.ComponentType.USHORT,
                        count = (ulong)mesh.triangles.Length,
                        bufferView = indexViewID,
                        byteOffset = mesh.GetIndexStart(i)
                    }
                );
            }

            StoreMesh
            (
                meshID,
                extractToBasis,
                new avs.Mesh
                {
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
                }
            );
        }

        private void CreateIndexBufferAndView(in int[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, out uid bufferViewID)
        {
            //Two bytes per unsigned short.
            //Right now the Oculus SDK expects USHORT indexes, if the object/surface is not instanced. 
            int stride = 2;

            avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
            newBuffer.byteLength = (ulong)(data.Length * stride);
            newBuffer.data = new byte[newBuffer.byteLength];

            //Get byte data from each int, and copy into buffer.
            //We are changing the indexes into counter-clockwise winding.
            for(int i = 0; i < (data.Length / 2); i++)
            {
                BitConverter.GetBytes((ushort)data[i]).CopyTo(newBuffer.data, (data.Length - 1 - i) * stride);
                BitConverter.GetBytes((ushort)data[data.Length - 1 - i]).CopyTo(newBuffer.data, i * stride);
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
                        BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 8);
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
                        BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 8);
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
                case TextureFormat.ASTC_RGB_4x4: return 8;
                case TextureFormat.ASTC_RGB_5x5: return 8;
                case TextureFormat.ASTC_RGB_6x6: return 8;
                case TextureFormat.ASTC_RGB_8x8: return 8;
                case TextureFormat.ASTC_RGB_10x10: return 8;
                case TextureFormat.ASTC_RGB_12x12: return 8;
                case TextureFormat.ASTC_RGBA_4x4: return 8;
                case TextureFormat.ASTC_RGBA_5x5: return 8;
                case TextureFormat.ASTC_RGBA_6x6: return 8;
                case TextureFormat.ASTC_RGBA_8x8: return 8;
                case TextureFormat.ASTC_RGBA_10x10: return 8;
                case TextureFormat.ASTC_RGBA_12x12: return 8;
                default:
                    Debug.LogWarning("Defaulting to <color=red>8</color> bytes per pixel as format is unsupported: " + format);
                    return 8;
            }
        }

        private uid AddTexture(Texture texture)
        {
            if(!texture) return 0;

            if(!processedResources.TryGetValue(texture, out uid textureID))
            {
                if(Application.isPlaying)
                {
                    Debug.LogWarning("Texture <b>" + texture.name + "</b> has not been extracted, but is being used on streamed geometry!");
                    return 0;
                }

                avs.Texture extractedTexture = new avs.Texture()
                {
                    nameLength = (ulong)texture.name.Length,
                    name = texture.name,

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
                
                textureID = GenerateID();
                processedResources[texture] = textureID;
                texturesWaitingForExtraction.Add(new TextureExtractionData{id = textureID, unityTexture = texture, textureData = extractedTexture});
            }

            return textureID;
        }
    }
}
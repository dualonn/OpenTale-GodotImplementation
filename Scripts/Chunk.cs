using Godot;
using System;
using System.Collections.Generic;
using VoxelEngine.Scripts;
using Godot.Collections;
using Array = Godot.Collections.Array;

public partial class Chunk : Node3D
{
    public const int ChunkSizeHorizontal = 16;
    public const int ChunkSizeVertical = 512;
    public byte[,,] blocks;
    public Voxel[,,] voxels = new Voxel[ChunkSizeHorizontal, ChunkSizeVertical, ChunkSizeHorizontal];
    private MeshInstance3D meshInstance;
    private StaticBody3D collider;
    private CollisionShape3D colliderShape;

    public int worldX = 0;
    public int worldZ = 0;
    
    private RandomNumberGenerator rng = new();
    
    public const int maxGroundHeight = 200;
    public const int lowestCaveDepth = 10;
    
    public NoiseManager noiseManager;

    public override void _Ready()
    {
        noiseManager = NoiseManager.Instance;
        blocks = new byte[ChunkSizeHorizontal, ChunkSizeVertical, ChunkSizeHorizontal];
        
        meshInstance = new MeshInstance3D();
        collider = new StaticBody3D();
        colliderShape = new CollisionShape3D();
        collider.AddChild(colliderShape);
        collider.AddChild(meshInstance);

        GenerateBlocks();
        BuildMesh();
    }

    public struct BiomeWeight
    {
        public float Grasslands;
        public float Forest;
        public float Mountains;
        public float Ocean;
    }

    //gets block type of block x, y, z
    public byte GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSizeHorizontal || y < 0 || y >= ChunkSizeVertical || z < 0 ||
            z >= ChunkSizeHorizontal) return 0;
        
        return blocks[x, y, z];
    }

    //sets block type of block x, y, z to ID X (used outside this class)
    public void SetBlock(int x, int y, int z, byte block)
    {
        if (x < 0 || x >= ChunkSizeHorizontal || y < 0 || y >= ChunkSizeVertical || z < 0 ||
            z >= ChunkSizeHorizontal) return;

        blocks[x, y, z] = block;
        VoxelType type = VoxelType.Air; //Always ID 0
        switch (block)
        {
            case 1:
                type = VoxelType.Dirt;
                break;
            case 2:
                type = VoxelType.Grass;
                break;
            case 3:
                type = VoxelType.Stone;
                break;
            case 4:
                type = VoxelType.DepthRock;
                break;
            case 5:
                type = VoxelType.Sand;
                break;
            case 6:
                type = VoxelType.OakLog;
                break;
            case 7:
                type = VoxelType.OakPlanks;
                break;
            case 8:
                type = VoxelType.Glass;
                break;
        }
        
        voxels[x, y, z] = new Voxel { type = type };
        BuildMesh();
    }

    //computes weights for biomes
    private BiomeWeight ComputeBiomeWeights(float biomeValue)
    {
        BiomeWeight w;
        w.Ocean = Mathf.Clamp(1f - Mathf.Abs(biomeValue - (-0.8f)) * 2f, 0f, 1f);
        w.Forest = Mathf.Clamp(1f - Mathf.Abs(biomeValue - (-0.2f)) * 2f, 0f, 1f);
        w.Grasslands = Mathf.Clamp(1f - Mathf.Abs(biomeValue - (0.3f)) * 2f, 0f, 1f);
        w.Mountains = Mathf.Clamp(1f - Mathf.Abs(biomeValue - (0.8f)) * 2f, 0f, 1f);

        float sum = w.Ocean + w.Forest + w.Grasslands + w.Mountains;
        if (sum > 0f)
        {
            w.Ocean /= sum;
            w.Forest /= sum;
            w.Grasslands /= sum;
            w.Mountains /= sum;
        }

        return w;
    }
    
    //sets appropriate terrain height value based on biomeNoise and biome weights
    private int GetTerrainHeight(int worldX, int worldZ)
    {
        float biomeValue = noiseManager.BiomeNoise.GetNoise2D(worldX, worldZ);
        float hills = noiseManager.HillNoise.GetNoise2D(worldX, worldZ) * 12f;
        
        BiomeWeight weights = ComputeBiomeWeights(biomeValue);
        float baseHeight = 60f;
        float height = baseHeight + hills * weights.Grasslands + hills * weights.Forest + weights.Mountains + weights.Ocean;
        
        return Mathf.Clamp((int)height, lowestCaveDepth, maxGroundHeight);
        //return (int)(noiseManager.HillNoise.GetNoise2D(worldX, worldZ) * 12f);
    }

    //blocks initializer
    private void GenerateBlocks()
    {
        for (int x = 0; x < ChunkSizeHorizontal; x++)
        {
            for (int z = 0; z < ChunkSizeHorizontal; z++)
            {
                int worldX = this.worldX * ChunkSizeHorizontal + x;
                int worldZ = this.worldZ * ChunkSizeHorizontal + z;
                
                int terrainHeight = GetTerrainHeight(worldX, worldZ);

                for (int y = 0; y < ChunkSizeVertical; y++)
                {
                    VoxelType type;

                    if (y > terrainHeight) type = VoxelType.Air;
                    else if (y == terrainHeight) type = VoxelType.Grass;
                    else if (y >= terrainHeight - 4) type = VoxelType.Dirt;
                    else if (y == 0) type = VoxelType.DepthRock;
                    else type = VoxelType.Stone;
                    
                    voxels[x, y, z] = new Voxel { type = type };
                    blocks[x, y, z] = (byte)type;
                }
            }
        }
    }

    //function that builds meshes from voxel data
    private void BuildMesh()
    {
        var arrays = new Array();
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        int index = 0;
        
        var mat = new StandardMaterial3D();

        for (int x = 0; x < ChunkSizeHorizontal; x++)
        {
            for (int y = 0; y < ChunkSizeVertical; y++)
            {
                for (int z = 0; z < ChunkSizeHorizontal; z++)
                {
                    if (!voxels[x, y, z].IsSolid) continue;

                    Vector3 position = new Vector3(x, y, z);
                    
                    //add faces only if neighbors are solid or border block
                    if (y == ChunkSizeVertical - 1 || !voxels[x, y + 1, z].IsSolid || (blocks[x, y + 1, z] == 8 && blocks[x, y, z] != 8)) 
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Up, voxels[x, y, z].type, ref index);
                    if (y == 0 || !voxels[x, y - 1, z].IsSolid || (blocks[x, y - 1, z] == 8 && blocks[x, y, z] != 8))
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Down, voxels[x, y, z].type, ref index);
                    
                    if (x == 0 || !voxels[x - 1, y, z].IsSolid || (blocks[x - 1, y, z] == 8 && blocks[x, y, z] != 8))
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Left, voxels[x, y, z].type, ref index);
                    if (x == ChunkSizeHorizontal - 1 || !voxels[x + 1, y, z].IsSolid || (blocks[x + 1, y, z] == 8 && blocks[x, y, z] != 8))
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Right, voxels[x, y, z].type, ref index);
                    
                    if (z == 0 || !voxels[x, y, z - 1].IsSolid || (blocks[x, y, z - 1] == 8 && blocks[x, y, z] != 8))
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Back, voxels[x, y, z].type, ref index);
                    if (z == ChunkSizeHorizontal - 1 || !voxels[x, y, z + 1].IsSolid || (blocks[x, y, z + 1] == 8 && blocks[x, y, z] != 8))
                        AddFace(vertices, indices, normals, uvs, position, Vector3.Forward, voxels[x, y, z].type, ref index);
                    
                    if(GetBlock(x, y, z) == 8)
                        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                }
            }
        }
        
        //adds rendering data, vertexes, indices, normals, uvs and sets the mesh data
        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[(int)ArrayMesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Index] = indices.ToArray();
        arrays[4] = uvs.ToArray();
                    
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Triangles, arrays);
        meshInstance.Mesh = mesh;
        
        mat.AlbedoTexture = Godot.ResourceLoader.Load<Texture2D>("res://Textures/TextureAtlas.png");
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        meshInstance.SetSurfaceOverrideMaterial(0, mat);

        if (indices.Count > 0)
        {
            var collision = new ConcavePolygonShape3D();
            collision.Data = mesh.GetFaces();
            colliderShape.Shape = collision;
        }
        else
        {
            colliderShape.Shape = null;
        }
        AddChild(collider);
    }

    //gets textures for each face per voxel type
    private Vector2[] GetFaceTextures(VoxelType type, Vector3 dir)
    {
        int atlasCols = 11;
        float texSize = 1f / atlasCols;

        int tileIndex = 0;

        switch (type)
        {
            case VoxelType.Grass:
                tileIndex = (dir == Vector3.Up) ? 1 : (dir == Vector3.Down) ? 0 : 3;
                break;
            case VoxelType.Dirt:
                tileIndex = 0;
                break;
            case VoxelType.Stone:
                tileIndex = 4;
                break;
            case VoxelType.DepthRock:
                tileIndex = 5;
                break;
            case VoxelType.Sand:
                tileIndex = 6;
                break;
            case VoxelType.OakLog:
                tileIndex = (dir == Vector3.Up || dir == Vector3.Down) ? 8 : 7;
                break;
            case VoxelType.OakPlanks:
                tileIndex = 9;
                break;
            case VoxelType.Glass:
                tileIndex = 10;
                break;
        }

        float uMin = tileIndex * texSize;
        float uMax = uMin + texSize;

        return new Vector2[]
        {
            new Vector2(uMin, 1),
            new Vector2(uMin, 0),
            new Vector2(uMax, 0),
            new Vector2(uMax, 1)
        };
    }

    //adds faces
    private void AddFace(List<Vector3> vertexes, List<int> indices, List<Vector3> normals, List<Vector2> uvs, Vector3 pos, Vector3 dir,
        VoxelType type, ref int index)
    {
        Vector3[] faceVertices = new Vector3[4];

        if (dir == Vector3.Up)
        {
            faceVertices[0] = pos + new Vector3(-0.5f, 0.5f, -0.5f);
            faceVertices[1] = pos + new Vector3(0.5f, 0.5f, -0.5f);
            faceVertices[2] = pos + new Vector3(0.5f, 0.5f, 0.5f);
            faceVertices[3] = pos + new Vector3(-0.5f, 0.5f, 0.5f);
        }
        else if (dir == Vector3.Down)
        {
            faceVertices[0] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
            faceVertices[1] = pos + new Vector3(-0.5f, -0.5f, 0.5f);
            faceVertices[2] = pos + new Vector3(0.5f, -0.5f, 0.5f);
            faceVertices[3] = pos + new Vector3(0.5f, -0.5f, -0.5f);
        }
        else if (dir == Vector3.Left)
        {
            faceVertices[0] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
            faceVertices[1] = pos + new Vector3(-0.5f, 0.5f, -0.5f);
            faceVertices[2] = pos + new Vector3(-0.5f, 0.5f, 0.5f);
            faceVertices[3] = pos + new Vector3(-0.5f, -0.5f, 0.5f);
        }
        else if (dir == Vector3.Right)
        {
            faceVertices[0] = pos + new Vector3(0.5f, -0.5f, 0.5f);
            faceVertices[1] = pos + new Vector3(0.5f, 0.5f, 0.5f);
            faceVertices[2] = pos + new Vector3(0.5f, 0.5f, -0.5f);
            faceVertices[3] = pos + new Vector3(0.5f, -0.5f, -0.5f);
        }
        else if (dir == Vector3.Forward)
        {
            faceVertices[0] = pos + new Vector3(-0.5f, -0.5f, 0.5f);
            faceVertices[1] = pos + new Vector3(-0.5f, 0.5f, 0.5f);
            faceVertices[2] = pos + new Vector3(0.5f, 0.5f, 0.5f);
            faceVertices[3] = pos + new Vector3(0.5f, -0.5f, 0.5f);
        }
        else if (dir == Vector3.Back)
        {
            faceVertices[0] = pos + new Vector3(0.5f, -0.5f, -0.5f);
            faceVertices[1] = pos + new Vector3(0.5f, 0.5f, -0.5f);
            faceVertices[2] = pos + new Vector3(-0.5f, 0.5f, -0.5f);
            faceVertices[3] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
        }
        
        vertexes.AddRange(faceVertices);
        indices.Add(index + 0); indices.Add(index + 1); indices.Add(index + 2);
        indices.Add(index + 2); indices.Add(index + 3); indices.Add(index + 0);
        
        var faceUVs = GetFaceTextures(type, dir);
        uvs.AddRange(faceUVs);

        for (int i = 0; i < 4; i++) normals.Add(dir);
        
        index += 4;
    }
}
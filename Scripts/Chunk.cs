using Godot;
using System;
using System.Collections.Generic;

public partial class Chunk : Node3D
{
    public const int ChunkSize_Horizontal = 16;
    public const int ChunkSize_Vertical = 512;

    public byte[,,] blocks;
    // x, y, z -> horizontal, vertical, horizontal
    public Voxel[,,] voxels = new Voxel[ChunkSize_Horizontal, ChunkSize_Vertical, ChunkSize_Horizontal];
    private MeshInstance3D meshInstance;
    private StaticBody3D collider;
    private CollisionShape3D colShape;

    private FastNoiseLite noise;
    private FastNoiseLite biomeNoise;
    private FastNoiseLite mountainNoise;
    private FastNoiseLite hillNoise;
    private FastNoiseLite detailNoise;
    private FastNoiseLite caveNoise;          // standard tunnels
    private FastNoiseLite caveBranchNoise;    // branching
    private FastNoiseLite undergroundRoomNoise; // underground large rooms
    private FastNoiseLite surfaceOpeningNoise;
    public int world_x = 0;
    public int world_z = 0;
    public RandomNumberGenerator rng = new RandomNumberGenerator();
    public const int MaxWorldHeight = 200;
    public const int MinCaveDepth = 10; // number of blocks above bottom that caves can reach

    public override void _Ready()
    {
        blocks = new byte[ChunkSize_Horizontal, ChunkSize_Vertical, ChunkSize_Horizontal];
        noise = new FastNoiseLite();          // legacy (optional)
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;

        biomeNoise = new FastNoiseLite();
        biomeNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        biomeNoise.SetFrequency(0.0005f); // huge continents
        biomeNoise.SetSeed(12345);

        mountainNoise = new FastNoiseLite();
        mountainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        mountainNoise.SetFrequency(0.0015f);
        mountainNoise.SetFractalType(FastNoiseLite.FractalTypeEnum.Fbm);
        mountainNoise.SetFractalOctaves(4);
        mountainNoise.SetSeed(54321);

        hillNoise = new FastNoiseLite();
        hillNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        hillNoise.SetFrequency(0.005f);
        hillNoise.SetFractalType(FastNoiseLite.FractalTypeEnum.Fbm);
        hillNoise.SetFractalLacunarity(1.2f);
        hillNoise.SetFractalOctaves(3);
        hillNoise.SetFractalGain(0.25f);
        hillNoise.SetSeed(7777);

        detailNoise = new FastNoiseLite();
        detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        detailNoise.SetFrequency(0.005f);
        detailNoise.SetFractalType(FastNoiseLite.FractalTypeEnum.Fbm);
        detailNoise.SetFractalOctaves(2);
        detailNoise.SetSeed(99999);
        
        caveNoise = new FastNoiseLite();
        caveNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        caveNoise.SetFrequency(0.02f);
        caveNoise.SetSeed(11111);

        caveBranchNoise = new FastNoiseLite();
        caveBranchNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        caveBranchNoise.SetFrequency(0.05f);
        caveBranchNoise.SetSeed(22222);

        undergroundRoomNoise = new FastNoiseLite();
        undergroundRoomNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        undergroundRoomNoise.SetFrequency(0.01f);
        undergroundRoomNoise.SetSeed(33333);

        surfaceOpeningNoise = new FastNoiseLite();
        surfaceOpeningNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        surfaceOpeningNoise.SetFrequency(0.005f);
        surfaceOpeningNoise.SetSeed(44444);

        
        meshInstance = new MeshInstance3D();
        collider = new StaticBody3D();
        colShape = new CollisionShape3D();
        collider.AddChild(colShape);
        AddChild(collider);
        AddChild(meshInstance);
        GenerateVoxels();
        BuildMesh();
    }

    public byte GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize_Horizontal || y < 0 || y >= ChunkSize_Vertical || z < 0 ||
            z >= ChunkSize_Horizontal) return 0;
        
        return blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, byte block)
    {
        if (x < 0 || x >= ChunkSize_Horizontal || y < 0 || y >= ChunkSize_Vertical || z < 0 ||
            z >= ChunkSize_Horizontal) return;

        blocks[x, y, z] = block;
        VoxelType type = VoxelType.Air;
        if (block == 1) type = VoxelType.Dirt;
        if (block == 2) type = VoxelType.Grass;
        if (block == 3) type = VoxelType.Stone;
        if (block == 4) type = VoxelType.DepthRock;

        voxels[x, y, z] = new Voxel { type = type };
        
        BuildMesh();
    }
    
    private int GetTerrainHeight(int worldX, int worldZ)
    {
        // ------------ Biome selector (-1 to 1) --------------
        float biomeVal = biomeNoise.GetNoise2D(worldX, worldZ);

        // Only 1 biome for now, but this is expandable:
        float grassBiomeWeight = 1.0f; // always grasslands for now

        // ------------ Hills --------------
        float hills = hillNoise.GetNoise2D(worldX, worldZ) * 12f;

        // ------------ Mountains --------------
        float mountainMask = Mathf.Clamp((biomeVal + 0.2f) * 1.2f, 0, 1);  
        float mountains = Mathf.Max(0, mountainNoise.GetNoise2D(worldX, worldZ)) * 45f * mountainMask;

        // ------------ Detail noise --------------
        float detail = detailNoise.GetNoise2D(worldX, worldZ) * 3f;

        // ------------ Base height --------------
        float baseHeight = 60f;

        // Combine all terrain contributions
        float height =
            baseHeight +
            hills * grassBiomeWeight +
            mountains * grassBiomeWeight +
            detail;

        return Mathf.Clamp((int)height, MinCaveDepth, MaxWorldHeight);
    }

    private void GenerateVoxels()
{

    for (int x = 0; x < ChunkSize_Horizontal; x++)
    {
        for (int z = 0; z < ChunkSize_Horizontal; z++)
        {
            int worldX = world_x * ChunkSize_Horizontal + x;
            int worldZ = world_z * ChunkSize_Horizontal + z;

            int terrainHeight = GetTerrainHeight(worldX, worldZ);

            for (int y = 0; y < ChunkSize_Vertical; y++)
            {
                VoxelType type;

                // --- Base terrain ---
                if (y > terrainHeight)
                {
                    type = VoxelType.Air;
                }
                else if (y == terrainHeight)
                {
                    type = VoxelType.Grass;
                }
                else if (y >= terrainHeight - 4)
                {
                    type = VoxelType.Dirt;
                }
                else
                {
                    type = VoxelType.Stone;
                }

                // --- Cave system ---
                if (y >= MinCaveDepth)
                {
                    float caveVal = caveNoise.GetNoise3D(worldX, y, worldZ);
                    float branchVal = caveBranchNoise.GetNoise3D(worldX, y, worldZ) * 0.5f;
                    caveVal += branchVal;

                    if (caveVal > 0.45f) type = VoxelType.Air;

                    float roomVal = undergroundRoomNoise.GetNoise3D(worldX, y, worldZ);
                    if (roomVal > 0.6f && y < terrainHeight - 5) type = VoxelType.Air;

                    if (y == terrainHeight && surfaceOpeningNoise.GetNoise2D(worldX, worldZ) > 0.85f)
                        type = VoxelType.Air;
                }

                // --- Bottom limit ---
                if (y == 0) type = VoxelType.DepthRock;

                voxels[x, y, z] = new Voxel { type = type };
                blocks[x, y, z] = (byte)type;
            }
        }
    }
}

    private void BuildMesh()
    {
        var arrays = new Godot.Collections.Array();
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();

        int index = 0;

        for (int x = 0; x < ChunkSize_Horizontal; x++)
        {
            for (int y = 0; y < ChunkSize_Vertical; y++)
            {
                for (int z = 0; z < ChunkSize_Horizontal; z++)
                {
                    if (!voxels[x, y, z].IsSolid) continue;

                    Vector3 pos = new Vector3(x, y, z);

                    // Up/Down use vertical limit
                    if (y == ChunkSize_Vertical - 1 || !voxels[x, y + 1, z].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Up, voxels[x, y, z].type, ref index);
                    if (y == 0 || !voxels[x, y - 1, z].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Down, voxels[x, y, z].type, ref index);

                    // Left/Right use horizontal limit
                    if (x == 0 || !voxels[x - 1, y, z].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Left, voxels[x, y, z].type, ref index);
                    if (x == ChunkSize_Horizontal - 1 || !voxels[x + 1, y, z].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Right, voxels[x, y, z].type, ref index);

                    // Forward/Back use horizontal limit for z
                    if (z == 0 || !voxels[x, y, z - 1].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Back, voxels[x, y, z].type, ref index);
                    if (z == ChunkSize_Horizontal - 1 || !voxels[x, y, z + 1].IsSolid)
                        AddFace(vertices, indices, uvs, normals, pos, Vector3.Forward, voxels[x, y, z].type, ref index);
                }
            }
        }

        arrays.Resize((int)ArrayMesh.ArrayType.Max);
        arrays[0] = vertices.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Normal] = normals.ToArray();
        arrays[4] = uvs.ToArray();
        arrays[(int)ArrayMesh.ArrayType.Index] = indices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(ArrayMesh.PrimitiveType.Triangles, arrays);
        meshInstance.Mesh = mesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = GD.Load<Texture2D>("res://textures/TextureAtlas.png");
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        meshInstance.MaterialOverride = mat;

        var collision = new ConcavePolygonShape3D();
        collision.Data = mesh.GetFaces();
        colShape.Shape = collision;
    }

    private Vector2[] GetFaceUVs(VoxelType type, Vector3 dir)
    {
        // Number of textures in the atlas horizontally
        int atlas_columns = 6;
        float tex_size = 1f / atlas_columns; // width per tile

        // default: dirt
        int tile_index = 0;

        if (type == VoxelType.Grass)
        {
            if (dir == Vector3.Up)
                tile_index = 1; // Grass top
            else if (dir == Vector3.Down)
                tile_index = 0; // Dirt bottom
            else
                tile_index = 3; // Grass sides
        }
        else if (type == VoxelType.Dirt)
        {
            tile_index = 0;
        }
        else if (type == VoxelType.Stone)
        {
            tile_index = 4;
        }
        else if (type == VoxelType.DepthRock)
        {
            tile_index = 5;
        }

        float u_min = tile_index * tex_size;
        float u_max = u_min + tex_size;

        // Return UVs in the order that matches the vertices in AddFace.
        // You had been using this order previously (rotated), so leave it as-is.
        return new Vector2[]
        {
            new Vector2(u_min, 1),
            new Vector2(u_min, 0),
            new Vector2(u_max, 0),
            new Vector2(u_max, 1)
        };
    }

    private void AddFace(
        List<Vector3> verts,
        List<int> inds,
        List<Vector2> uvs,
        List<Vector3> normals,
        Vector3 pos,
        Vector3 dir,
        VoxelType type,
        ref int index)
    {
        Vector3[] faceVerts = new Vector3[4];

        // ---- VERTICES ----
        if (dir == Vector3.Up)
        {
            faceVerts[0] = pos + new Vector3(-0.5f, 0.5f, -0.5f);
            faceVerts[1] = pos + new Vector3( 0.5f, 0.5f, -0.5f);
            faceVerts[2] = pos + new Vector3( 0.5f, 0.5f,  0.5f);
            faceVerts[3] = pos + new Vector3(-0.5f, 0.5f,  0.5f);
        }
        else if (dir == Vector3.Down)
        {
            faceVerts[0] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
            faceVerts[1] = pos + new Vector3(-0.5f, -0.5f,  0.5f);
            faceVerts[2] = pos + new Vector3( 0.5f, -0.5f,  0.5f);
            faceVerts[3] = pos + new Vector3( 0.5f, -0.5f, -0.5f);
        }
        else if (dir == Vector3.Left)
        {
            faceVerts[0] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
            faceVerts[1] = pos + new Vector3(-0.5f,  0.5f, -0.5f);
            faceVerts[2] = pos + new Vector3(-0.5f,  0.5f,  0.5f);
            faceVerts[3] = pos + new Vector3(-0.5f, -0.5f,  0.5f);
        }
        else if (dir == Vector3.Right)
        {
            faceVerts[0] = pos + new Vector3(0.5f, -0.5f,  0.5f);
            faceVerts[1] = pos + new Vector3(0.5f,  0.5f,  0.5f);
            faceVerts[2] = pos + new Vector3(0.5f,  0.5f, -0.5f);
            faceVerts[3] = pos + new Vector3(0.5f, -0.5f, -0.5f);
        }
        else if (dir == Vector3.Forward)
        {
            faceVerts[0] = pos + new Vector3(-0.5f, -0.5f, 0.5f);
            faceVerts[1] = pos + new Vector3(-0.5f,  0.5f, 0.5f);
            faceVerts[2] = pos + new Vector3( 0.5f,  0.5f, 0.5f);
            faceVerts[3] = pos + new Vector3( 0.5f, -0.5f, 0.5f);
        }
        else // Back (-Z)
        {
            faceVerts[0] = pos + new Vector3( 0.5f, -0.5f, -0.5f);
            faceVerts[1] = pos + new Vector3( 0.5f,  0.5f, -0.5f);
            faceVerts[2] = pos + new Vector3(-0.5f,  0.5f, -0.5f);
            faceVerts[3] = pos + new Vector3(-0.5f, -0.5f, -0.5f);
        }

        // ---- PUSH VERTICES ----
        verts.AddRange(faceVerts);

        // ---- TRIANGLES ----
        inds.Add(index + 0); inds.Add(index + 1); inds.Add(index + 2);
        inds.Add(index + 2); inds.Add(index + 3); inds.Add(index + 0);

        // ---- UVS ----
        var faceUVs = GetFaceUVs(type, dir);
        uvs.AddRange(faceUVs);

        // ---- NORMALS ----
        for (int i = 0; i < 4; i++)
            normals.Add(dir);

        index += 4;
    }
}

using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class World : Node3D
{
	public const int RenderDistance = 8;
	public PackedScene chunk_scene;

	private System.Collections.Generic.Dictionary<Vector2I, Chunk> chunks = new();
	private Queue<Vector2I> loadQueue = new();
	public Player player;

	public override void _Ready()
	{
		chunk_scene = GD.Load<PackedScene>("res://Chunk.tscn");

		player = GetNode<Player>("../Player");

		if (player == null)
			GD.Print("No player found!");

		Callable.From(AsyncChunkLoader).CallDeferred();
	}

	public override void _Process(double delta)
	{
		UpdateChunkQueue();
	}

	private void UnloadFarChunks(int px, int pz)
	{
		var toRemove = new System.Collections.Generic.List<Vector2I>();

		foreach (var kv in chunks)
		{
			var key = kv.Key;
			int dx = key.X - px;
			int dz = key.Y - pz;

			if (Math.Abs(dx) > RenderDistance || Math.Abs(dz) > RenderDistance) toRemove.Add(key);
		}

		foreach (var key in toRemove)
		{
			chunks[key].QueueFree();
			chunks.Remove(key);
		}
	}

	private void UpdateChunkQueue()
	{
		if (player == null) return;

		int playerChunkX = Mathf.FloorToInt(player.GlobalPosition.X / Chunk.ChunkSize_Horizontal);
		int playerChunkZ = Mathf.FloorToInt(player.GlobalPosition.Z / Chunk.ChunkSize_Horizontal);
		
		for (int x = -RenderDistance; x <= RenderDistance; x++)
		{
			for (int z = -RenderDistance; z <= RenderDistance; z++)
			{
				Vector2I key = new(playerChunkX + x, playerChunkZ + z);
				if(!chunks.ContainsKey(key) && !loadQueue.Contains(key)) loadQueue.Enqueue(key);
			}
		}
		UnloadFarChunks(playerChunkX, playerChunkZ);
	}

	private async void AsyncChunkLoader()
	{
		while (true)
		{
			if (loadQueue.Count > 0)
			{
				Vector2I key = loadQueue.Dequeue();

				await LoadChunkAsync(key.X, key.Y);

				//await Task.Delay(1);
			}

			await Task.Delay(1);
		}
	}

	private async Task LoadChunkAsync(int cx, int cz)
	{
		Vector2I key = new(cx, cz);

		if (chunks.ContainsKey(key)) return;

		Chunk chunk = chunk_scene.Instantiate<Chunk>();

		chunk.world_x = cx;
		chunk.world_z = cz;

		chunk.Position = new Vector3(
			cx * Chunk.ChunkSize_Horizontal,
			0,
			cz * Chunk.ChunkSize_Horizontal
		);
		
		AddChild(chunk);
		chunks[key] = chunk;

		await Task.Delay(1);
	}

	public bool WorldToBlockCoords(
		Vector3 worldPos,
		out Vector2I chunkCoord,
		out Vector3I blockPos
	) {
		// Convert world coords → integer block coords
		int bx = Mathf.RoundToInt(worldPos.X);
		int by = Mathf.RoundToInt(worldPos.Y);
		int bz = Mathf.RoundToInt(worldPos.Z);

		// Which chunk?
		int cx = Mathf.FloorToInt((float)bx / Chunk.ChunkSize_Horizontal);
		int cz = Mathf.FloorToInt((float)bz / Chunk.ChunkSize_Horizontal);

		chunkCoord = new Vector2I(cx, cz);

		// Chunk missing = fail
		if (!chunks.TryGetValue(chunkCoord, out Chunk chunk))
		{
			blockPos = default;
			return false;
		}

		// Local block coords inside chunk
		int localX = bx - cx * Chunk.ChunkSize_Horizontal;
		int localZ = bz - cz * Chunk.ChunkSize_Horizontal;

		// Bounds check (ALL out-of-range returns must set blockPos)
		if (localX < 0 || localX >= Chunk.ChunkSize_Horizontal ||
		    by < 0      || by >= Chunk.ChunkSize_Vertical   ||
		    localZ < 0 || localZ >= Chunk.ChunkSize_Horizontal)
		{
			blockPos = default;
			return false;
		}

		// VALID → assign and return true
		blockPos = new Vector3I(localX, by, localZ);
		return true;
	}

	public void BreakBlock(Vector3 worldPos)
	{
		if(!WorldToBlockCoords(worldPos, out var chunkKey, out var blockPos)) return;
		chunks[chunkKey].SetBlock(blockPos.X, blockPos.Y, blockPos.Z, 0);
	}

	public void PlaceBlock(Vector3 worldPos, byte block)
	{
		if(!WorldToBlockCoords(worldPos, out var chunkKey, out var blockPos)) return;
		chunks[chunkKey].SetBlock(blockPos.X, blockPos.Y, blockPos.Z, block);
	}
}

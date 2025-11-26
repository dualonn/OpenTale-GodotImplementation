using Godot;
using System;

public enum VoxelType
{
	Air,
	Dirt,
	Grass,
	Stone,
	DepthRock,
}

public struct Voxel
{
	public VoxelType type;
	public bool IsSolid => type != VoxelType.Air;
}

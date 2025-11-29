using Godot;
using System;

public enum VoxelType
{
	Air,
	Dirt,
	Grass,
	Stone,
	DepthRock,
	Sand,
	OakLog,
	OakPlanks,
	Glass,
}

public struct Voxel
{
	public VoxelType type;
	public bool IsSolid => type != VoxelType.Air;
}

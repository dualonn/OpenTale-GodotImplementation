using System.Collections.Generic;
using Godot.Collections;
using Godot;

namespace VoxelEngine.Scripts;

[GlobalClass]
public partial class Item: Resource
{
	[Export] public Texture2D Icon;
	[Export] public string Name;
	[Export] public bool Stackable = true;
	[Export] public int MaxStack = 256;
	[Export] public bool IsBlock = true;
	[Export] public VoxelType BlockType;
	[Export] public int InternalID;
}

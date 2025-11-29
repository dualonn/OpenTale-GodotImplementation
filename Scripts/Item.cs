using System.Collections.Generic;
using Godot.Collections;
using Godot;

namespace VoxelEngine.Scripts;

[GlobalClass]
public partial class Item: Resource
{
	[Export] public Texture2D Icon;
	[Export] public string Name;
	[Export] public bool Stackable;
	[Export] public int MaxStack;
	[Export] public bool IsBlock;
	[Export] public VoxelType BlockType;
	[Export] public string InternalID;
	[Export] public Array<Item> itemLibrary { get; set; } = new Array<Item>();
	public Array<Item> ItemLibrary => itemLibrary;
}

using Godot;
using Godot.Collections;
using VoxelEngine.Scripts;

[GlobalClass]
public partial class ItemDatabase : Resource {
    [Export] public Array<Item> Items { get; set; }
}

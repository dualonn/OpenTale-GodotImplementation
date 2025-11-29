using Godot;
using System;
using System.Collections.Generic;
using VoxelEngine.Scripts;

public partial class Hotbar : Control
{

	private const int HotbarSize = 9;
	public List<Item> items = new();
	private int selectedIndex = 0;
	//public Item SelectedItem => items[selectedIndex];
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		//var itemLib = Item.ItemLibrary;
		for (int i = 0; i < HotbarSize; i++)
		{
			items.Add(null);
		}

		UpdateHotbar();
		HighlightSelection();
		
		//SetItem(0, Item.ItemLibrary[0]);
	}

	public override void _Input(InputEvent @event)
	{
		for (int i = 0; i < HotbarSize; i++)
		{
			if (@event.IsActionPressed($"hotbar_{i + 1}"))
			{
				SelectSlot(i);
				return;
			}
		}

		if (@event is InputEventMouseButton mouse)
		{
			if (mouse.ButtonIndex == MouseButton.WheelUp && mouse.Pressed)
			{
				SelectSlot((selectedIndex - 1 + HotbarSize) % HotbarSize);
			}

			if (mouse.ButtonIndex == MouseButton.WheelDown && mouse.Pressed)
			{
				SelectSlot((selectedIndex + 1) % HotbarSize);
			}
		}
	}

	private void SelectSlot(int index)
	{
		selectedIndex = index;
		HighlightSelection();
		GD.Print($"Selected item: {items[index]?.Name ?? "Empty"}");
	}

	private void HighlightSelection()
	{
		for (int i = 0; i < HotbarSize; i++)
		{
			var slot = GetChild<Control>(i);
			slot.Modulate = (i == selectedIndex) ? new Color(1, 1, 1) : new Color(0.7f, 0.7f, 0.7f);
		}
	}

	public void UpdateHotbar()
	{
		for (int i = 0; i < HotbarSize; i++)
		{
			var slot = GetChild<Control>(i);
			var iconRect = slot.GetNode<TextureRect>("Icon");

			if (items[i] != null)
			{
				iconRect.Texture = items[i].Icon;
				iconRect.Visible = true;
			}
			else
			{
				iconRect.Visible = false;
			}
		}
	}

	public void SetItem(int index, Item item)
	{
		items[index] = item;
		UpdateHotbar();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}

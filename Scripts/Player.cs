using Godot;
using System;

public partial class Player : CharacterBody3D
{
	// Called when the node enters the scene tree for the first time.

	[Export] public float speed = 3f;
	[Export] public float jumpVelocity = 6f;
	[Export] public float GravityScale = 1f;
	[Export] public bool InfiniteMode = false;
	private Camera3D cam;
	private Vector2 look;
	[Export] public World world;
	[Export] public float interactionDistance = 6f;
	public override void _Ready()
	{
		cam = GetNode<Camera3D>("Camera3D");
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel")) Input.MouseMode = Input.MouseModeEnum.Visible;
		if (@event is InputEventMouseButton mbe && mbe.Pressed) Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;
		
		Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");

		Vector3 direction = (Transform.Basis.Z * input.Y + Transform.Basis.X * input.X);
		direction.Y = 0;
		direction = direction.Normalized();

		velocity.X = direction.X * speed;
		velocity.Z = direction.Z * speed;
		
		if(!IsOnFloor()) velocity.Y -= InfiniteMode ? 0.0f : GravityScale * (float)delta;
		
		if (Input.IsActionJustPressed("jump") && IsOnFloor() && !InfiniteMode) velocity.Y = jumpVelocity;

		Velocity = velocity;
		MoveAndSlide();

		Vector2 mouseDelta = Input.GetLastMouseVelocity() * 0.00015f;
		look += new Vector2(-mouseDelta.Y, -mouseDelta.X);
		
		look.X = Mathf.Clamp(look.X, -1.5708f, 1.5708f);

		cam.Rotation = new Vector3(look.X, 0, 0);
		Rotation = new Vector3(0, look.Y, 0);
		
		HandleBlockInteraction();
	}

	private void HandleBlockInteraction()
	{
		var space = GetWorld3D().DirectSpaceState;

		var from = cam.GlobalPosition + cam.GlobalTransform.Basis.Z * -0.1f;
		var to = from + cam.GlobalTransform.Basis.Z * -interactionDistance;

		var result = new PhysicsRayQueryParameters3D()
		{
			From = from,
			To = to,
			CollideWithAreas = false,
			CollideWithBodies = true,
		};

		var hit = space.IntersectRay(result);

		if (hit.Count > 0)
		{
			Vector3 pos = (Vector3)hit["position"];
			Vector3 normal = ((Vector3)hit["normal"]).Normalized();

			// compute exact block grid coordinates
			Vector3 breakTarget = pos - normal * 0.0001f;
			Vector3 placeTarget = pos + normal * 0.5f;

			if (Input.IsActionJustPressed("break"))
				BreakBlock(breakTarget);

			if (Input.IsActionJustPressed("interact"))
				PlaceBlock(placeTarget, 1);
		}
	}

	private void BreakBlock(Vector3 pos)
	{
		world.BreakBlock(pos);
	}

	private void PlaceBlock(Vector3 pos, byte block)
	{
		world.PlaceBlock(pos, block);
	}
}

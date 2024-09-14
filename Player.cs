using Godot;
using System;

public partial class Player : CharacterBody3D
{
	[Export] public float Speed = 10f;
	[Export] public float Gravity = 4;
	[Export] public float Jump = 4;

	private float _yVelocity;
	private Node3D _model;
	private AnimationTree _animationTree;

	public override void _Ready()
	{
		_model = GetNode<Node3D>("Model");
		_animationTree = GetNode<AnimationTree>("AnimationTree");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("normal")) NormalAttack();
		if (@event.IsActionPressed("special")) SpecialAttack();
	}

	public override void _PhysicsProcess(double delta)
	{
		float movement = Input.GetAxis("right", "left");

		if (movement != 0)
			_model.Scale = new Vector3(1, 1, movement > 0 ? -1 : 1);

		_yVelocity -= Gravity * (float)delta;

		if (IsOnFloor() && _yVelocity < 0) _yVelocity = 0;

		if (IsOnFloor() && Input.IsActionPressed("up")) _yVelocity = Jump;
		Velocity = Vector3.Forward * movement * Speed + Vector3.Up * _yVelocity;

		MoveAndSlide();

		UpdateAnimationTree(movement);
	}

	private void UpdateAnimationTree(float movement)
	{
		_animationTree.Set("parameters/WalkBlend/blend_amount", movement != 0 ? 1 : 0);
	}

	private void NormalAttack()
	{
		_animationTree.Set("parameters/NormalOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void SpecialAttack()
	{
		_animationTree.Set("parameters/SpecialOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}
}

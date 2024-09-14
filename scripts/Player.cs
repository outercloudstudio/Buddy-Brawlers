using Godot;
using Networking;
using Riptide;
using System;

public partial class Player : CharacterBody3D, NetworkPointUser
{
	[Export] public float Speed = 10f;
	[Export] public float Gravity = 4;
	[Export] public float Jump = 4;

	public NetworkPoint NetworkPoint { get; set; } = new NetworkPoint();

	private NetworkedVariable<Vector3> _networkedPosition = new NetworkedVariable<Vector3>(Vector3.Zero);
	private NetworkedVariable<Vector3> _networkedVelocity = new NetworkedVariable<Vector3>(Vector3.Zero);
	private NetworkedVariable<float> _networkedMovement = new NetworkedVariable<float>(0);

	private float _yVelocity;
	private Node3D _model;
	private AnimationTree _animationTree;

	public override void _Ready()
	{
		_model = GetNode<Node3D>("Model");
		_animationTree = GetNode<AnimationTree>("AnimationTree");

		NetworkPoint.Setup(this);

		NetworkPoint.Register(nameof(_networkedPosition), _networkedPosition);
		NetworkPoint.Register(nameof(_networkedVelocity), _networkedVelocity);
		NetworkPoint.Register(nameof(_networkedMovement), _networkedMovement);

		NetworkPoint.Register(nameof(NormalAttackRpc), NormalAttackRpc);
		NetworkPoint.Register(nameof(SpecialAttackRpc), SpecialAttackRpc);
	}

	public override void _Input(InputEvent @event)
	{
		if (!NetworkPoint.IsOwner) return;

		if (@event.IsActionPressed("normal")) NormalAttack();
		if (@event.IsActionPressed("special")) SpecialAttack();
	}

	public override void _PhysicsProcess(double delta)
	{
		_yVelocity -= Gravity * (float)delta;

		if (IsOnFloor() && _yVelocity < 0) _yVelocity = 0;

		if (IsOnFloor() && Input.IsActionPressed("up")) _yVelocity = Jump;

		_networkedPosition.Sync();
		_networkedVelocity.Sync();
		_networkedMovement.Sync();

		float movement = Input.GetAxis("right", "left");

		if (NetworkPoint.IsOwner)
		{
			Velocity = Vector3.Forward * movement * Speed + Vector3.Up * _yVelocity;

			MoveAndSlide();
			UpdateAnimationTree(movement);

			_networkedPosition.Value = GlobalPosition;
			_networkedVelocity.Value = Velocity;
			_networkedMovement.Value = movement;
		}
		else
		{
			GlobalPosition = GlobalPosition.Lerp(_networkedPosition.Value, (float)delta * 20.0f);
			Velocity = _networkedVelocity.Value;

			movement = _networkedMovement.Value;

			MoveAndSlide();
			UpdateAnimationTree(_networkedMovement.Value);
		}

		if (movement != 0)
			_model.Scale = new Vector3(1, 1, movement > 0 ? -1 : 1);
	}

	private void UpdateAnimationTree(float movement)
	{
		_animationTree.Set("parameters/WalkBlend/blend_amount", movement != 0 ? 1 : 0);
	}

	private void NormalAttack()
	{
		NetworkPoint.BounceRpcToClientsFast(nameof(NormalAttackRpc));
	}

	private void SpecialAttack()
	{
		NetworkPoint.BounceRpcToClientsFast(nameof(SpecialAttackRpc));
	}

	private void NormalAttackRpc(Message message)
	{
		_animationTree.Set("parameters/NormalOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void SpecialAttackRpc(Message message)
	{
		_animationTree.Set("parameters/SpecialOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}
}

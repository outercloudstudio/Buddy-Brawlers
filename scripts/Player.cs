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
	private Node3D _flipOrigin;
	private Area3D _attackArea;
	private Vector3 _knockback;

	public override void _Ready()
	{
		_model = GetNode<Node3D>("Model");
		_animationTree = GetNode<AnimationTree>("AnimationTree");
		_flipOrigin = GetNode<Node3D>("FlipOrigin");
		_attackArea = _flipOrigin.GetNode<Area3D>("AttackArea");

		NetworkPoint.Setup(this);

		NetworkPoint.Register(nameof(_networkedPosition), _networkedPosition);
		NetworkPoint.Register(nameof(_networkedVelocity), _networkedVelocity);
		NetworkPoint.Register(nameof(_networkedMovement), _networkedMovement);

		NetworkPoint.Register(nameof(NormalAttackRpc), NormalAttackRpc);
		NetworkPoint.Register(nameof(SpecialAttackRpc), SpecialAttackRpc);
		NetworkPoint.Register(nameof(DamageRpc), DamageRpc);
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

		_knockback = MathHelper.FixedLerp(_knockback, Vector3.Zero, 4f, (float)delta);

		if (IsOnFloor() && _yVelocity < 0) _yVelocity = 0;

		if (IsOnFloor() && Input.IsActionPressed("up")) _yVelocity = Jump;

		_networkedPosition.Sync();
		_networkedVelocity.Sync();
		_networkedMovement.Sync();

		float movement = Input.GetAxis("right", "left");

		if (NetworkPoint.IsOwner)
		{
			Velocity = Vector3.Forward * movement * Speed + Vector3.Up * _yVelocity + _knockback;

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
		{
			_model.Scale = new Vector3(1, 1, movement > 0 ? -1 : 1);
			_flipOrigin.Scale = new Vector3(1, 1, movement > 0 ? -1 : 1);
		}
	}

	public void Damage(Vector3 knockback)
	{
		NetworkPoint.BounceRpcToClients(nameof(DamageRpc), message =>
		{
			message.AddFloat(knockback.X);
			message.AddFloat(knockback.Y);
			message.AddFloat(knockback.Z);
		});
	}

	private void UpdateAnimationTree(float movement)
	{
		_animationTree.Set("parameters/WalkBlend/blend_amount", movement != 0 ? 1 : 0);
	}

	private void NormalAttack()
	{
		NetworkPoint.BounceRpcToClientsFast(nameof(NormalAttackRpc));

		var bodies = _attackArea.GetOverlappingBodies();

		foreach (Node body in bodies)
		{
			if (body == this) continue;

			if (!(body is Player otherPlayer)) continue;

			Vector3 playerOffset = otherPlayer.GlobalPosition - GlobalPosition;
			playerOffset.Y = 0;
			playerOffset.X = 0;

			playerOffset = playerOffset.Normalized();

			otherPlayer.Damage(playerOffset * 7f + Vector3.Up * 5f);
		}
	}

	private void SpecialAttack()
	{
		NetworkPoint.BounceRpcToClientsFast(nameof(SpecialAttackRpc));

		var bodies = _attackArea.GetOverlappingBodies();

		foreach (Node body in bodies)
		{
			if (body == this) continue;

			if (!(body is Player otherPlayer)) continue;

			Vector3 playerOffset = otherPlayer.GlobalPosition - GlobalPosition;
			playerOffset.Y = 0;
			playerOffset.X = 0;

			playerOffset = playerOffset.Normalized();

			otherPlayer.Damage(playerOffset * 7f + Vector3.Up * 10f);
		}
	}

	private void NormalAttackRpc(Message message)
	{
		_animationTree.Set("parameters/NormalOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void SpecialAttackRpc(Message message)
	{
		_animationTree.Set("parameters/SpecialOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	private void DamageRpc(Message message)
	{
		Vector3 knockback = new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());

		_knockback = knockback;
	}
}

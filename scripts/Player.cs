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
	private AnimationTree _visualAnimationTree;
	private AnimationTree _animationTree;
	private AnimationPlayer _animationPlayer;
	private Node3D _flipOrigin;
	private Area3D _attackArea;
	private Vector3 _knockback;
	private float _movement;
	private bool _lastMovedRight = true;

	public override void _Ready()
	{
		_model = GetNode<Node3D>("Model");
		_visualAnimationTree = GetNode<AnimationTree>("VisualAnimationTree");
		_animationTree = GetNode<AnimationTree>("AnimationTree");
		_animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
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

	public override void _Process(double delta)
	{
		UpdateAnimationTree((float)delta);
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

		_movement = Input.GetAxis("right", "left");

		if (NetworkPoint.IsOwner)
		{
			Velocity = Vector3.Forward * _movement * Speed + Vector3.Up * _yVelocity + _knockback;

			MoveAndSlide();

			_networkedPosition.Value = GlobalPosition;
			_networkedVelocity.Value = Velocity;
			_networkedMovement.Value = _movement;
		}
		else
		{
			GlobalPosition = GlobalPosition.Lerp(_networkedPosition.Value, (float)delta * 20.0f);
			Velocity = _networkedVelocity.Value;

			_movement = _networkedMovement.Value;

			MoveAndSlide();
		}

		if (_movement != 0) _lastMovedRight = _movement < 0;

		_flipOrigin.Scale = new Vector3(1, 1, _lastMovedRight ? 1 : -1);
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

	private void UpdateAnimationTree(float delta)
	{
		_visualAnimationTree.Set("parameters/WalkBlend/blend_amount", MathHelper.FixedLerp((float)_visualAnimationTree.Get("parameters/WalkBlend/blend_amount"), _movement != 0 ? 1 : 0, 8f, delta));
		_visualAnimationTree.Set("parameters/NormalBlend/blend_amount", MathHelper.FixedLerp((float)_visualAnimationTree.Get("parameters/NormalBlend/blend_amount"), _animationPlayer.CurrentAnimation == "normal" && _animationPlayer.CurrentAnimationPosition > 0.1 && _animationPlayer.CurrentAnimationPosition < _animationPlayer.CurrentAnimationLength - 0.1 ? 1 : 0, 20f, delta));
		_visualAnimationTree.Set("parameters/SpecialBlend/blend_amount", MathHelper.FixedLerp((float)_visualAnimationTree.Get("parameters/SpecialBlend/blend_amount"), _animationPlayer.CurrentAnimation == "special" && _animationPlayer.CurrentAnimationPosition > 0.1 && _animationPlayer.CurrentAnimationPosition < _animationPlayer.CurrentAnimationLength - 0.1 ? 1 : 0, 20f, delta));

		_animationTree.Set("parameters/TurnBlendSpace/blend_position", MathHelper.FixedLerp((float)_animationTree.Get("parameters/TurnBlendSpace/blend_position"), _lastMovedRight ? 1 : -1, 12f, delta));
	}

	public virtual void TriggerNormalAttack()
	{
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

	public virtual void TriggerSpecialAttack()
	{
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

	private void NormalAttack()
	{
		if (_animationPlayer.IsPlaying()) return;

		NetworkPoint.BounceRpcToClientsFast(nameof(NormalAttackRpc));
	}

	private void SpecialAttack()
	{
		if (_animationPlayer.IsPlaying()) return;

		NetworkPoint.BounceRpcToClientsFast(nameof(SpecialAttackRpc));
	}

	private void NormalAttackRpc(Message message)
	{
		_visualAnimationTree.Set("parameters/NormalOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);

		_animationPlayer.Stop();
		_animationPlayer.Play("normal");
	}

	private void SpecialAttackRpc(Message message)
	{
		_visualAnimationTree.Set("parameters/SpecialOneShot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);

		_animationPlayer.Stop();
		_animationPlayer.Play("special");
	}

	private void DamageRpc(Message message)
	{
		Vector3 knockback = new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());

		_knockback = knockback;
	}
}

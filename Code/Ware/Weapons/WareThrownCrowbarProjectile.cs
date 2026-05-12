public sealed class WareThrownCrowbarProjectile : Projectile
{
	private const float HitSoundSpeed = 50f;
	private const float BodyHitSoundSpeed = 512f;
	private const float PlayerHitMinSpeed = 260f;
	private const float PlayerHitMaxSpeed = 900f;
	private const float PlayerHitLift = 160f;
	private const float PlayerHitGroundingDelay = 0.12f;
	private const float PlayerTraceRadius = 16f;

	private readonly HashSet<Player> _hitPlayers = new();
	private Vector3 _previousPosition;

	[Property] public float BounceRestitution { get; set; } = 0.72f;
	[Property] public float MinBounceSpeed { get; set; } = 80f;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_previousPosition = WorldPosition;
		_hitPlayers.Clear();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var velocity = Rigidbody.Velocity;
		if ( velocity.Length > HitSoundSpeed )
		{
			var player = TracePlayerHit();
			if ( player.IsValid() )
				TryApplyPlayerHit( player, velocity );
		}

		_previousPosition = WorldPosition;
	}

	protected override void OnHit( Collision collision = default )
	{
		var velocity = Rigidbody.Velocity;
		var speed = velocity.Length;

		if ( speed > HitSoundSpeed )
			Sound.Play( "weapons/crowbar/sounds/crowbar.hit.sound", WorldPosition );

		var player = collision.Other.GameObject.GetComponentInParent<Player>();
		if ( speed > BodyHitSoundSpeed && player.IsValid() )
		{
			Sound.Play( "weapons/crowbar/sounds/crowbar.hit.sound", WorldPosition );
			TryApplyPlayerHit( player, velocity );
		}

		var body = collision.Other.GameObject.GetComponentInChildren<Rigidbody>();
		if ( body.IsValid() )
			body.ApplyImpulseAt( collision.Contact.Point, Rigidbody.Velocity.Normal * body.Mass * 100f );

		Bounce( velocity, collision.Contact.Normal );
	}

	private Player TracePlayerHit()
	{
		var trace = Scene.Trace.FromTo( _previousPosition, WorldPosition )
			.Radius( PlayerTraceRadius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "projectile" );

		if ( Instigator.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( Instigator.GameObject );

		var result = trace.Run();
		if ( !result.Hit ) return null;

		return result.GameObject.GetComponentInParent<Player>();
	}

	private void TryApplyPlayerHit( Player player, Vector3 velocity )
	{
		if ( !player.IsValid() ) return;
		if ( _hitPlayers.Contains( player ) ) return;

		_hitPlayers.Add( player );
		ApplyPlayerHit( player, velocity );
	}

	private static void ApplyPlayerHit( Player player, Vector3 velocity )
	{
		var direction = velocity.Normal;
		var pushSpeed = Math.Clamp( velocity.Length, PlayerHitMinSpeed, PlayerHitMaxSpeed );
		var push = direction * pushSpeed;
		push.z = MathF.Max( push.z, PlayerHitLift );

		player.SetPendingWareDeathImpulse( push );
		player.ApplyWareVelocity( push, PlayerHitGroundingDelay );
	}

	private void Bounce( Vector3 velocity, Vector3 normal )
	{
		var speed = velocity.Length;
		if ( speed < MinBounceSpeed ) return;
		if ( normal.LengthSquared < 0.001f ) return;

		normal = normal.Normal;
		if ( velocity.Dot( normal ) > 0f )
			normal = -normal;

		var reflected = velocity - 2f * velocity.Dot( normal ) * normal;
		Rigidbody.Velocity = reflected * BounceRestitution;
		Rigidbody.AngularVelocity += Vector3.Cross( normal, velocity.Normal ) * 8f;
	}
}
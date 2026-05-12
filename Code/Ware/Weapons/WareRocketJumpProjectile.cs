public sealed class WareRocketJumpProjectile : Projectile
{
	private const float PushRadius = 60f;
	private const float OwnerPushSpeed = 1100f;
	private const float OwnerMinimumLift = 350f;
	private const float OtherPlayerBlastPushSpeed = 820f;
	private const float OtherPlayerDirectPushSpeed = 980f;
	private const float OtherPlayerBlastLift = 420f;
	private const float OtherPlayerDirectLift = 850f;
	private const float WorldTraceRadius = 4f;
	private const float PlayerTraceRadius = 14f;
	private const float PlayerGroundingDelay = 0.22f;

	private Vector3 _previousPosition;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_previousPosition = WorldPosition;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		var worldHit = TraceWorldHit();
		if ( worldHit.Hit )
		{
			Explode( null, worldHit.HitPosition );
			GameObject.Destroy();
			return;
		}

		var player = TracePlayerHit();
		if ( player.IsValid() )
		{
			Explode( player, WorldPosition );
			GameObject.Destroy();
			return;
		}

		_previousPosition = WorldPosition;
	}

	protected override void OnHit( Collision collision = default )
	{
		var directHit = collision.Other.GameObject.GetComponentInParent<Player>();
		Explode( directHit, collision.Contact.Point );
		GameObject.Destroy();
	}

	private SceneTraceResult TraceWorldHit()
	{
		var trace = Scene.Trace.FromTo( _previousPosition, WorldPosition )
			.Radius( WorldTraceRadius )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "ragdoll", "projectile", "player", "playercontroller" );

		if ( Instigator.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( Instigator.GameObject );

		return trace.Run();
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

	private void Explode( Player directHit, Vector3 blastPosition )
	{
		if ( IsPlayerInsideBlast( Instigator, blastPosition ) )
		{
			Instigator.SetWareVelocity( GetOwnerRocketJumpVelocity( blastPosition ), 0.2f );
		}

		foreach ( var player in Scene.GetAll<Player>() )
		{
			if ( !player.IsValid() || player == Instigator ) continue;
			if ( player != directHit && !IsPlayerInsideBlast( player, blastPosition ) ) continue;

			ApplyPlayerBlast( player, blastPosition, player == directHit );
		}
	}

	private Vector3 GetOwnerRocketJumpVelocity( Vector3 blastPosition )
	{
		var aim = Instigator.EyeTransform.Forward.Normal;
		var horizontal = -aim.WithZ( 0f );
		if ( horizontal.LengthSquared > 0.001f )
			horizontal = horizontal.Normal * (OwnerPushSpeed * horizontal.Length.Clamp( 0f, 1f ));

		var aimPush = horizontal + Vector3.Up * MathF.Max( MathF.Abs( aim.z ) * OwnerPushSpeed, OwnerMinimumLift );
		var playerCenter = Instigator.WorldPosition + Vector3.Up * (Instigator.Controller.CurrentHeight * 0.5f);
		var blastPush = (playerCenter - blastPosition).Normal * OwnerPushSpeed;
		blastPush.z = MathF.Abs( blastPush.z );

		var push = aimPush.z >= blastPush.z ? aimPush : blastPush;
		push.z = MathF.Max( push.z, OwnerMinimumLift );
		return push;
	}

	private bool IsPlayerInsideBlast( Player player, Vector3 blastPosition )
	{
		if ( !player.IsValid() || !player.Controller.IsValid() ) return false;

		var bottom = player.WorldPosition;
		var top = bottom + Vector3.Up * player.Controller.CurrentHeight;
		var closest = ClosestPointOnSegment( blastPosition, bottom, top );
		var radius = PushRadius + player.Controller.BodyRadius;

		return closest.Distance( blastPosition ) <= radius;
	}

	private static Vector3 ClosestPointOnSegment( Vector3 point, Vector3 start, Vector3 end )
	{
		var segment = end - start;
		var lengthSquared = segment.LengthSquared;
		if ( lengthSquared <= 0.001f ) return start;

		var fraction = (point - start).Dot( segment ) / lengthSquared;
		fraction = Math.Clamp( fraction, 0f, 1f );
		return start + segment * fraction;
	}

	private void ApplyPlayerBlast( Player player, Vector3 blastPosition, bool directHit )
	{
		var playerCenter = player.WorldPosition + Vector3.Up * (player.Controller.CurrentHeight * 0.5f);
		var direction = (playerCenter - blastPosition).Normal;
		if ( direction.LengthSquared < 0.001f )
			direction = Vector3.Up;

		var speed = directHit ? OtherPlayerDirectPushSpeed : OtherPlayerBlastPushSpeed;
		var lift = directHit ? OtherPlayerDirectLift : OtherPlayerBlastLift;
		var push = direction * speed;
		push.z = MathF.Max( push.z, lift );

		if ( directHit )
			player.SetWareVelocity( push, PlayerGroundingDelay );
		else
			player.ApplyWareVelocity( push, PlayerGroundingDelay );
	}
}
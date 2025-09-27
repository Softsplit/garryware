using Sandbox.Rendering;
using Sandbox.Utility;

public class RpgWeapon : BaseWeapon
{
	[Property] public float TimeBetweenShots { get; set; } = 2f;
	[Property] public GameObject ProjectilePrefab { get; set; }
	[Property] public GameObject LaserEnd { get; set; }

	[Sync, Change( nameof( OnLaserChanged ) )] public bool Laser { get; set; }
	[Sync( SyncFlags.FromHost )] RpgProjectile Projectile { get; set; }

	private void OnLaserChanged( bool before, bool after )
	{
		LaserEnd.Enabled = after;
	}

	protected override void OnEnabled()
	{
		LaserEnd.Enabled = false;

		base.OnEnabled();
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Down( "attack1" ) )
		{
			Shoot( player );
		}

		if ( Input.Released( "Attack2" ) )
		{
			Laser = !Laser;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Laser )
		{
			var player = Owner;
			if ( !player.IsValid() )
				return;

			var forward = player.EyeTransform.Rotation.Forward;
			forward = forward.Normal;

			var tr = Scene.Trace.Ray( player.EyeTransform.ForwardRay with { Forward = forward }, 4096 )
				.IgnoreGameObjectHierarchy( player.GameObject )
				.WithoutTags( "projectile" )

				.UseHitboxes()
				.Run();

			LaserEnd.WorldPosition = tr.EndPosition + (-tr.Direction * 2f);
			LaserEnd.WorldRotation = Rotation.LookAt( -tr.Normal );

			if ( !Projectile.IsValid() ) return;

			if ( !IsProxy )
			{
				Projectile.UpdateWithTarget( tr.EndPosition, 1024 );
			}
		}
	}

	private Vector3 CheckThrowPosition( Player player, Vector3 eyePosition, Vector3 grenadePosition )
	{
		var tr = Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, 8.0f ), eyePosition, grenadePosition )
			.WithoutTags( "trigger", "ragdoll", "player", "effect" )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();

		if ( tr.Hit )
		{
			Log.Info( tr.GameObject );
			return tr.EndPosition;
		}

		return grenadePosition;
	}

	TimeSince TimeSinceShoot;

	public void Shoot( Player player )
	{
		if ( !CanShoot() || !TakeAmmo( 1 ) )
		{
			TryAutoReload();
			return;
		}

		TimeSinceShoot = 0;

		AddShootDelay( TimeBetweenShots );

		ViewModel?.RunEvent<ViewModel>( x => x.OnAttack() );

		var transform = player.EyeTransform;
		transform.Position = transform.Position + Vector3.Down * 8f + transform.Right * 8f;
		var forward = transform.Forward;
		var right = transform.Right;
		var initialPos = transform.ForwardRay.Position + (forward * 64.0f);

		initialPos = CheckThrowPosition( player, transform.Position + (forward * 0.0f), initialPos );

		CreateProjectile( initialPos, transform.Forward, 1024 );

		player.Controller.EyeAngles += new Angles( Random.Shared.Float( -0.2f, -0.3f ), Random.Shared.Float( -0.1f, 0.1f ), 0 );

		if ( !player.Controller.ThirdPerson && player.IsLocalPlayer )
		{
			new Sandbox.CameraNoise.Punch( new Vector3( Random.Shared.Float( 45, 35 ), Random.Shared.Float( -10, -5 ), 0 ), 1.5f, 2, 0.5f );
			new Sandbox.CameraNoise.Shake( 1f, 0.6f );

			if ( HasAmmo() )
			{
				ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );
			}
		}
	}

	/// <summary>
	/// Creates the projectile with the host's permission
	/// </summary>
	/// <param name="start"></param>
	/// <param name="direction"></param>
	/// <param name="speed"></param>
	[Rpc.Host]
	void CreateProjectile( Vector3 start, Vector3 direction, float speed )
	{
		if ( !Owner.IsValid() ) return;

		var go = ProjectilePrefab?.Clone( start );

		var projectile = go.GetComponent<RpgProjectile>();
		Assert.True( projectile.IsValid(), "RpgProjectile not on projectile prefab" );

		projectile.InstigatorId = Owner.PlayerId;
		projectile.Explosive.InstigatorId = Owner.PlayerId;

		go.NetworkSpawn();

		Projectile = projectile;
		projectile.UpdateDirection( direction, speed );
	}

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var tss = TimeSinceShoot.Relative.Remap( 0, 0.2f, 1, 0 );

		var gap = 6 + Easing.EaseOut( tss ) * 32;
		var len = 6;
		var w = 2;

		Color color = !CanShoot() ? CrosshairNoShoot : CrosshairCanShoot;

		hud.SetBlendMode( BlendMode.Lighten );

		// Define the size of the square
		var squareSize = 64f;

		// Draw the four edges of the square
		hud.DrawLine( center + new Vector2( -squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, -squareSize / 2 ), w, color ); // Top edge
		hud.DrawLine( center + new Vector2( squareSize / 2, -squareSize / 2 ), center + new Vector2( squareSize / 2, squareSize / 2 ), w, color );   // Right edge
		hud.DrawLine( center + new Vector2( squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, squareSize / 2 ), w, color );  // Bottom edge
		hud.DrawLine( center + new Vector2( -squareSize / 2, squareSize / 2 ), center + new Vector2( -squareSize / 2, -squareSize / 2 ), w, color ); // Left edge

		if ( Laser )
		{
			gap += 32f;
			len = 16;
			hud.DrawLine( center + Vector2.Left * (len + gap), center + Vector2.Left * gap, w, color );
			hud.DrawLine( center - Vector2.Left * (len + gap), center - Vector2.Left * gap, w, color );
			hud.DrawLine( center + Vector2.Up * (len + gap), center + Vector2.Up * gap, w, color );
			hud.DrawLine( center - Vector2.Up * (len + gap), center - Vector2.Up * gap, w, color );
		}
	}
}

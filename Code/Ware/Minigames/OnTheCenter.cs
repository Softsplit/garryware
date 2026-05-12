namespace WareMinigames;

public sealed class OnTheCenter : WareMinigame
{
	private Vector3 _center;
	private float _radius;

	public override string Name => "onthecenter";
	public override string Title => "On the center!";
	public override string Room => "hexaprism";
	public override float Windup => 1f;
	public override float Duration => 6f;

	public override bool CanPlay( IReadOnlyList<Player> players )
	{
		return players.Count >= 2;
	}

	public override void Initialize()
	{
		_center = Environment.GetCenter( "center" );
		var land = Environment.GetRandomLocation( "land_a" );
		_radius = land.IsValid() ? MathF.Max( 32f, ((_center - land.WorldPosition).Length - 64f) * 0.4f ) : 128f;
		CreateRingZone();
		SetInstruction( "On the center!" );
	}

	public override void StartAction()
	{
		foreach ( var player in Players )
			GiveWeapon( player, WareWeaponPaths.RocketJump );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			var distance = (player.WorldPosition.WithZ( _center.z ) - _center).Length;
			SetAchieved( player, distance <= _radius );
		}
	}

	private void CreateRingZone()
	{
		var marker = CreateTemporaryObject( "ware_ringzone", _center + Vector3.Up * 0.5f, true );
		var ring = marker.Components.Create<WareRingZone>();
		ring.Radius = _radius;
		ring.ZoneColor = Color.FromBytes( 185, 220, 255 );
		marker.NetworkSpawn();
	}
}

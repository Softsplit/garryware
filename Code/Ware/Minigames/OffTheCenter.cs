namespace WareMinigames;

public sealed class OffTheCenter : WareMinigame
{
	private Vector3 _center;
	private float _radius;
	private float _heightLimit;

	public override string Name => "offthecenter";
	public override string Title => "Away from the center! Don't fall!";
	public override string Room => "hexaprism";
	public override float Windup => 4f;
	public override float Duration => 4f;
	public override bool? InitialPlayerResult => true;

	public override bool CanPlay( IReadOnlyList<Player> players )
	{
		return players.Count >= 2;
	}

	public override void Initialize()
	{
		_center = Environment.GetCenter( "center" );
		_radius = GetOuterRadius();
		_heightLimit = GetHeightLimit();
		CreateRingZone();
		SetInstruction( "Away from the center! Don't fall!" );
	}

	public override void StartAction()
	{
		foreach ( var player in Players )
			GiveWeapon( player, WareWeaponPaths.Crowbar );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			if ( player.PlayerData.WareLocked ) continue;

			if ( player.WorldPosition.z < _heightLimit )
			{
				FailAndSimulateDeath( player );
				continue;
			}

			var distance = (player.WorldPosition.WithZ( _center.z ) - _center).Length;
			if ( distance > _radius * 0.95f ) continue;

			var impulse = (player.WorldPosition + Vector3.Up * 128f - _center).Normal * 1000f;
			FailAndSimulateDeath( player, impulse );
		}
	}

	private float GetOuterRadius()
	{
		var land = Environment.GetRandomLocation( "land_a" );
		return land.IsValid() ? MathF.Max( 32f, (_center - land.WorldPosition).Length - 64f ) : 320f;
	}

	private float GetHeightLimit()
	{
		var pit = Environment.GetRandomLocation( "pit_measure" );
		var land = Environment.GetRandomLocation( "land_measure" );

		if ( pit.IsValid() && land.IsValid() )
			return pit.WorldPosition.z + (land.WorldPosition.z - pit.WorldPosition.z) * 0.8f;

		return Environment.GetLowestZ( "land_a", "center" ) - 128f;
	}

	private void CreateRingZone()
	{
		var marker = CreateTemporaryObject( "ware_ringzone", _center + Vector3.Up * 0.5f, true );
		var ring = marker.Components.Create<WareRingZone>();
		ring.Radius = _radius;
		ring.ZoneColor = Color.Black;
		marker.NetworkSpawn();
	}
}
public sealed class WareEnvironment
{
	private readonly Scene _scene;
	private readonly List<WareRoom> _rooms = new();
	private readonly List<GameObject> _all = new();

	public WareRoom Current { get; private set; }
	public IReadOnlyList<WareRoom> Rooms => _rooms;
	public IReadOnlyList<GameObject> AllLocations => Current?.AllLocations ?? _all;

	public WareEnvironment( Scene scene )
	{
		_scene = scene;
		Rebuild();
	}

	public void Rebuild()
	{
		_rooms.Clear();
		_all.Clear();

		AddSceneRooms();
		AddSceneLocations();
		AddSceneSpawnPoints();

		Current = FindEnvironment( Current?.Name ?? "generic", 0 ) ?? _rooms.FirstOrDefault();
	}

	public bool Select( string roomName, int playerCount )
	{
		var next = FindEnvironment( roomName, playerCount );
		if ( next is null ) return false;

		Current = next;
		return true;
	}

	public bool HasEnvironment( string roomName )
	{
		return FindCandidates( roomName ).Any();
	}

	public IReadOnlyList<GameObject> GetLocations( params string[] groups )
	{
		return Current?.GetLocations( groups ) ?? [];
	}

	public GameObject GetRandomLocation( params string[] groups )
	{
		var locations = GetLocations( groups );
		return locations.Count == 0 ? null : Random.Shared.FromArray( locations.ToArray() );
	}

	public Vector3 GetCenter( params string[] groups )
	{
		var locations = GetLocations( groups );
		if ( locations.Count == 0 ) return Current?.Center ?? Vector3.Zero;

		var center = Vector3.Zero;
		foreach ( var location in locations )
			center += location.WorldPosition;

		return center / locations.Count;
	}

	public float GetLowestZ( params string[] groups )
	{
		var locations = GetLocations( groups );
		if ( locations.Count == 0 ) return Current?.Center.z ?? 0f;

		return locations.Min( x => x.WorldPosition.z );
	}

	public Transform GetRandomSpawnTransform()
	{
		if ( Current is null ) return Transform.Zero;

		if ( Current.SpawnPoints.Count > 0 )
			return Random.Shared.FromList( Current.SpawnPoints ).WorldTransform.WithScale( 1 );

		var fallback = Current.GetLocations( "cross", "dark_ground", "light_ground", "oncrate", "center" );
		if ( fallback.Count > 0 )
		{
			var location = Random.Shared.FromArray( fallback.ToArray() );
			return new Transform( location.WorldPosition + Vector3.Up * 24f ).WithScale( 1 );
		}

		return new Transform( Current.Center ).WithScale( 1 );
	}

	private WareRoom FindEnvironment( string roomName, int playerCount )
	{
		if ( string.Equals( roomName, "none", StringComparison.OrdinalIgnoreCase ) && Current is not null )
			return Current;

		var candidates = FindCandidates( roomName ).ToArray();
		if ( candidates.Length == 0 ) return null;

		return FindBestEnvironmentByPlayerCount( candidates, playerCount );
	}

	private IEnumerable<WareRoom> FindCandidates( string roomName )
	{
		if ( string.Equals( roomName, "none", StringComparison.OrdinalIgnoreCase ) )
			return Current is null ? FindCandidates( "generic" ) : [Current];

		if ( string.IsNullOrWhiteSpace( roomName ) || string.Equals( roomName, "generic", StringComparison.OrdinalIgnoreCase ) )
			return _rooms.Where( x => x.Name.Contains( "generic", StringComparison.OrdinalIgnoreCase ) );

		return _rooms.Where( x => x.Name.Contains( roomName, StringComparison.OrdinalIgnoreCase ) );
	}

	private static WareRoom FindBestEnvironmentByPlayerCount( IReadOnlyList<WareRoom> rooms, int playerCount )
	{
		WareRoom bestOutsideRange = null;
		float bestOutsideDiff = float.MaxValue;

		WareRoom bestInsideRange = null;
		float bestInsideDiff = float.MaxValue;

		foreach ( var room in rooms )
		{
			if ( playerCount >= room.MinPlayers && playerCount < room.MaxPlayers )
			{
				var midpoint = (room.MinPlayers + room.MaxPlayers) * 0.5f;
				var diff = MathF.Abs( playerCount - midpoint );
				if ( diff < bestInsideDiff )
				{
					bestInsideDiff = diff;
					bestInsideRange = room;
				}
				continue;
			}

			var outsideDiff = playerCount < room.MinPlayers ? room.MinPlayers - playerCount : playerCount - room.MaxPlayers;
			if ( outsideDiff < bestOutsideDiff )
			{
				bestOutsideDiff = outsideDiff;
				bestOutsideRange = room;
			}
		}

		return bestInsideRange ?? bestOutsideRange ?? rooms.FirstOrDefault();
	}

	private void AddSceneRooms()
	{
		foreach ( var component in _scene.GetAllComponents<WareRoomComponent>() )
		{
			if ( !component.IsValid() ) continue;

			var room = new WareRoom( component );
			_rooms.Add( room );
		}
	}

	private void AddSceneLocations()
	{
		foreach ( var location in _scene.GetAllComponents<WareLocationComponent>() )
		{
			if ( !location.IsValid() || !location.Room.IsValid() ) continue;

			var room = _rooms.FirstOrDefault( x => x.Component == location.Room );
			if ( room is null ) continue;

			_all.Add( location.GameObject );
			room.AddLocation( location.GameObject.Name, location.GameObject );
		}
	}

	private void AddSceneSpawnPoints()
	{
		foreach ( var spawnPoint in _scene.GetAllComponents<SpawnPoint>() )
		{
			if ( !spawnPoint.IsValid() ) continue;

			var room = _rooms.FirstOrDefault( x => x.Contains( spawnPoint.WorldPosition ) );
			if ( room is null ) continue;

			room.SpawnPoints.Add( spawnPoint.GameObject );
		}
	}
}

public sealed class WareRoom
{
	private readonly Dictionary<string, List<GameObject>> _locations = new( StringComparer.OrdinalIgnoreCase );

	public WareRoom( WareRoomComponent component )
	{
		Component = component;
	}

	public WareRoomComponent Component { get; }
	public string Name => Source.Name;
	public int MinPlayers => Component.MinPlayers;
	public int MaxPlayers => Component.MaxPlayers;
	public GameObject Source => Component.GameObject;
	public List<GameObject> SpawnPoints { get; } = new();
	public Vector3 Center => AllLocations.Count > 0 ? AverageLocationPosition() : Source.WorldPosition;
	public IReadOnlyList<GameObject> AllLocations => _locations.Values.SelectMany( x => x ).Distinct().ToArray();

	public bool Contains( Vector3 position )
	{
		return Source.GetBounds().Grow( 1f ).Contains( position );
	}

	public void AddLocation( string group, GameObject location )
	{
		if ( string.IsNullOrWhiteSpace( group ) || !location.IsValid() ) return;

		if ( !_locations.TryGetValue( group, out var list ) )
		{
			list = new List<GameObject>();
			_locations[group] = list;
		}

		if ( !list.Contains( location ) )
			list.Add( location );
	}

	public IReadOnlyList<GameObject> GetLocations( params string[] groups )
	{
		var results = new List<GameObject>();

		foreach ( var group in groups )
		{
			if ( string.IsNullOrWhiteSpace( group ) ) continue;
			if ( _locations.TryGetValue( group, out var list ) )
				results.AddRange( list );
		}

		return results.Distinct().ToArray();
	}

	private Vector3 AverageLocationPosition()
	{
		var locations = AllLocations;
		var center = Vector3.Zero;

		foreach ( var location in locations )
			center += location.WorldPosition;

		return center / locations.Count;
	}
}
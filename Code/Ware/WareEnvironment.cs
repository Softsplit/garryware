public sealed class WareEnvironment
{
	private readonly Scene _scene;
	private readonly List<WareRoom> _rooms = new();
	private readonly List<GameObject> _allLocations = new();

	public WareRoom Current { get; private set; }
	public IReadOnlyList<WareRoom> Rooms => _rooms;
	public IReadOnlyList<GameObject> AllLocations => Current?.AllLocations ?? _allLocations;

	public WareEnvironment( Scene scene )
	{
		_scene = scene;
		Rebuild();
	}

	public void Rebuild()
	{
		_rooms.Clear();
		_allLocations.Clear();

		AddSceneRooms();
		AddSceneLocations();
		AddSceneSpawnPoints();

		Current = FindRoom( Current?.Name ?? "generic", 0 ) ?? _rooms.FirstOrDefault();
	}

	public bool Select( string roomName, int playerCount )
	{
		var next = FindRoom( roomName, playerCount );
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
		return GetSpawnTransforms( 1 ).FirstOrDefault( Transform.Zero );
	}

	public IReadOnlyList<Transform> GetSpawnTransforms( int count )
	{
		if ( count <= 0 ) return [];
		if ( Current is null ) return RepeatSpawn( Transform.Zero, count );

		var candidates = GetSpawnCandidates();
		if ( candidates.Count == 0 )
			return RepeatSpawn( new Transform( Current.Center ).WithScale( 1 ), count );

		var results = new List<Transform>( count );
		var pool = new List<Transform>();

		for ( var spawnCount = 0; spawnCount < count; spawnCount++ )
		{
			if ( pool.Count == 0 )
				pool.AddRange( candidates );

			var index = Random.Shared.Int( 0, pool.Count - 1 );
			results.Add( pool[index] );
			pool.RemoveAt( index );
		}

		return results;
	}

	private static IReadOnlyList<Transform> RepeatSpawn( Transform spawn, int count )
	{
		var results = new List<Transform>( count );

		for ( var spawnCount = 0; spawnCount < count; spawnCount++ )
		{
			results.Add( spawn );
		}

		return results;
	}

	private IReadOnlyList<Transform> GetSpawnCandidates()
	{
		if ( Current.SpawnPoints.Count > 0 )
			return Current.SpawnPoints.Select( spawnPoint => spawnPoint.WorldTransform.WithScale( 1 ) ).ToArray();

		var fallback = Current.GetLocations( "cross", "dark_ground", "light_ground", "oncrate", "center" );
		if ( fallback.Count > 0 )
			return fallback.Select( location => new Transform( location.WorldPosition ).WithScale( 1 ) ).ToArray();

		return [];
	}

	private WareRoom FindRoom( string roomName, int playerCount )
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
			if ( playerCount >= room.MinPlayers && playerCount <= room.MaxPlayers )
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

			_allLocations.Add( location.GameObject );
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
	public List<GameObject> SpawnPoints { get; } = [];
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
			list = [];
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

		return [.. results.Distinct()];
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
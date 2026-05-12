public sealed class WareRoundSystem : GameObjectSystem<WareRoundSystem>, Global.IPlayerEvents, ISceneLoadingEvents
{
	private readonly WareMinigameRegistry _registry = new();
	private readonly List<GameObject> _temporaryObjects = new();
	private WareMinigame _current;
	private WareRoom _previousRoom;
	private TimeUntil _phaseEnds;

	public WareEnvironment Environment { get; private set; }
	public WareRoundPhase Phase { get; private set; } = WareRoundPhase.Waiting;
	public int WaresPlayed { get; private set; }
	public IEnumerable<Player> Players => Scene.GetAllComponents<Player>().Where( x => x.IsValid() && x.PlayerData.IsValid() );
	public float TimeRemaining => MathF.Max( 0f, _phaseEnds );

	public WareRoundSystem( Scene scene ) : base( scene )
	{
		Environment = new WareEnvironment( scene );
		Listen( Stage.StartUpdate, 0, Tick, "WareRoundSystem" );
	}

	async Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context )
	{
		await Task.Yield();
		Environment.Rebuild();
	}

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		_temporaryObjects.Clear();
	}

	private void Tick()
	{
		if ( !Networking.IsHost ) return;

		var players = Players.ToArray();
		if ( players.Length == 0 )
		{
			if ( Phase != WareRoundPhase.Waiting )
				SetWaiting( "Waiting for players" );

			return;
		}

		switch ( Phase )
		{
			case WareRoundPhase.Waiting:
				StartNextWindup( players );
				break;
			case WareRoundPhase.Windup:
				if ( _phaseEnds <= 0 )
					StartAction();
				break;
			case WareRoundPhase.Action:
				_current?.UpdateAction();
				if ( _phaseEnds <= 0 )
					EndAction();
				break;
			case WareRoundPhase.Results:
				if ( _phaseEnds <= 0 )
					StartNextWindup( players );
				break;
		}
	}

	private void StartNextWindup( IReadOnlyList<Player> players )
	{
		CleanupTemporaryObjects();
		Environment.Rebuild();

		_current = _registry.CreateNext( this, players );
		if ( _current is null )
		{
			SetWaiting( "No wares registered" );
			return;
		}

		_previousRoom = Environment.Current;
		if ( !Environment.Select( _current.Room, players.Count ) )
		{
			SetWaiting( $"No room for {_current.Room}" );
			return;
		}

		if ( _previousRoom != Environment.Current )
			MovePlayersToCurrentRoom( players );

		foreach ( var player in players )
			player.PlayerData.ResetWareState( _current.InitialResult );

		_current.Initialize();

		Phase = WareRoundPhase.Windup;
		_phaseEnds = _current.Windup;
	}

	private void MovePlayersToCurrentRoom( IReadOnlyList<Player> players )
	{
		foreach ( var player in players )
		{
			if ( !player.IsValid() ) continue;

			var spawn = Environment.GetRandomSpawnTransform();
			player.WorldPosition = spawn.Position;

			if ( player.Controller.IsValid() )
				player.Controller.EyeAngles = spawn.Rotation.Angles();
		}
	}

	private void StartAction()
	{
		Phase = WareRoundPhase.Action;
		_phaseEnds = _current.Duration;
		_current.StartAction();
	}

	private void EndAction()
	{
		_current?.EndAction();

		foreach ( var player in Players )
			player.PlayerData.LockWareResult();

		WaresPlayed++;
		Phase = WareRoundPhase.Results;
		_phaseEnds = 2.5f;
	}

	private void SetWaiting( string instruction )
	{
		CleanupTemporaryObjects();
		_current = null;
		Phase = WareRoundPhase.Waiting;
		_phaseEnds = 2f;
	}

	public void SetInstruction( string instruction )
	{
	}

	public void SetAchieved( Player player, bool achieved )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() || !player.PlayerData.IsValid() ) return;
		if ( Phase == WareRoundPhase.Windup ) return;

		player.PlayerData.SetWareAchieved( achieved );
	}

	public void LockResult( Player player, bool achieved )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() || !player.PlayerData.IsValid() ) return;

		player.PlayerData.SetWareAchieved( achieved );
		player.PlayerData.LockWareResult();
	}

	public void GiveWeapon( Player player, string prefabPath )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;
		if ( inventory.Weapons.Any( x => string.Equals( x.GameObject.PrefabInstanceSource, prefabPath, StringComparison.OrdinalIgnoreCase ) ) ) return;

		inventory.Give( prefabPath );
	}

	public GameObject CreateTemporaryObject( string name, Vector3 position )
	{
		var go = new GameObject( true, name );
		go.WorldPosition = position;
		go.Flags = GameObjectFlags.NotSaved;
		go.Tags.Add( "ware" );
		go.Tags.Add( "removable" );
		_temporaryObjects.Add( go );
		return go;
	}

	public void CleanupTemporaryObjects()
	{
		foreach ( var go in _temporaryObjects.ToArray() )
		{
			if ( go.IsValid() )
				go.Destroy();
		}

		_temporaryObjects.Clear();
	}

	void Global.IPlayerEvents.OnPlayerDied( Player player, PlayerDiedParams args )
	{
		if ( !Networking.IsHost ) return;
		if ( Phase != WareRoundPhase.Action ) return;
		if ( !player.PlayerData.IsValid() ) return;

		LockResult( player, false );
	}

	void Global.IPlayerEvents.OnPlayerRespawning( PlayerRespawnEvent e )
	{
		if ( Environment?.Current is null ) return;

		e.SpawnLocation = Environment.GetRandomSpawnTransform();
	}

	[ConCmd( "ware_next" )]
	private static void ForceNextWare()
	{
		if ( !Networking.IsHost ) return;
		Current?.StartNextWindup( Current.Players.ToArray() );
	}
}
public sealed partial class WareRoundSystem : GameObjectSystem<WareRoundSystem>, Global.IPlayerEvents, ISceneLoadingEvents
{
	private readonly WareMinigameRegistry _registry = new();
	private readonly List<GameObject> _temporaryObjects = new();
	private readonly List<Transform> _deathSpawnPool = new();
	private readonly HashSet<long> _playersSentStatus = new();
	private WareMinigame _currentMinigame;
	private bool _introPlayed;
	private bool _everyoneStatusSent;
	private TimeUntil _phaseTimeRemaining;

	public WareEnvironment Environment { get; private set; }
	public WareRoundPhase Phase { get; private set; } = WareRoundPhase.Waiting;
	public int WaresPlayed { get; private set; }
	public string CurrentInstruction { get; private set; }
	public string CurrentWareName => _currentMinigame?.Name;
	public IEnumerable<Player> Players => Scene.GetAllComponents<Player>().Where( x => x.IsValid() && x.PlayerData.IsValid() );
	public float TimeRemaining => MathF.Max( 0f, _phaseTimeRemaining );

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
			if ( HasConnectedPlayers() )
			{
				if ( Phase is WareRoundPhase.Waiting or WareRoundPhase.Results )
					return;
			}
			else if ( Phase != WareRoundPhase.Waiting )
			{
				WaitForPlayers();
				return;
			}

			if ( Phase == WareRoundPhase.Waiting ) return;
		}

		switch ( Phase )
		{
			case WareRoundPhase.Waiting:
				StartNextWare( players );
				break;
			case WareRoundPhase.Windup:
				if ( _phaseTimeRemaining <= 0 )
					StartActionPhase();
				break;
			case WareRoundPhase.Action:
				_currentMinigame?.UpdateAction();
				if ( _phaseTimeRemaining <= 0 )
					FinishActionPhase();
				break;
			case WareRoundPhase.Results:
				if ( _phaseTimeRemaining <= 0 )
					StartNextWare( players );
				break;
		}
	}

	private void StartNextWare( IReadOnlyList<Player> players )
	{
		CleanupTemporaryObjects();
		Environment.Rebuild();

		_currentMinigame = SelectNextWare( players );
		if ( _currentMinigame is null )
		{
			SetWaiting( "No playable wares" );
			return;
		}

		if ( !Environment.Select( _currentMinigame.Room, players.Count ) )
		{
			SetWaiting( $"No room for {_currentMinigame.Room}" );
			return;
		}

		_deathSpawnPool.Clear();

		MovePlayersToCurrentRoom( players );

		ResetPlayersForWare( players );

		ClearStatus();
		_everyoneStatusSent = false;
		_playersSentStatus.Clear();
		_currentMinigame.Initialize();
		StartWareAudio( _currentMinigame.Name == "_intro", _currentMinigame.Windup + _currentMinigame.Duration );

		Phase = WareRoundPhase.Windup;
		_phaseTimeRemaining = _currentMinigame.Windup;
	}

	private void ResetPlayersForWare( IReadOnlyList<Player> players )
	{
		foreach ( var player in players )
		{
			player.RestoreWareDeath();
			player.PlayerData.ResetWareState( _currentMinigame.InitialPlayerResult );
			RemoveWeapons( player );
		}
	}

	private WareMinigame SelectNextWare( IReadOnlyList<Player> players )
	{
		if ( !_introPlayed )
		{
			_introPlayed = true;
			return _registry.CreateIntro( this );
		}

		return _registry.CreateNext( this, players );
	}

	private void MovePlayersToCurrentRoom( IReadOnlyList<Player> players )
	{
		var spawns = Environment.GetSpawnTransforms( players.Count );

		for ( var playerIndex = 0; playerIndex < players.Count; playerIndex++ )
		{
			var player = players[playerIndex];
			if ( !player.IsValid() ) continue;

			player.MoveToWareSpawn( spawns[playerIndex] );
		}
	}

	private void StartActionPhase()
	{
		Phase = WareRoundPhase.Action;
		var duration = _currentMinigame.Duration;
		StartActionTimer( duration );
		StartActionAudio( duration, _currentMinigame );
		_currentMinigame.BeginAction();
	}

	private void StartActionTimer( float duration )
	{
		_phaseTimeRemaining = duration;
	}

	private void FinishActionPhase()
	{
		var nextPhaseDuration = _currentMinigame?.GetNextPhaseDuration();
		if ( nextPhaseDuration is > 0f )
		{
			StartActionTimer( nextPhaseDuration.Value );
			PlayPhaseAudio( nextPhaseDuration.Value );
			StartActionAudio( nextPhaseDuration.Value, _currentMinigame );
			_currentMinigame.BeginNextActionPhase( _currentMinigame.ActionPhase + 1 );
			return;
		}

		_currentMinigame?.EndAction();

		var players = Players.ToArray();

		if ( _currentMinigame?.ShowResults == true )
		{
			var newlyLocked = new List<Player>();

			foreach ( var player in players )
			{
				var wasLocked = player.PlayerData.WareLocked;
				player.PlayerData.LockWareResult();

				if ( !wasLocked )
					newlyLocked.Add( player );
			}

			SendResults( players, newlyLocked );
		}

		RestorePlayersAfterWare( players );

		WaresPlayed++;
		Phase = WareRoundPhase.Results;
		_phaseTimeRemaining = 2.5f;
	}

	private void RestorePlayersAfterWare( IReadOnlyList<Player> players )
	{
		foreach ( var player in players )
		{
			if ( !player.IsValid() ) continue;

			player.RestoreWareDeath();
			RemoveWeapons( player );
		}
	}

	private void SendResults( IReadOnlyList<Player> players, IReadOnlyList<Player> newlyLocked )
	{
		if ( players.Count == 0 ) return;

		var firstResult = players[0].PlayerData.HasAchievedWare == true;
		var everyoneSame = players.Count > 1 && players.All( x => (x.PlayerData.HasAchievedWare == true) == firstResult );

		if ( everyoneSame && !_everyoneStatusSent )
		{
			SendEveryoneStatus( firstResult );
		}
		else if ( !everyoneSame )
		{
			foreach ( var player in players )
				SendLocalStatus( player, player.PlayerData.HasAchievedWare == true );
		}

		foreach ( var player in players )
		{
			var achieved = player.PlayerData.HasAchievedWare == true;
			ReceiveWareEndSound( player.SteamId, achieved );
		}
	}

	private bool TryGetLockedEveryoneResult( out bool achieved )
	{
		achieved = false;
		var players = Players.ToArray();
		if ( players.Length < 2 ) return false;
		if ( players.Any( x => !x.PlayerData.WareLocked ) ) return false;

		achieved = players[0].PlayerData.HasAchievedWare == true;
		var expected = achieved;
		return players.All( x => (x.PlayerData.HasAchievedWare == true) == expected );
	}

	private void SendEveryoneStatus( bool achieved )
	{
		if ( _everyoneStatusSent ) return;

		_everyoneStatusSent = true;
		ReceiveWareEveryoneStatus( achieved );
	}

	private void SendLocalStatus( Player player, bool achieved )
	{
		if ( !player.IsValid() ) return;
		if ( !_playersSentStatus.Add( player.SteamId ) ) return;

		ReceiveWareLocalStatus( player.SteamId, achieved );
	}

	private static void SendPeerStatusSound( Player player, bool achieved )
	{
		if ( !player.IsValid() ) return;

		ReceiveWarePeerStatusSound( player.SteamId, player.WorldPosition, achieved );
	}

	private void WaitForPlayers()
	{
		_introPlayed = false;
		SetWaiting( "Waiting for players" );
	}

	private static bool HasConnectedPlayers()
	{
		return PlayerData.All.Any( x => x.IsValid() && x.Connection is not null );
	}

	private void SetWaiting( string instruction )
	{
		CleanupTemporaryObjects();
		_currentMinigame = null;
		Phase = WareRoundPhase.Waiting;
		_phaseTimeRemaining = 2f;
		SetInstruction( instruction );
	}

	public void SetInstruction( string instruction )
	{
		CurrentInstruction = instruction;
		ReceiveInstruction( instruction );
	}

	private static void ClearStatus()
	{
		ReceiveClearStatus();
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
		if ( player.PlayerData.WareLocked ) return;

		player.PlayerData.SetWareAchieved( achieved );
		player.PlayerData.LockWareResult();
		SendPeerStatusSound( player, achieved );

		if ( TryGetLockedEveryoneResult( out var everyoneAchieved ) )
			SendEveryoneStatus( everyoneAchieved );
		else
			SendLocalStatus( player, achieved );
	}

	public void FailAndSimulateDeath( Player player, Vector3 impulse = default )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() || !player.PlayerData.IsValid() ) return;
		if ( player.PlayerData.WareLocked ) return;

		LockResult( player, false );
		RemoveWeapons( player );

		var deathTransform = player.WorldTransform;
		player.SimulateWareDeath( deathTransform, impulse );
		player.MoveToWareSpawn( TakeDeathSpawn() );
	}

	private Transform TakeDeathSpawn()
	{
		if ( _deathSpawnPool.Count == 0 )
			_deathSpawnPool.AddRange( Environment.GetSpawnTransforms( Math.Max( 1, Players.Count() ) ) );

		var spawn = _deathSpawnPool[0];
		_deathSpawnPool.RemoveAt( 0 );
		return spawn;
	}

	public void RegisterTemporaryObject( GameObject go )
	{
		if ( !Networking.IsHost ) return;
		if ( !go.IsValid() ) return;

		go.Tags.Add( "ware" );
		go.Tags.Add( "removable" );

		if ( !_temporaryObjects.Contains( go ) )
			_temporaryObjects.Add( go );
	}

	public void SendDone( Player player )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() || !player.PlayerData.IsValid() ) return;

		ReceiveWareDone( player.SteamId );
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

	public void RemoveWeapons( Player player )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() ) return;

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		foreach ( var weapon in inventory.Weapons.ToArray() )
			inventory.Remove( weapon );
	}

	public GameObject CreateTemporaryObject( string name, Vector3 position, bool networked = false )
	{
		var go = new GameObject( true, name )
		{
			WorldPosition = position,
			Flags = GameObjectFlags.NotSaved,
			NetworkMode = networked ? NetworkMode.Object : NetworkMode.Never
		};
		RegisterTemporaryObject( go );
		return go;
	}

	public void CleanupTemporaryObjects()
	{
		foreach ( var go in Scene.GetAllObjects( true ).Where( x => x.Tags.Contains( "ware" ) && x.Tags.Contains( "removable" ) ).ToArray() )
		{
			if ( go.IsValid() )
				go.Destroy();
		}

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
		if ( _currentMinigame?.ShowResults != true ) return;
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
		Current?.StartNextWare( Current.Players.ToArray() );
	}
}

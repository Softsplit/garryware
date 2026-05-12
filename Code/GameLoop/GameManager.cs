public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup, IScenePhysicsEvents
{
	public GameManager( Scene scene ) : base( scene )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new Sandbox.Network.LobbyConfig() { Privacy = Sandbox.Network.LobbyPrivacy.Public, MaxPlayers = 32, Name = "GarryWare", DestroyWhenHostLeaves = true } );
		}
	}

	void Component.INetworkListener.OnActive( Connection channel )
	{
		channel.CanSpawnObjects = false;

		var playerData = CreatePlayerInfo( channel );
		SpawnPlayer( playerData );

		Log.Info( $"{channel.DisplayName} has joined the game" );
	}

	/// <summary>
	/// Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		var pd = PlayerData.For( channel );
		if ( pd is not null )
		{
			pd.GameObject.Destroy();
		}

		if ( _kickedPlayers.Remove( channel.Id ) ) return;

		Log.Info( $"{channel.DisplayName} has left the game" );
	}

	private PlayerData CreatePlayerInfo( Connection channel )
	{
		var existingPlayerInfo = PlayerData.For( channel );
		if ( existingPlayerInfo.IsValid() )
			return existingPlayerInfo;

		var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
		var data = go.AddComponent<PlayerData>();
		data.SteamId = (long)channel.SteamId;
		data.PlayerId = channel.Id;
		data.DisplayName = channel.DisplayName;

		go.NetworkSpawn( null );
		go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

		return data;
	}

	public void SpawnPlayer( Connection connection ) => SpawnPlayer( PlayerData.For( connection ) );

	public void SpawnPlayer( PlayerData playerData )
	{
		Assert.NotNull( playerData, "PlayerData is null" );
		Assert.True( Networking.IsHost, $"Client tried to SpawnPlayer: {playerData.DisplayName}" );

		// does this connection already have a player?
		if ( Scene.GetAll<Player>().Any( x => x.Network.Owner?.Id == playerData.PlayerId ) )
			return;

		// Find a spawn location for this player
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Fire pre-respawn event — listeners can modify spawn location
		var respawnEvent = new PlayerRespawnEvent { PlayerData = playerData, SpawnLocation = startLocation };
		Global.IPlayerEvents.Post( x => x.OnPlayerRespawning( respawnEvent ) );
		startLocation = respawnEvent.SpawnLocation;

		// Spawn this object and make the client the owner
		var playerGo = GameObject.Clone( "/prefabs/engine/player.prefab", new CloneConfig { Name = playerData.DisplayName, StartEnabled = false, Transform = startLocation } );

		var player = playerGo.Components.Get<Player>( true );
		player.PlayerData = playerData;

		var owner = Connection.Find( playerData.PlayerId );
		playerGo.NetworkSpawn( owner );

		Local.IPlayerEvents.PostToGameObject( player.GameObject, x => x.OnSpawned() );
		Global.IPlayerEvents.Post( x => x.OnPlayerSpawned( player ) );
	}

	public void SpawnPlayerDelayed( PlayerData playerData )
	{
		GameTask.RunInThreadAsync( async () =>
		{
			await Task.Delay( 4000 );
			await GameTask.MainThread();
			if ( Current is not null )
				Current.SpawnPlayer( playerData );
		} );
	}

	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{
		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

		if ( spawnPoints.Length == 0 )
		{
			return Transform.Zero;
		}

		return Random.Shared.FromArray( spawnPoints ).Transform.World;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private static void SendMessage( string msg )
	{
		Log.Info( msg );
	}

	/// <summary>
	/// Called on the host when a played is killed
	/// </summary>
	public void OnDeath( Player player, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );

		Assert.True( player.IsValid(), "Player invalid" );
		Assert.True( player.PlayerData.IsValid(), $"{player.GameObject.Name}'s PlayerData invalid" );

		var source = dmg.Attacker?.GetComponentInParent<IKillSource>( true );
		if ( source == null ) return;

		var isSuicide = source is Player p && p == player;

		if ( !isSuicide )
			source.OnKill( player.GameObject );

		// Fire kill event on the killer if they're a player
		if ( !isSuicide && source is Player killer )
		{
			var killEvent = new PlayerKillEvent { Player = killer, Victim = player.GameObject, DamageInfo = dmg };
			Local.IPlayerEvents.PostToGameObject( killer.GameObject, x => x.OnKill( killEvent ) );
			Global.IPlayerEvents.Post( x => x.OnPlayerKill( killEvent ) );
		}

		player.PlayerData.Deaths++;

		var weapon = dmg.Weapon;
		var damageTags = dmg.Tags.ToString() + ( isSuicide ? " suicide" : "" );
		var attackerTags = isSuicide ? "" : source.Tags;
		var attackerName = isSuicide ? null : source.DisplayName;
		var attackerSteamId = isSuicide ? 0L : source.SteamId;
		if ( string.IsNullOrEmpty( attackerName ) )
		{
			SendMessage( $"{player.DisplayName} died (tags: {dmg.Tags})" );
		}
		else if ( weapon.IsValid() )
		{
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} with {weapon.Name} (tags: {dmg.Tags})" );
		}
		else
		{
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} (tags: {dmg.Tags})" );
		}
	}

	void IScenePhysicsEvents.OnOutOfBounds( Rigidbody body )
	{
		body.DestroyGameObject();
	}

}

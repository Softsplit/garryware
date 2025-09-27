public enum RoundState
{
	Waiting,
	Preparation,
	Active,
	Ended
}

public enum MinigameType
{
	Cooperative,
	Competitive
}

/// <summary>
/// Round management as part of GameManager
/// </summary>
public sealed partial class GameManager
{
	[Sync] public RoundState CurrentRoundState { get; private set; } = RoundState.Waiting;
	[Sync] public int CurrentRound { get; private set; } = 0;
	[Sync] public string CurrentInstructions { get; private set; } = "";
	[Sync] public float RoundTimeRemaining { get; private set; }
	
	private IMinigame currentMinigame;
	private float stateTimer;
	private List<Type> availableMinigames = [];
	private List<Type> recentMinigames = [];
	
	private void InitializeRounds()
	{
		LoadMinigames();
		SetRoundState( RoundState.Waiting );
	}
	
	private void UpdateRounds()
	{
		if ( availableMinigames.Count == 0 )
		{
			InitializeRounds();
		}
		
		stateTimer -= Time.Delta;
		RoundTimeRemaining = MathF.Max( 0, stateTimer );
		
		switch ( CurrentRoundState )
		{
			case RoundState.Waiting:
				UpdateWaiting();
				break;
			case RoundState.Preparation:
				UpdatePreparation();
				break;
			case RoundState.Active:
				UpdateActive();
				break;
			case RoundState.Ended:
				UpdateEnded();
				break;
		}
	}
	
	private void UpdateWaiting()
	{
		var alivePlayers = GetAlivePlayers();
		if ( alivePlayers.Count >= GameSettings.MinPlayersToStart )
		{
			StartNewRound();
		}
		else
		{
			CurrentInstructions = $"Waiting for players ({alivePlayers.Count}/{GameSettings.MinPlayersToStart})";
		}
	}
	
	private void UpdatePreparation()
	{
		if ( stateTimer <= 0 )
		{
			StartRoundAction();
		}
		else
		{
			CurrentInstructions = $"Get ready! {currentMinigame?.Name ?? "Unknown"} - {currentMinigame?.Instructions ?? ""}";
		}
	}
	
	private void UpdateActive()
	{
		currentMinigame?.Update();
		
		if ( stateTimer <= 0 )
		{
			EndRound();
		}
	}
	
	private void UpdateEnded()
	{
		if ( stateTimer <= 0 )
		{
			StartNewRound();
		}
	}
	
	private void StartNewRound()
	{
		CurrentRound++;
		
		// Reset player round completion status
		foreach ( var playerData in PlayerData.All )
		{
			playerData.HasCompletedCurrentRound = false;
			playerData.CurrentRoundScore = 0;
		}
		
		// Select a random minigame
		var minigameType = SelectRandomMinigame();
		currentMinigame = CreateMinigame( minigameType );
		
		var context = new MinigameContext
		{
			Players = GetAlivePlayers(),
			SpawnPoints = Scene.GetAllComponents<SpawnPoint>().ToList(),
			Scene = Scene
		};
		
		currentMinigame.Initialize( context );
		
		SetRoundState( RoundState.Preparation );
		stateTimer = (float)currentMinigame.PreparationTime.TotalSeconds;
	}
	
	private void StartRoundAction()
	{
		// Spawn players who aren't spawned yet
		SpawnUnspawnedPlayers();
		
		currentMinigame.Start();
		SetRoundState( RoundState.Active );
		stateTimer = (float)currentMinigame.Duration.TotalSeconds;
		CurrentInstructions = currentMinigame.Instructions;
	}
	
	private void EndRound()
	{
		currentMinigame?.End();
		
		// Calculate winners
		var achievers = currentMinigame?.GetAchievers() ?? new List<Player>();
		var allPlayers = GetAlivePlayers();
		
		foreach ( var player in allPlayers )
		{
			var playerData = player.PlayerData;
			if ( achievers.Contains( player ) )
			{
				playerData.RoundWins++;
				playerData.HasCompletedCurrentRound = true;
				playerData.CurrentRoundScore = 100;
			}
			else
			{
				playerData.RoundLosses++;
				playerData.CurrentRoundScore = 0;
			}
		}
		
		// Show results
		var winnerCount = achievers.Count;
		var totalCount = allPlayers.Count;
		
		if ( currentMinigame.Type == MinigameType.Cooperative )
		{
			CurrentInstructions = winnerCount == totalCount ? "Everyone succeeded!" : 
			                     winnerCount == 0 ? "Nobody succeeded!" :
			                     $"{winnerCount}/{totalCount} succeeded!";
		}
		else
		{
			CurrentInstructions = winnerCount == 1 ? $"{achievers.First().DisplayName} won!" :
			                     winnerCount == 0 ? "Nobody won!" :
			                     $"{winnerCount} players won!";
		}
		
		currentMinigame?.Dispose();
		currentMinigame = null;
		
		SetRoundState( RoundState.Ended );
		stateTimer = GameSettings.RoundResultsTime;
	}
	
	private void SetRoundState( RoundState newState )
	{
		CurrentRoundState = newState;
	}
	
	private Type SelectRandomMinigame()
	{
		// Get minigames that haven't been played recently
		var candidates = availableMinigames.Except( recentMinigames ).ToList();
		
		if ( candidates.Count == 0 )
		{
			recentMinigames.Clear();
			candidates = availableMinigames.ToList();
		}
		
		var selected = Random.Shared.FromList( candidates );
		recentMinigames.Add( selected );
		
		// Keep recent list reasonable size
		if ( recentMinigames.Count > availableMinigames.Count / 2 )
		{
			recentMinigames.RemoveAt( 0 );
		}
		
		return selected;
	}
	
	private IMinigame CreateMinigame( Type minigameType )
	{
		return TypeLibrary.Create<IMinigame>( minigameType );
	}
	
	private void LoadMinigames()
	{
		availableMinigames.Clear();
		
		var minigameTypes = TypeLibrary.GetTypes<IMinigame>()
			.Where( t => !t.IsAbstract && !t.IsInterface && t.TargetType != typeof(Minigame) )
			.Select( t => t.TargetType )
			.ToList();
			
		availableMinigames.AddRange( minigameTypes );
		
		Log.Info( $"Loaded {availableMinigames.Count} minigames: {string.Join( ", ", availableMinigames.Select( t => t.Name ) )}" );
	}
	
	private List<Player> GetAlivePlayers()
	{
		return Scene.GetAllComponents<Player>()
			.Where( p => p.IsValid && p.Health > 0 )
			.ToList();
	}
	
	private void SpawnUnspawnedPlayers()
	{
		var existingPlayers = Scene.GetAllComponents<Player>().Select( p => p.PlayerData?.PlayerId ).ToHashSet();
		
		foreach ( var playerData in PlayerData.All )
		{
			if ( playerData.Connection?.IsActive == true && !existingPlayers.Contains( playerData.PlayerId ) )
			{
				SpawnPlayer( playerData );
			}
		}
	}
}
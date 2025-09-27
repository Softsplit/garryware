/// <summary>
/// Clean interface for minigames with proper lifecycle management
/// </summary>
public interface IMinigame : IDisposable
{
	string Name { get; }
	string Instructions { get; }
	MinigameType Type { get; }
	TimeSpan Duration { get; }
	TimeSpan PreparationTime { get; }

	/// <summary>
	/// Initialize the minigame with the given context
	/// </summary>
	void Initialize( MinigameContext context );
	
	/// <summary>
	/// Start the active gameplay phase
	/// </summary>
	void Start();
	
	/// <summary>
	/// Update the minigame (called each frame during active phase)
	/// </summary>
	void Update();
	
	/// <summary>
	/// End the minigame and evaluate results
	/// </summary>
	void End();
	
	/// <summary>
	/// Check if a specific player has achieved the objective
	/// </summary>
	bool HasPlayerAchieved( Player player );
	
	/// <summary>
	/// Get all players who have achieved the objective
	/// </summary>
	IReadOnlyList<Player> GetAchievers();
}

/// <summary>
/// Context provided to minigames for initialization
/// </summary>
public readonly record struct MinigameContext(
	Scene Scene,
	IReadOnlyList<Player> Players,
	IReadOnlyList<SpawnPoint> SpawnPoints
);
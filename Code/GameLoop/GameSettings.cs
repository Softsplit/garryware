/// <summary>
/// The game settings for this server
/// </summary>
public static class GameSettings
{
	/// <summary>
	/// Give you everything
	/// </summary>
	[ConVar( "garryware.cheatmode", ConVarFlags.Replicated | ConVarFlags.Saved | ConVarFlags.GameSetting | ConVarFlags.Cheat ), Group( "Cheats" ), Title( "Spawn with all weapons" )]
	public static bool CheatMode { get; set; } = false;

	/// <summary>
	/// Show debug information
	/// </summary>
	[ConVar( "garryware.debug", ConVarFlags.Replicated ), Group( "Cheats" )]
	public static int Debug { get; set; } = 0;

	/// <summary>
	/// Maximum decals that can exist. Remove old ones when new ones are created.
	/// </summary>
	[ConVar( "garryware.maxdecals", ConVarFlags.Replicated | ConVarFlags.GameSetting )]
	[Range( 64, 1024f ), Step( 1f ), Group( "Weapons" )]
	public static int MaxDecals { get; set; } = 512;

	/// <summary>
	/// Minimum players required to start a round
	/// </summary>
	[ConVar( "garryware.minplayers", ConVarFlags.Replicated | ConVarFlags.GameSetting ), Group( "Round System" ), Range( 1, 32 ), Step( 1 )]
	public static int MinPlayersToStart { get; set; } = 2;

	/// <summary>
	/// Default preparation time for minigames in seconds
	/// </summary>
	[ConVar( "garryware.preptime", ConVarFlags.Replicated | ConVarFlags.GameSetting ), Group( "Round System" ), Range( 1, 10 ), Step( 0.5f )]
	public static float DefaultPreparationTime { get; set; } = 3f;

	/// <summary>
	/// Default duration for minigames in seconds
	/// </summary>
	[ConVar( "garryware.roundtime", ConVarFlags.Replicated | ConVarFlags.GameSetting ), Group( "Round System" ), Range( 3, 60 ), Step( 1f )]
	public static float DefaultRoundDuration { get; set; } = 10f;

	/// <summary>
	/// How long to show round results in seconds
	/// </summary>
	[ConVar( "garryware.resultstime", ConVarFlags.Replicated | ConVarFlags.GameSetting ), Group( "Round System" ), Range( 1, 10 ), Step( 0.5f )]
	public static float RoundResultsTime { get; set; } = 3f;
}

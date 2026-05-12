/// <summary>
/// The local user's GarryWare preferences.
/// </summary>
public static class GamePreferences
{
	/// <summary>
	/// Enables automatic switching to better weapons when granted.
	/// </summary>
	[ConVar( "ware.autoswitch", ConVarFlags.UserInfo | ConVarFlags.Saved )]
	public static bool AutoSwitch { get; set; } = true;

	/// <summary>
	/// Enables fast switching between inventory weapons
	/// </summary>
	[ConVar( "ware.fastswitch", ConVarFlags.Saved )]
	public static bool FastSwitch { get; set; } = false;

	/// <summary>
	/// Intensity of your camera's screenshake
	/// </summary>
	[ConVar( "ware.viewbob", ConVarFlags.Saved )]
	[Group( "Camera" )]
	public static bool ViewBobbing { get; set; } = true;

	/// <summary>
	/// Intensity of your camera's screenshake
	/// </summary>
	[ConVar( "ware.screenshake", ConVarFlags.Saved )]
	[Range( 0.1f, 2f ), Step( 0.1f ), Group( "Camera" )]
	public static float Screenshake { get; set; } = 0.3f;
}

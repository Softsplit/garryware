public sealed partial class WareRoundSystem
{
	private static void StartWareAudio( bool intro, float totalDuration )
	{
		ReceiveWareStarted( intro, GetAmbientLoop( totalDuration ) );
	}

	private static void StartActionAudio( float duration, WareMinigame ware )
	{
		if ( ware is null || !ware.UseCountdown ) return;

		ReceiveWareActionStarted( duration, (int)ware.Announcer );
	}

	private static void PlayPhaseAudio( float duration )
	{
		ReceiveWarePhase( GetAmbientLoop( duration ) );
	}

	private static int GetAmbientLoop( float duration )
	{
		return duration >= 10f ? 2 : 1;
	}

	[Rpc.Broadcast]
	private static void ReceiveInstruction( string instruction )
	{
		if ( Current is not null )
			Current.CurrentInstruction = instruction;

		WareHudState.ShowInstruction( instruction );
	}

	[Rpc.Broadcast]
	private static void ReceiveWareStarted( bool intro, int ambientLoop )
	{
		WareSounds.StartWare( intro, ambientLoop );
	}

	[Rpc.Broadcast]
	private static void ReceiveWareActionStarted( float duration, int announcer )
	{
		WareSounds.StartCountdown( duration, announcer );
	}

	[Rpc.Broadcast]
	private static void ReceiveWarePhase( int ambientLoop )
	{
		WareSounds.StartPhase( ambientLoop );
	}

	[Rpc.Broadcast]
	private static void ReceiveClearStatus()
	{
		WareHudState.ClearStatus();
	}

	[Rpc.Broadcast]
	private static void ReceiveWareLocalStatus( long steamId, bool achieved )
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( !localPlayer.IsValid() || localPlayer.SteamId != steamId ) return;

		WareSounds.Play( achieved ? WareSounds.LocalWin : WareSounds.LocalLose );
		WareHudState.ShowStatus( achieved ? "Success!" : "Failure!", achieved );
	}

	[Rpc.Broadcast]
	private static void ReceiveWareEveryoneStatus( bool achieved )
	{
		WareHudState.ShowStatus( achieved ? "Everyone won!" : "Everyone failed!", achieved );
		WareSounds.Play( achieved ? WareSounds.EveryoneWon : WareSounds.EveryoneLost );
	}

	[Rpc.Broadcast]
	private static void ReceiveWarePeerStatusSound( long steamId, Vector3 position, bool achieved )
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( localPlayer.IsValid() && localPlayer.SteamId == steamId ) return;

		WareSounds.PlayPeerStatus( position, achieved );
	}

	[Rpc.Broadcast]
	private static void ReceiveWareDone( long steamId )
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( !localPlayer.IsValid() || localPlayer.SteamId != steamId ) return;

		WareSounds.Play( WareSounds.LocalWin );
		WareHudState.ShowStatus( "Done!", true, 1f );
	}

	[Rpc.Broadcast]
	private static void ReceiveWareEndSound( long steamId, bool achieved )
	{
		var localPlayer = Player.FindLocalPlayer();
		if ( !localPlayer.IsValid() || localPlayer.SteamId != steamId ) return;

		WareSounds.PlayEnd( achieved );
	}
}
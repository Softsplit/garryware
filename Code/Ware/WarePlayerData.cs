public sealed partial class PlayerData
{
	[Sync] public int WareScore { get; set; }
	[Sync] public int WareStreak { get; set; }
	[Sync] public int WareBestStreak { get; set; }
	[Sync] public WarePlayerResult WareResult { get; set; } = WarePlayerResult.Unknown;
	[Sync] public bool WareLocked { get; set; }

	public bool? HasAchievedWare => WareResult switch
	{
		WarePlayerResult.Passed => true,
		WarePlayerResult.Failed => false,
		_ => null
	};

	public void ResetWareState( bool? initialResult )
	{
		Assert.True( Networking.IsHost, "PlayerData.ResetWareState is host-only" );

		WareLocked = false;
		WareResult = initialResult switch
		{
			true => WarePlayerResult.Passed,
			false => WarePlayerResult.Failed,
			_ => WarePlayerResult.Unknown
		};
	}

	public bool SetWareAchieved( bool achieved )
	{
		if ( WareLocked ) return false;

		WareResult = achieved ? WarePlayerResult.Passed : WarePlayerResult.Failed;
		return true;
	}

	public void LockWareResult()
	{
		Assert.True( Networking.IsHost, "PlayerData.LockWareResult is host-only" );

		if ( WareLocked ) return;

		if ( WareResult == WarePlayerResult.Unknown )
			WareResult = WarePlayerResult.Failed;

		WareLocked = true;

		if ( WareResult == WarePlayerResult.Passed )
		{
			WareScore++;
			WareStreak++;
			WareBestStreak = Math.Max( WareBestStreak, WareStreak );
			Kills++;
		}
		else
		{
			WareStreak = 0;
			Deaths++;
		}
	}
}
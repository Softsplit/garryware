namespace WareMinigames;

public sealed class DontMove : WareMinigame
{
	public override string Name => "dontmove";
	public override string Title => "Don't move!";
	public override float Windup => 3.5f;
	public override float Duration => 2f;
	public override bool? InitialPlayerResult => true;

	public override void Initialize()
	{
		SetInstruction( "Don't move!" );
	}

	public override void StartAction()
	{
		foreach ( var player in Players )
			GiveWeapon( player, WareWeaponPaths.Crowbar );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			if ( player.PlayerData.WareLocked ) continue;

			var velocity = PlayerVelocity( player );
			if ( velocity.Length > 16f )
				FailAndSimulateDeath( player, velocity * 1000f );
		}
	}
}

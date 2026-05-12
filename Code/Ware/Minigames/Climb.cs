namespace WareMinigames;

public sealed class Climb : WareMinigame
{
	public override string Name => "climb";
	public override string Title => "Get on a box!";
	public override float Windup => 1.5f;
	public override float Duration => 3f;

	public override void Initialize()
	{
		SetInstruction( "Get on a box!" );
	}

	public override void StartAction()
	{
		foreach ( var player in Players )
			GiveWeapon( player, WareWeaponPaths.Crowbar );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
			SetAchieved( player, IsOnAnyLocation( player, "oncrate" ) );
	}
}

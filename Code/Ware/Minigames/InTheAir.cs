namespace WareMinigames;

public sealed class InTheAir : WareMinigame
{
	private bool _trap;
	private float _zCap;

	public override string Ident => "in_the_air";
	public override string Title => "When clock reaches zero...";
	public override string Room => "empty";
	public override float Windup => 2f;
	public override float Duration => 3.5f;

	public override void Initialize()
	{
		_trap = Random.Shared.Float( 0f, 1f ) <= 0.35f;
		_zCap = Environment.GetLowestZ( "dark_ground" ) + 96f;
		SetInstruction( "When clock reaches zero..." );
	}

	public override void StartAction()
	{
		SetInstruction( _trap ? "Stay on the ground!" : "Be high in the air!" );

		foreach ( var player in Players )
			GiveWeapon( player, "weapons/rpg/rpg.prefab" );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
			SetAchieved( player, _trap ? player.WorldPosition.z < _zCap : player.WorldPosition.z > _zCap );
	}
}
namespace WareMinigames;

public sealed class InTheAir : WareMinigame
{
	private bool _trap;
	private float _duration;
	private float _zCap;

	public override string Name => "intheair";
	public override string Title => "When clock reaches zero...";
	public override string Room => "empty";
	public override float Windup => 2f;
	public override float Duration => _duration;

	public override void Initialize()
	{
		_trap = Random.Shared.Int( 0, 10 ) <= 3;
		_duration = _trap ? Random.Shared.Float( 1.3f, 2.5f ) : Random.Shared.Float( 1.3f, 5.0f );
		_zCap = Environment.GetLowestZ( "dark_ground" ) + 96f;
		SetInstruction( "When clock reaches zero..." );
	}

	public override void StartAction()
	{
		SetInstruction( _trap ? "Stay on the ground!" : "Be high in the air!" );

		foreach ( var player in Players )
			GiveWeapon( player, WareWeaponPaths.RocketJumpLimited );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
			SetAchieved( player, _trap ? player.WorldPosition.z < _zCap : player.WorldPosition.z > _zCap );
	}
}
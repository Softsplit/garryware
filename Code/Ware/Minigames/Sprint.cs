namespace WareMinigames;

public sealed class Sprint : WareMinigame
{
	private const float MaxSpeed = 320f;

	public override string Name => "sprint";
	public override string Title => "Don't stop sprinting!";
	public override float Windup => 2.5f;
	public override float Duration => 5f;
	public override bool? InitialPlayerResult => true;

	public override void Initialize()
	{
		SetInstruction( Title );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			if ( player.PlayerData.WareLocked ) continue;
			if ( MovementSpeed( player ) < MaxSpeed * 0.8f )
				FailAndSimulateDeath( player );
		}
	}
}

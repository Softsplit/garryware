namespace WareMinigames;

public sealed class Sprint : WareMinigame
{
	public override string Ident => "sprint";
	public override string Title => "Keep moving!";
	public override float Windup => 1.5f;
	public override float Duration => 4f;

	public override void Initialize()
	{
		SetInstruction( Title );
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			var speed = player.Controller?.Velocity.WithZ( 0 ).Length ?? 0f;
			SetAchieved( player, speed > 160f );
		}
	}
}
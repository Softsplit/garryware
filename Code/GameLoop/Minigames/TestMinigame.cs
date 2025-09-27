/// <summary>
/// A simple test minigame to demonstrate the round system
/// </summary>
public class TestMinigame : Minigame
{
	public override string Name => "Test Game";
	public override string Instructions => "This is a test minigame. Everyone wins!";
	public override MinigameType Type => MinigameType.Cooperative;
	public override TimeSpan Duration => TimeSpan.FromSeconds( 3 );
	public override TimeSpan PreparationTime => TimeSpan.FromSeconds( 2 );
	
	protected override void OnStart()
	{
		Log.Info( "Test minigame started!" );
	}
	
	protected override void OnEnd()
	{
		Log.Info( "Test minigame ended!" );
	}
	
	protected override bool CheckPlayerAchievement( Player player )
	{
		// Everyone wins in this test game
		return true;
	}
}
namespace WareMinigames;

public sealed class JumpBox : WareMinigame
{
	private readonly Dictionary<Player, GameObject> _lastBlockByPlayer = new();
	private readonly Dictionary<Player, int> _swapCountByPlayer = new();
	private float _groundZ;

	public override string Name => "jumpbox";
	public override string Title => "Sprint-jump from box to box twice!";
	public override float Windup => 0f;
	public override float Duration => 7f;

	public override void Initialize()
	{
		_groundZ = Environment.GetLowestZ( "cross" );
		SetInstruction( "Sprint-jump from box to box twice!" );
	}

	public override void StartAction()
	{
		_lastBlockByPlayer.Clear();
		_swapCountByPlayer.Clear();

		foreach ( var player in Players )
		{
			_lastBlockByPlayer[player] = null;
			_swapCountByPlayer[player] = -1;
		}
	}

	public override void UpdateAction()
	{
		foreach ( var player in Players )
		{
			if ( player.WorldPosition.z - _groundZ <= 5f )
			{
				_lastBlockByPlayer[player] = null;
				_swapCountByPlayer[player] = -1;
				continue;
			}

			var block = GetBlockUnderPlayer( player );
			if ( !block.IsValid() || _lastBlockByPlayer.GetValueOrDefault( player ) == block )
				continue;

			_lastBlockByPlayer[player] = block;
			_swapCountByPlayer[player] = _swapCountByPlayer.GetValueOrDefault( player, -1 ) + 1;

			if ( _swapCountByPlayer[player] >= 2 )
				LockResult( player, true );
		}
	}

	private GameObject GetBlockUnderPlayer( Player player )
	{
		foreach ( var block in Environment.GetLocations( "oncrate" ) )
		{
			if ( IsInsideLocationBox( player.WorldPosition, block.WorldPosition ) )
			{
				return block;
			}
		}

		return null;
	}
}

public sealed class WareMinigameRegistry
{
	private readonly List<Func<WareMinigame>> _minigameFactories = new();
	private readonly Queue<Func<WareMinigame>> _minigameDeck = new();

	public WareMinigameRegistry()
	{
		Register<WareMinigames.Climb>();
		Register<WareMinigames.DontMove>();
		Register<WareMinigames.InTheAir>();
		Register<WareMinigames.JumpBox>();
		Register<WareMinigames.OffTheCenter>();
		Register<WareMinigames.OnTheCenter>();
		Register<WareMinigames.Sprint>();
	}

	public WareMinigame CreateIntro( WareRoundSystem system )
	{
		return Bind( new WareMinigames.Intro(), system );
	}

	public void Register<T>() where T : WareMinigame, new()
	{
		_minigameFactories.Add( () => new T() );
	}

	public WareMinigame CreateNext( WareRoundSystem system, IReadOnlyList<Player> players )
	{
		if ( _minigameFactories.Count == 0 ) return null;

		for ( var attempts = 0; attempts < _minigameFactories.Count * 2; attempts++ )
		{
			if ( _minigameDeck.Count == 0 )
				RebuildDeck();

			var minigame = Bind( _minigameDeck.Dequeue().Invoke(), system );

			if ( CanPlay( system, minigame, players ) )
				return minigame;
		}

		return _minigameFactories
			.Select( factory => Bind( factory(), system ) )
			.FirstOrDefault( minigame => CanPlay( system, minigame, players ) );
	}

	private void RebuildDeck()
	{
		var pool = new List<Func<WareMinigame>>();

		foreach ( var factory in _minigameFactories )
		{
			var minigame = factory();
			for ( var i = 0; i < Math.Max( 1, minigame.OccurrencesPerCycle ); i++ )
				pool.Add( factory );
		}

		while ( pool.Count > 0 )
		{
			var index = Random.Shared.Int( 0, pool.Count - 1 );
			_minigameDeck.Enqueue( pool[index] );
			pool.RemoveAt( index );
		}
	}

	private static WareMinigame Bind( WareMinigame minigame, WareRoundSystem system )
	{
		minigame.Bind( system );
		return minigame;
	}

	private static bool CanPlay( WareRoundSystem system, WareMinigame minigame, IReadOnlyList<Player> players )
	{
		return system.Environment.HasEnvironment( minigame.Room ) && minigame.CanPlay( players );
	}
}

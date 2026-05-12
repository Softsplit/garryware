public sealed class WareMinigameRegistry
{
	private readonly List<Func<WareMinigame>> _factories = new();
	private readonly Queue<Func<WareMinigame>> _sequence = new();

	public WareMinigameRegistry()
	{
		Register<WareMinigames.InTheAir>();
		Register<WareMinigames.Sprint>();
	}

	public void Register<T>() where T : WareMinigame, new()
	{
		_factories.Add( () => new T() );
	}

	public WareMinigame CreateNext( WareRoundSystem system, IReadOnlyList<Player> players )
	{
		if ( _factories.Count == 0 ) return null;

		for ( var attempts = 0; attempts < _factories.Count * 2; attempts++ )
		{
			if ( _sequence.Count == 0 )
				RebuildSequence();

			var minigame = _sequence.Dequeue().Invoke();
			minigame.Bind( system );

			if ( minigame.CanPlay( players ) )
				return minigame;
		}

		var fallback = _factories[0].Invoke();
		fallback.Bind( system );
		return fallback;
	}

	private void RebuildSequence()
	{
		var pool = new List<Func<WareMinigame>>();

		foreach ( var factory in _factories )
		{
			var minigame = factory();
			for ( var i = 0; i < Math.Max( 1, minigame.OccurrencesPerCycle ); i++ )
				pool.Add( factory );
		}

		while ( pool.Count > 0 )
		{
			var index = Random.Shared.Int( 0, pool.Count - 1 );
			_sequence.Enqueue( pool[index] );
			pool.RemoveAt( index );
		}
	}
}
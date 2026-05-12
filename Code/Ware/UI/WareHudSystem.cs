public sealed class WareHudSystem : GameObjectSystem<WareHudSystem>, ISceneLoadingEvents
{
	private GameObject _hudRoot;
	private bool _clientStateActive = true;

	public WareHudSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 1000, Tick, "WareHud" );
	}

	private void Tick()
	{
		if ( Application.IsEditor && !Game.IsPlaying )
		{
			if ( _clientStateActive || WareSounds.HasActiveSounds )
				StopClientState();

			_clientStateActive = false;
			return;
		}

		_clientStateActive = true;
		WareSounds.SpeedPercent = MathF.Max( 1f, Scene.TimeScale * 100f );
		WareSounds.Update();
		EnsureHud();
	}

	public override void Dispose()
	{
		StopClientState();
		base.Dispose();
	}

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		StopClientState();
	}

	async Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context )
	{
		await Task.Yield();
		EnsureHud();
	}

	private void EnsureHud()
	{
		if ( Application.IsDedicatedServer ) return;
		if ( Application.IsEditor && !Game.IsPlaying ) return;
		if ( _hudRoot.IsValid() ) return;

		_hudRoot = new GameObject( true, "Ware HUD" )
		{
			Flags = GameObjectFlags.NotSaved
		};

		var screen = _hudRoot.Components.Create<ScreenPanel>();
		screen.AutoScreenScale = true;
		screen.ScaleStrategy = ScreenPanel.AutoScale.ConsistentHeight;
		screen.ZIndex = 300;

		var hudType = FindHudType();
		if ( hudType is null )
		{
			Log.Warning( "Ware HUD panel type was not found." );
			return;
		}

		_hudRoot.Components.Create( hudType );
	}

	private static TypeDescription FindHudType()
	{
		return Game.TypeLibrary.GetTypes<PanelComponent>()
			.FirstOrDefault( type => type.TargetType.Name == "WareHud" );
	}

	private void StopClientState()
	{
		if ( _hudRoot.IsValid() )
			_hudRoot.Destroy();

		_hudRoot = null;
		WareHudState.Clear();
		WareSounds.StopAll();
	}
}
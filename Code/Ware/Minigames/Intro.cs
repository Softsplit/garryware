namespace WareMinigames;

public sealed class Intro : WareMinigame
{
	private readonly HashSet<Guid> _completedTraining = new();
	private int _showcaseSequence;

	public override string Name => "_intro";
	public override string Title => "A new GarryWare game starts!";
	public override float Windup => 4f;
	public override float Duration => 5f;
	public override bool? InitialPlayerResult => true;
	public override bool UseCountdown => false;
	public override WareAnnouncer Announcer => WareAnnouncer.None;
	public override bool ShowResults => false;

	public override void Initialize()
	{
		_completedTraining.Clear();
		SetInstruction( "A new GarryWare game starts!" );
	}

	public override void StartAction()
	{
		SetInstruction( "Rules are easy : Do what it tells you to do!" );
		_ = SpawnModelShowcase( ++_showcaseSequence );
	}

	public override float? GetNextPhaseDuration()
	{
		return ActionPhase < 3 ? 3.5f : null;
	}

	public override void PhaseSignal( int phase )
	{
		if ( phase == 2 )
			SetInstruction( "To get on a box, jump then press crouch while in the air!" );
		else if ( phase == 3 )
			SetInstruction( "Try to get on a box!" );
	}

	public override void UpdateAction()
	{
		if ( ActionPhase < 3 ) return;

		foreach ( var player in Players )
		{
			if ( !player.PlayerData.IsValid() ) continue;
			if ( _completedTraining.Contains( player.PlayerData.PlayerId ) ) continue;
			if ( !IsOnAnyLocation( player, "oncrate" ) ) continue;

			_completedTraining.Add( player.PlayerData.PlayerId );
			SendDone( player );
		}
	}

	public override void EndAction()
	{
		_showcaseSequence++;
		SetInstruction( "Game begins now! Have fun!" );
	}

	private async Task SpawnModelShowcase( int sequence )
	{
		var models = WareModelCatalog.IntroPrecacheModels;
		var delay = 4f / models.Length;

		foreach ( var model in models )
		{
			if ( sequence != _showcaseSequence ) return;
			if ( System.Phase != WareRoundPhase.Action || System.CurrentWareName != Name ) return;

			SpawnShowcaseModel( model );
			await GameTask.Delay( Math.Max( 1, (int)(delay * 1000f) ) );
		}
	}

	private void SpawnShowcaseModel( string modelPath )
	{
		var model = Model.Load( modelPath );
		if ( !model.IsValid() ) return;

		var go = CreateTemporaryObject( "ware_intro_model", GetShowcasePosition(), true );
		go.WorldRotation = Rotation.From( Random.Shared.Float( -180f, 180f ), Random.Shared.Float( -180f, 180f ), Random.Shared.Float( -180f, 180f ) );

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;

		var body = go.Components.Create<Rigidbody>();
		body.Gravity = true;
		body.MassOverride = 60f;

		if ( model.Physics is not null )
		{
			var collider = go.Components.Create<ModelCollider>();
			collider.Model = model;
		}

		go.NetworkSpawn();

		body.AngularVelocity = Vector3.Random * 8f;
		body.ApplyImpulse( Vector3.Random * Random.Shared.Float( 256f, 468f ) * body.Mass );
	}

	private Vector3 GetShowcasePosition()
	{
		var location = Environment.GetRandomLocation( "inair", "dark_inair" );
		if ( location.IsValid() ) return location.WorldPosition;

		return (Environment.Current?.Center ?? Vector3.Zero) + Vector3.Up * 256f;
	}
}

public sealed partial class Player
{
	private const float WareDeathPlayerAlpha = 64f / 255f;
	private const float WareDeathCameraDuration = 2.5f;
	private const int WareRagdollEnableDelay = 100;
	private const float WareDeathImpulseLifetime = 0.35f;
	private const float WareDeathImpulseMinSpeed = 250f;
	private const float WareDeathImpulseMaxSpeed = 1400f;
	private const float WareSuicideDamageThreshold = 1_000_000f;

	private GameObject _wareDeathRagdoll;
	private float _wareDeathStarted;
	private Angles? _wareDeathCameraAngles;
	private Vector3 _pendingWareDeathImpulse;
	private TimeSince _timeSincePendingWareDeathImpulse;
	private readonly Dictionary<SkinnedModelRenderer, Color> _wareOriginalTints = new();

	[Sync] public Vector3 WareVelocity { get; private set; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( !Controller.IsValid() ) return;

		WareVelocity = Controller.Velocity;
	}

	public void MoveToWareSpawn( Transform spawn )
	{
		if ( !Networking.IsHost ) return;

		var angles = spawn.Rotation.Angles();
		ApplyWareSpawn( spawn.Position, angles );
		ApplyWareSpawnOnOwner( spawn.Position, angles );
	}

	[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ApplyWareSpawnOnOwner( Vector3 position, Angles eyeAngles )
	{
		ApplyWareSpawn( position, eyeAngles );
	}

	private void ApplyWareSpawn( Vector3 position, Angles eyeAngles )
	{
		if ( !Controller.IsValid() )
		{
			WorldPosition = position;
			return;
		}

		position = FindWareSpawnPosition( position );
		WorldPosition = position;
		Controller.Body.WorldPosition = position;

		Controller.EyeAngles = eyeAngles;
		Controller.WishVelocity = Vector3.Zero;
		Controller.Body.Velocity = Vector3.Zero;
		Controller.Body.AngularVelocity = Vector3.Zero;
		Controller.Body.Sleeping = false;
		UpdateWareGround( position );
	}

	private Vector3 FindWareSpawnPosition( Vector3 position )
	{
		var trace = Controller.TraceBody( position + Vector3.Up * 48f, position + Vector3.Down * 128f );
		if ( trace.StartedSolid || !trace.Hit ) return position;

		return trace.EndPosition + Vector3.Up * 0.1f;
	}

	private void UpdateWareGround( Vector3 position )
	{
		var trace = Controller.TraceBody( position + Vector3.Up * 4f, position + Vector3.Down * 2f );
		if ( trace.StartedSolid || !trace.Hit )
		{
			Controller.GroundObject = null;
			Controller.GroundComponent = null;
			Controller.GroundSurface = null;
			Controller.GroundIsDynamic = false;
			Controller.GroundFriction = 0f;
			return;
		}

		Controller.GroundObject = trace.Body?.GameObject;
		Controller.GroundComponent = trace.Body?.Component;
		Controller.GroundSurface = trace.Surface;
		Controller.GroundFriction = trace.Surface.Friction;
		Controller.GroundIsDynamic = true;

		if ( trace.Component is Collider collider )
		{
			Controller.GroundFriction = collider.Friction ?? trace.Surface.Friction;
			Controller.GroundIsDynamic = collider.IsDynamic;
		}
	}

	public void ApplyWareVelocity( Vector3 velocity, float preventGrounding = 0f )
	{
		if ( !Networking.IsHost ) return;

		ApplyWareVelocityLocal( velocity, preventGrounding );

		if ( !IsLocalPlayer )
			ApplyWareVelocityOnOwner( velocity, preventGrounding );
	}

	[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
	private void ApplyWareVelocityOnOwner( Vector3 velocity, float preventGrounding )
	{
		ApplyWareVelocityLocal( velocity, preventGrounding );
	}

	private void ApplyWareVelocityLocal( Vector3 velocity, float preventGrounding )
	{
		if ( !Controller.IsValid() ) return;

		if ( preventGrounding > 0f )
			Controller.PreventGrounding( preventGrounding );

		Controller.Body.Sleeping = false;
		Controller.Body.Velocity += velocity;
	}

	public void SetWareVelocity( Vector3 velocity, float preventGrounding = 0f )
	{
		if ( !Networking.IsHost ) return;

		SetWareVelocityLocal( velocity, preventGrounding );

		if ( !IsLocalPlayer )
			SetWareVelocityOnOwner( velocity, preventGrounding );
	}

	[Rpc.Owner( NetFlags.HostOnly | NetFlags.Reliable )]
	private void SetWareVelocityOnOwner( Vector3 velocity, float preventGrounding )
	{
		SetWareVelocityLocal( velocity, preventGrounding );
	}

	private void SetWareVelocityLocal( Vector3 velocity, float preventGrounding )
	{
		if ( !Controller.IsValid() ) return;

		if ( preventGrounding > 0f )
			Controller.PreventGrounding( preventGrounding );

		Controller.Body.Sleeping = false;
		Controller.Body.Velocity = velocity;
	}

	public void SetPendingWareDeathImpulse( Vector3 impulse )
	{
		if ( !Networking.IsHost ) return;
		if ( impulse.Length < 1f ) return;

		_pendingWareDeathImpulse = impulse;
		_timeSincePendingWareDeathImpulse = 0f;
	}

	public void SimulateWareDeath( Transform deathTransform, Vector3 impulse = default )
	{
		if ( !Networking.IsHost ) return;
		if ( !Controller.IsValid() ) return;

		if ( impulse.Length < 1f && _timeSincePendingWareDeathImpulse < WareDeathImpulseLifetime )
			impulse = _pendingWareDeathImpulse;

		_pendingWareDeathImpulse = Vector3.Zero;
		CreateWareDeathRagdoll( deathTransform, impulse );
	}

	public void RestoreWareDeath()
	{
		if ( !Networking.IsHost ) return;

		DestroyWareDeathRagdoll();
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void CreateWareDeathRagdoll( Transform deathTransform, Vector3 impulse )
	{
		DestroyLocalWareDeathRagdoll();

		if ( !Controller.Renderer.IsValid() ) return;

		_wareDeathRagdoll = CreateRagdollObject( Vector3.Zero, deathTransform.Position, RagdollKind.WareSimulation, deathTransform );
		if ( !_wareDeathRagdoll.IsValid() ) return;

		_wareDeathRagdoll.Name = "Ware Death Ragdoll";
		_wareDeathRagdoll.Flags = GameObjectFlags.NotSaved;
		_wareDeathRagdoll.Tags.Add( "ware" );
		_wareDeathRagdoll.Tags.Add( "removable" );

		_wareDeathStarted = Time.Now;
		_wareDeathCameraAngles = null;
		SetWarePlayerAlpha( WareDeathPlayerAlpha );

		InitializeWareRagdollPhysics( _wareDeathRagdoll.Components.Get<ModelPhysics>(), impulse );
	}

	private void InitializeWareRagdollPhysics( ModelPhysics physics, Vector3 impulse )
	{
		if ( !physics.IsValid() ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.Sleeping = true;
			rb.MotionEnabled = false;
		}

		EnableWareRagdollPhysics( physics, impulse );
	}

	private async void EnableWareRagdollPhysics( ModelPhysics physics, Vector3 impulse )
	{
		await GameTask.Delay( WareRagdollEnableDelay );

		if ( !physics.IsValid() ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.MotionEnabled = true;
			rb.Sleeping = false;
		}

		ApplyWareDeathImpulse( physics, impulse );
	}

	private static void ApplyWareDeathImpulse( ModelPhysics physics, Vector3 impulse )
	{
		if ( impulse.Length < 1f ) return;

		var target = GetWareDeathImpulseBody( physics );
		if ( !target.IsValid() ) return;

		var speed = impulse.Length.Clamp( WareDeathImpulseMinSpeed, WareDeathImpulseMaxSpeed );
		target.Velocity = impulse.Normal * speed;
	}

	private static Rigidbody GetWareDeathImpulseBody( ModelPhysics physics )
	{
		Rigidbody result = null;
		var closestDistance = float.MaxValue;
		var center = Vector3.Zero;
		var count = 0;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			center += rb.WorldPosition;
			count++;
		}

		if ( count == 0 ) return null;

		center /= count;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;

			var distance = rb.WorldPosition.DistanceSquared( center );
			if ( distance >= closestDistance ) continue;

			result = rb;
			closestDistance = distance;
		}

		return result;
	}

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void DestroyWareDeathRagdoll()
	{
		DestroyLocalWareDeathRagdoll();
	}

	private void DestroyLocalWareDeathRagdoll()
	{
		if ( _wareDeathRagdoll.IsValid() )
			_wareDeathRagdoll.Destroy();

		_wareDeathRagdoll = null;
		_wareDeathCameraAngles = null;
		RestoreWarePlayerAlpha();
	}

	private void SetWarePlayerAlpha( float alpha )
	{
		foreach ( var renderer in GetPlayerRenderers() )
		{
			if ( !_wareOriginalTints.ContainsKey( renderer ) )
				_wareOriginalTints[renderer] = renderer.Tint;

			renderer.Tint = renderer.Tint.WithAlpha( alpha );
		}
	}

	private void RestoreWarePlayerAlpha()
	{
		foreach ( var pair in _wareOriginalTints.ToArray() )
		{
			if ( pair.Key.IsValid() )
				pair.Key.Tint = pair.Value;
		}

		_wareOriginalTints.Clear();
	}

	private IEnumerable<SkinnedModelRenderer> GetPlayerRenderers()
	{
		if ( Controller.Renderer.IsValid() )
			yield return Controller.Renderer;

		foreach ( var clothing in Controller.Renderer.GameObject.Children.Where( x => x.Tags.Has( "clothing" ) ).SelectMany( x => x.Components.GetAll<SkinnedModelRenderer>() ) )
		{
			if ( clothing.IsValid() )
				yield return clothing;
		}
	}

	private bool ShouldBlockWareHealthDamage( in DamageInfo damage )
	{
		if ( WareRoundSystem.Current is null ) return false;

		return !IsExplicitWareSuicide( damage );
	}

	private bool IsExplicitWareSuicide( in DamageInfo damage )
	{
		return damage.Damage >= WareSuicideDamageThreshold
			&& damage.Attacker == GameObject
			&& !damage.Weapon.IsValid();
	}

	private bool ApplyWareDeathCamera( CameraComponent camera )
	{
		if ( !IsLocalPlayer ) return false;
		if ( !_wareDeathRagdoll.IsValid() ) return false;

		var renderer = _wareDeathRagdoll.Components.Get<SkinnedModelRenderer>();
		if ( !renderer.IsValid() ) return false;
		if ( !TryGetWareRagdollViewTransform( renderer, out var ragdollView ) ) return false;

		var elapsed = Time.Now - _wareDeathStarted;
		if ( elapsed > WareDeathCameraDuration ) return false;

		var normalPosition = camera.WorldPosition;
		var normalRotation = camera.WorldRotation;
		var progress = Math.Clamp( elapsed / WareDeathCameraDuration, 0f, 1f );
		var positionBlend = 1f - MathF.Pow( 1f - progress, 2f );

		_wareDeathCameraAngles ??= ragdollView.Rotation.Angles();
		var targetAngles = progress < 0.3f ? ragdollView.Rotation.Angles() : normalRotation.Angles();
		var turnSpeed = 7f * (1f + progress) * Time.Delta * 60f;
		var angles = _wareDeathCameraAngles.Value;
		angles.pitch = ApproachAngle( angles.pitch, targetAngles.pitch, turnSpeed );
		angles.yaw = ApproachAngle( angles.yaw, targetAngles.yaw, turnSpeed );
		angles.roll = ApproachAngle( angles.roll, targetAngles.roll, turnSpeed * 0.5f );
		_wareDeathCameraAngles = angles;

		camera.WorldPosition = Vector3.Lerp( ragdollView.Position - ragdollView.Rotation.Forward * 0.4f, normalPosition, positionBlend );
		camera.WorldRotation = angles.ToRotation();
		return true;
	}

	private static float ApproachAngle( float current, float target, float amount )
	{
		var delta = Angles.NormalizeAngle( target - current );
		if ( MathF.Abs( delta ) <= amount ) return target;

		return current + MathF.Sign( delta ) * amount;
	}

	private static bool TryGetWareRagdollViewTransform( SkinnedModelRenderer renderer, out Transform transform )
	{
		if ( renderer.TryGetBoneTransform( "head", out transform ) ) return true;
		if ( renderer.TryGetBoneTransform( "neck_0", out transform ) ) return true;
		if ( renderer.TryGetBoneTransform( "spine_2", out transform ) )
		{
			transform.Position += Vector3.Up * 10f;
			return true;
		}

		if ( renderer.TryGetBoneTransform( "pelvis", out transform ) )
		{
			transform.Position += Vector3.Up * 25f;
			return true;
		}

		transform = default;
		return false;
	}
}
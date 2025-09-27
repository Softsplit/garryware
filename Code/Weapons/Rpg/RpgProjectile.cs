
/// <summary>
/// An RPG projectile. It supports either being fired in a set direction, or continuously updated with an end target.
/// </summary>
public partial class RpgProjectile : Projectile
{
	[Property] public SoundEvent LoopingSound { get; set; }
	[RequireComponent] public Explosive Explosive { get; set; }

	SoundHandle LoopingSoundHandle;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		LoopingSoundHandle = Sound.Play( LoopingSound, WorldPosition );

		if ( IsProxy )
			return;

		Rigidbody.Gravity = false;
	}

	protected override void OnDisabled()
	{
		LoopingSoundHandle?.Stop();
	}

	protected override void OnUpdate()
	{
		LoopingSoundHandle.Position = WorldPosition;
	}

	protected override void OnHit( Collision collision = default )
	{
		Explosive.Explode();
	}

	/// <summary>
	/// This is meant to be called continuously, updates the target, rotates slowly to it and moves at a set speed.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="speed"></param>
	[Rpc.Host]
	internal void UpdateWithTarget( Vector3 target, float speed )
	{
		var direction = (target - WorldPosition).Normal;
		var targetRotation = Rotation.LookAt( direction, Vector3.Up );

		WorldRotation = Rotation.Slerp( WorldRotation, targetRotation, Time.Delta * 6f );
		Rigidbody.Velocity = WorldTransform.Forward * (speed * 2f);
	}
}

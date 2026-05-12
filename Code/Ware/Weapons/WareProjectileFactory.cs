internal static class WareProjectileFactory
{
	private const float ProjectileMass = 80f;

	public static GameObject CreateAnimatedTriggerProjectile( string name, Vector3 position, Vector3 direction, string modelPath, Vector3 colliderStart, Vector3 colliderEnd, float colliderRadius )
	{
		var go = CreateBaseProjectile( name, position, direction );

		if ( !string.IsNullOrWhiteSpace( modelPath ) )
		{
			var renderer = go.Components.Create<SkinnedModelRenderer>();
			renderer.Model = Model.Load( modelPath );
			renderer.UseAnimGraph = false;
		}

		CreateRigidbody( go );

		var collider = go.Components.Create<CapsuleCollider>();
		collider.Start = colliderStart;
		collider.End = colliderEnd;
		collider.Radius = colliderRadius;
		collider.IsTrigger = true;

		return go;
	}

	public static GameObject CreatePhysicsModelProjectile( string name, Vector3 position, Vector3 direction, string modelPath, Vector3 fallbackColliderStart, Vector3 fallbackColliderEnd, float fallbackColliderRadius )
	{
		var go = CreateBaseProjectile( name, position, direction );
		var model = Model.Load( modelPath );

		var renderer = go.Components.Create<ModelRenderer>();
		renderer.Model = model;

		CreateRigidbody( go );

		if ( model?.Physics is not null )
		{
			var collider = go.Components.Create<ModelCollider>();
			collider.Model = model;
		}
		else
		{
			var collider = go.Components.Create<CapsuleCollider>();
			collider.Start = fallbackColliderStart;
			collider.End = fallbackColliderEnd;
			collider.Radius = fallbackColliderRadius;
		}

		return go;
	}

	private static GameObject CreateBaseProjectile( string name, Vector3 position, Vector3 direction )
	{
		var go = new GameObject( true, name )
		{
			WorldPosition = position,
			WorldRotation = Rotation.LookAt( direction, Vector3.Up ),
			Flags = GameObjectFlags.NotSaved,
			NetworkMode = NetworkMode.Object
		};

		go.Tags.Add( "projectile" );
		WareRoundSystem.Current?.RegisterTemporaryObject( go );

		return go;
	}

	private static Rigidbody CreateRigidbody( GameObject go )
	{
		var body = go.Components.Create<Rigidbody>();
		body.Gravity = true;
		body.EnhancedCcd = true;
		body.MassOverride = ProjectileMass;
		body.LinearDamping = 0.05f;
		body.AngularDamping = 0.05f;
		return body;
	}
}
public sealed partial class Player
{
	private float roll;

	void PlayerController.IEvents.OnEyeAngles( ref Angles ang )
	{
		var angles = ang;
		Local.IPlayerEvents.Post( x => x.OnCameraMove( ref angles ) );
		ang = angles;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent camera )
	{
		camera.FovAxis = CameraComponent.Axis.Vertical;
		camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );

		camera.RenderExcludeTags.Remove( "firstperson" );

		Local.IPlayerEvents.Post( x => x.OnCameraSetup( camera ) );

		if ( ApplyWareDeathCamera( camera ) )
		{
			Local.IPlayerEvents.Post( x => x.OnCameraPostSetup( camera ) );
			return;
		}

		ApplyMovementCameraEffects( camera );

		Local.IPlayerEvents.Post( x => x.OnCameraPostSetup( camera ) );
	}

	private void ApplyMovementCameraEffects( CameraComponent camera )
	{
		if ( !GamePreferences.ViewBobbing ) return;

		var r = Controller.WishVelocity.Dot( EyeTransform.Left ) / -250.0f;
		roll = MathX.Lerp( roll, r, Time.Delta * 10.0f, true );

		camera.WorldRotation *= new Angles( 0, 0, roll );
	}
}

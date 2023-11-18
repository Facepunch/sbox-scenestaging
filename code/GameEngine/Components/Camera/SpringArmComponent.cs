using Sandbox;
using Sandbox.Diagnostics;

[Title("Spring Arm")]
[Category("Camera")]
[Icon("start")]
public class SpringArmComponent : BaseComponent
{
    private CameraComponent _camera;


    [Property, Range(50, 200), Category("Follow Settings")]
    public float TargetArmLength { get; set; } = 180f;

    [Property, Range(0, 5), Category("Follow Settings")]
    public float SmoothTime { get; set; } = .2f;

    [Property, Range(0, 50), Category("Collision Settings")]
    public float CollisionOffset { get; set; } = 15f;

    [Property, Range(0, 15, .5f), Category("Collision Settings")]
    public float CollisionProbeSize { get; set; } = 5f;

    [Property, Category("Collision Settings")]
    public TagSet IgnoreLayers { get; set; } = new();

    [Property, Range(1, 5), Category("Debugging")] 
    private float SpringArmLineWidth { get; set; } = 2f;
    
    [Property, Category("Debugging")] 
    private Color SpringArmColor { get; set; } = new(.75f, .2f, .2f, .75f);
    
    [Property, Category("Debugging")] 
    private bool VisualDebugging { get; set; } = false;
    
    [Property, Category("Debugging")] 
    private bool ShowRaycasts { get; set; } = true;
    
    [Property, Category("Debugging")] 
    private bool ShowCollisionProbe { get; set; } = true;
    
    [Property, Category("Debugging")] 
    private bool ShowCollisionThroughSurface { get; set; } = true;
    
    [Property, Category("Debugging")] 
    private Color CollisionProbeColor { get; set; } = new(.15f, .75f, .2f, .75f);

    public override void OnEnabled()
    {
        _camera = GetComponent<CameraComponent>(deep: true);
        
        Assert.NotNull(_camera);
    }

    public override void Update()
    {
        if (_camera is null)
            return;

        UpdateCameraPosition();
        DrawGizmos();
    }

    private void UpdateCameraPosition()
    {
        var target = Transform.Position + Transform.Rotation.Forward * -TargetArmLength;
        var trace = Scene.PhysicsWorld.Trace
            .Ray(Transform.Position, target + -Transform.Rotation.Forward * CollisionOffset)
            .WithoutTags(IgnoreLayers).Run();

        var newCamPos = trace.Hit
            ? trace.HitPosition + Transform.Rotation.Forward * CollisionOffset
            : target;

        var velocity = Vector3.Zero;
        _camera.Transform.Position =
            Vector3.SmoothDamp(_camera.Transform.Position, newCamPos, ref velocity, SmoothTime, Time.Delta);
    }

    public override void DrawGizmos()
    {
        if (!VisualDebugging)
            return;

        Gizmo.Draw.LineThickness = SpringArmLineWidth;

        if (ShowRaycasts)
        {
            Gizmo.Draw.Color = SpringArmColor;

            if (ShowCollisionThroughSurface)
                Gizmo.Draw.Line(Transform.Position, _camera.Transform.Position);
            else
                Gizmo.Draw.Line(Transform.Position, _camera.Transform.Position);
        }

        if (ShowCollisionProbe)
        {
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.Color = CollisionProbeColor;
            Gizmo.Draw.LineSphere(_camera.Transform.Position, CollisionProbeSize);
        }
    }
}
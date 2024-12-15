using System;
using Sandbox.Spline;

public class SplineEditorTool : EditorTool<SplineComponent>
{

	public override void OnEnabled()
	{
		window = new SplineToolWindow();
		AddOverlay( window, TextFlag.RightBottom, 10 );
	}

	public override void OnUpdate()
	{
		window.ToolUpdate();
	}

	public override void OnDisabled()
	{
		window.OnDisabled();
	}

	public override void OnSelectionChanged()
	{

		var target = GetSelectedComponent<SplineComponent>();
		window.OnSelectionChanged( target );
	}

	private SplineToolWindow window = null;
}

class SplineToolWindow : WidgetWindow
{
	SplineComponent targetComponent;

	static bool IsClosed = false;

	ControlWidget positionControl;
	ControlWidget inTangentControl;
	ControlWidget outTangentControl;

	public SplineToolWindow()
	{
		ContentMargins = 0;
		Layout = Layout.Column();
		MaximumWidth = 800;
		MinimumWidth = 400;

		Rebuild();
	}

	void Rebuild()
	{
		Layout.Clear( true );
		Layout.Margin = 0;
		Icon = IsClosed ? "" : "route";
		WindowTitle = IsClosed ? "" : $"Spline Point [{_selectedPointIndex}] Editor - {targetComponent?.GameObject?.Name ?? ""}";
		IsGrabbable = !IsClosed;

		if ( IsClosed )
		{
			var closedRow = Layout.AddRow();
			closedRow.Add( new IconButton( "route", () => { IsClosed = false; Rebuild(); } ) { ToolTip = "Open Spline Point Editor", FixedHeight = HeaderHeight, FixedWidth = HeaderHeight, Background = Color.Transparent } );
			MinimumWidth = 0;
			return;
		}

		MinimumWidth = 400;

		var headerRow = Layout.AddRow();
		headerRow.AddStretchCell();
		headerRow.Add( new IconButton( "info" )
		{
			ToolTip = "Controls to edit the spline points.\nIn addition to modifying the properties in the control sheet, you can also use the 3D Gizmos.\nClicking on the spline between points will split the spline at that location.\nHolding shift while dragging a point's location will drag out a new point.",
			FixedHeight = HeaderHeight,
			FixedWidth = HeaderHeight,
			Background = Color.Transparent
		} );
		headerRow.Add( new IconButton( "close", CloseWindow ) { ToolTip = "Close Editor", FixedHeight = HeaderHeight, FixedWidth = HeaderHeight, Background = Color.Transparent } );

		if ( targetComponent.IsValid() )
		{
			this.GetSerialized().GetProperty( "_selectedPoint" ).TryGetAsObject( out SerializedObject point );

			var controlSheet = new ControlSheet();

			controlSheet.AddRow( point.GetProperty( nameof( SplinePoint.TangentMode ) ) );
			positionControl = controlSheet.AddRow( point.GetProperty( nameof( SplinePoint.Location ) ) );
			inTangentControl = controlSheet.AddRow( point.GetProperty( nameof( SplinePoint.InLocationRelative ) ) );
			outTangentControl = controlSheet.AddRow( point.GetProperty( nameof( SplinePoint.OutLocationRelative ) ) );

			var row = Layout.Row();
			row.Spacing = 16;
			row.Margin = 8;
			row.Add( new IconButton( "skip_previous", () =>
			{
				_selectedPointIndex = Math.Max( 0, _selectedPointIndex - 1 );
				RecalculateTangentsForSelecedPointAndAdjacentPoints();
				targetComponent.RequiresResample();
			} )
			{ ToolTip = "Go to previous point " } );
			row.Add( new IconButton( "skip_next", () =>
			{
				_selectedPointIndex = Math.Min( targetComponent.NumberOfPoints() - 1, _selectedPointIndex + 1 );
				RecalculateTangentsForSelecedPointAndAdjacentPoints();
				targetComponent.RequiresResample();
			} )
			{ ToolTip = "Go to next point" } );
			row.Add( new IconButton( "delete", () =>
			{
				targetComponent.RemovePoint( _selectedPointIndex );
				_selectedPointIndex = Math.Max( 0, _selectedPointIndex - 1 );
				RecalculateTangentsForSelecedPointAndAdjacentPoints();
				targetComponent.RequiresResample();
			} )
			{ ToolTip = "Delete point" } );
			row.Add( new IconButton( "add", () =>
			{
				if ( _selectedPointIndex == targetComponent.NumberOfPoints() - 1 )
				{
					targetComponent.InsertPoint( _selectedPointIndex + 1, _selectedPoint with { Location = _selectedPoint.Location + targetComponent.GetTangetAtDistance( targetComponent.GetDistanceAtPoint( _selectedPointIndex ) ) * 200 } );
				}
				else
				{
					targetComponent.AddPointAtDistance( (targetComponent.GetDistanceAtPoint( _selectedPointIndex ) + targetComponent.GetDistanceAtPoint( _selectedPointIndex + 1 )) / 2, Utils.SplitTangentModeBehavior.InferFromAdjacentpoints );
				}
				_selectedPointIndex++;
			} )
			{ ToolTip = "Insert point after curent point.\nYou can also hold shift while dragging a point to create a new point." } );

			controlSheet.AddLayout( row );

			Layout.Add( controlSheet );
		}


		Layout.Margin = 4;
	}

	void CloseWindow()
	{
		IsClosed = true;
		// TODO internal ?
		// Release();
		Rebuild();
		Position = Parent.Size - 32;
	}

	public void ToolUpdate()
	{
		if ( !targetComponent.IsValid() )
			return;

		DrawGizmos();
	}

	public void OnSelectionChanged( SplineComponent spline )
	{
		targetComponent = spline;

		targetComponent.ShouldRenderGizmos = false;

		Rebuild();
	}

	public void OnDisabled()
	{
		if ( targetComponent.IsValid() )
		{
			targetComponent.ShouldRenderGizmos = true;
		}
	}

	int _selectedPointIndex = 0;

	Sandbox.Spline.SplinePoint _selectedPoint
	{
		get => targetComponent.GetPoint( _selectedPointIndex );
		set
		{
			targetComponent.UpdatePoint( _selectedPointIndex, value );
			if ( _selectedPoint.TangentMode == Sandbox.Spline.SplinePointTangentMode.Auto || _selectedPoint.TangentMode == Sandbox.Spline.SplinePointTangentMode.Linear )
			{
				inTangentControl.Enabled = false;
				outTangentControl.Enabled = false;
			}
			else
			{
				inTangentControl.Enabled = true;
				outTangentControl.Enabled = true;
			}
			RecalculateTangentsForSelecedPointAndAdjacentPoints();
			targetComponent.RequiresResample();
		}
	}

	void RecalculateTangentsForSelecedPointAndAdjacentPoints()
	{
		targetComponent.RecalculateTangentsForPointAndAdjacentPoints( _selectedPointIndex );
	}


	bool _inTangentSelected = false;

	bool _outTangentSelected = false;

	bool _draggingOutNewPoint = false;

	void DrawGizmos()
	{
		var _splinePoints = targetComponent.GetPoints();

		using ( Gizmo.Scope( "spline_editor", targetComponent.WorldTransform ) )
		{
			// foreach segment
			for ( var i = 0; i < targetComponent.NumberOfSegments(); i++ )
			{
				// calculate bbox
				var bboxSegment =
					Utils.CalculateBoundingBox( _splinePoints.GetRange( i, 2 ).AsReadOnly() );
				//Gizmo.Draw.LineBBox( bboxSegment );
			}

			var points = Utils.ConvertSplineToPolyLine( _splinePoints.AsReadOnly(), 0.1f );

			var bbox = Utils.CalculateMinOrientedBoundingBox( _splinePoints.AsReadOnly() );
			using ( Gizmo.Scope( "bbox", bbox.Transform ) )
			{
				//Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( Vector3.Zero, bbox.Extents * 2 ) );
			}

			for ( var i = 0; i < points.Count - 1; i++ )
			{
				using ( Gizmo.Scope( "segment" + i, new Transform( Vector3.Zero ) ) )
				{
					Gizmo.Draw.LineThickness = 2f;
					using ( Gizmo.Hitbox.LineScope() )
					{
						Gizmo.Draw.Line( points[i], points[i + 1] );

						if ( Gizmo.IsHovered && Gizmo.HasMouseFocus )
						{
							Gizmo.Draw.Color = Color.Orange;
							Vector3 point_on_line;
							Vector3 point_on_ray;
							if ( !new Line( points[i], points[i + 1] ).ClosestPoint(
									Gizmo.CurrentRay.ToLocal( Gizmo.Transform ), out point_on_line, out point_on_ray ) )
								return;

							// It would be slighlty more efficient to use Spline.Utils directly,
							// but doggfoding the simplified component API ensures a user of that one would also have the ability yo built a spline editor
							var hoverDistance = targetComponent.FindDistanceClosestToLocation( point_on_line );

							using ( Gizmo.Scope( "hover_handle", new Transform( point_on_line,
									   Rotation.LookAt( targetComponent.GetTangetAtDistance( hoverDistance ) ) ) ) )
							{
								using ( Gizmo.GizmoControls.PushFixedScale() )
								{
									Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );
								}
							}

							if ( Gizmo.HasClicked && Gizmo.Pressed.This )
							{
								var newPointIndex = targetComponent.AddPointAtDistance( hoverDistance, Utils.SplitTangentModeBehavior.InferFromAdjacentpoints );
								_selectedPointIndex = newPointIndex;
								_inTangentSelected = false;
								_outTangentSelected = false;
							}
						}
					}
				}
			}

			// position location
			var positionGizmoLocation = _selectedPoint.Location;
			if ( _inTangentSelected )
			{
				positionGizmoLocation = _selectedPoint.InLocation;
			}

			if ( _outTangentSelected )
			{
				positionGizmoLocation = _selectedPoint.OutLocation;
			}

			if ( !Gizmo.IsShiftPressed )
			{
				_draggingOutNewPoint = false;
			}

			using ( Gizmo.Scope( "position", new Transform( positionGizmoLocation ) ) )
			{
				if ( Gizmo.Control.Position( "spline_control_", Vector3.Zero, out var delta ) )
				{
					if ( _inTangentSelected )
					{
						MoveSelectedPointInTanget( delta );
					}
					else if ( _outTangentSelected )
					{
						MoveSelectedPointOutTanget( delta );
					}
					else
					{
						if ( Gizmo.IsShiftPressed && !_draggingOutNewPoint )
						{
							_draggingOutNewPoint = true;
							targetComponent.InsertPoint( _selectedPointIndex + 1, _selectedPoint );
							_selectedPointIndex++;
						}
						else
						{
							MoveSelectedPoint( delta );
						}
					}
				}
			}

			for ( var i = 0; i < targetComponent.NumberOfPoints(); i++ )
			{
				if ( !targetComponent.IsLoop || i != targetComponent.NumberOfSegments() )
				{
					var splinePoint = targetComponent.GetPoint( i );

					using ( Gizmo.Scope( "point_controls" + i, new Transform( splinePoint.Location ) ) )
					{
						using ( Gizmo.Scope( "position", new Transform( Vector3.Zero ) ) )
						{
							using ( Gizmo.GizmoControls.PushFixedScale() )
							{
								Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

								if ( Gizmo.IsHovered || i == _selectedPointIndex &&
									(!_inTangentSelected && !_outTangentSelected) )
								{
									Gizmo.Draw.Color = Color.Orange;
								}

								Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

								if ( Gizmo.HasClicked && Gizmo.Pressed.This )
								{
									_selectedPointIndex = i;
									_inTangentSelected = false;
									_outTangentSelected = false;
								}
							}
						}

						Gizmo.Draw.Color = Color.White;


						if ( _selectedPointIndex == i )
						{
							Gizmo.Draw.LineThickness = 0.8f;

							using ( Gizmo.Scope( "in_tangent", new Transform( splinePoint.InLocationRelative ) ) )
							{

								if ( (_selectedPoint.TangentMode == SplinePointTangentMode.CustomMirrored || _selectedPoint.TangentMode == SplinePointTangentMode.Auto) && (_inTangentSelected || _outTangentSelected) )
								{
									Gizmo.Draw.Color = Color.Orange;
								}

								Gizmo.Draw.Line( -splinePoint.InLocationRelative, Vector3.Zero );

								using ( Gizmo.GizmoControls.PushFixedScale() )
								{
									if ( splinePoint.TangentMode != SplinePointTangentMode.Linear )
									{
										Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

										if ( Gizmo.IsHovered || _inTangentSelected )
										{
											Gizmo.Draw.Color = Color.Orange;
										}

										Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

										if ( Gizmo.HasClicked && Gizmo.Pressed.This )
										{
											_selectedPointIndex = i;
											_outTangentSelected = false;
											_inTangentSelected = true;
										}
									}

								}
							}

							using ( Gizmo.Scope( "out_tangent", new Transform( splinePoint.OutLocationRelative ) ) )
							{
								if ( (_selectedPoint.TangentMode == SplinePointTangentMode.CustomMirrored || _selectedPoint.TangentMode == SplinePointTangentMode.Auto) && (_inTangentSelected || _outTangentSelected) )
								{
									Gizmo.Draw.Color = Color.Orange;
								}

								Gizmo.Draw.Line( -splinePoint.OutLocationRelative, Vector3.Zero );

								using ( Gizmo.GizmoControls.PushFixedScale() )
								{
									if ( splinePoint.TangentMode != SplinePointTangentMode.Linear )
									{

										Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

										if ( Gizmo.IsHovered || _outTangentSelected )
										{
											Gizmo.Draw.Color = Color.Orange;
										}

										Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( Vector3.Zero, 2f ) );

										if ( Gizmo.HasClicked && Gizmo.Pressed.This )
										{
											_selectedPointIndex = i;
											_inTangentSelected = false;
											_outTangentSelected = true;
										}

									}
								}
							}
						}
					}
				}
			}
		}
	}

	private void MoveSelectedPoint( Vector3 delta )
	{
		var updatedPoint = _selectedPoint with { Location = _selectedPoint.Location + delta };
		targetComponent.UpdatePoint( _selectedPointIndex, updatedPoint );
	}

	private void MoveSelectedPointInTanget( Vector3 delta )
	{
		var updatedPoint = _selectedPoint;
		updatedPoint.InLocationRelative += delta;
		if ( updatedPoint.TangentMode == SplinePointTangentMode.Auto )
		{
			updatedPoint.TangentMode = SplinePointTangentMode.CustomMirrored;
		}
		if ( updatedPoint.TangentMode == SplinePointTangentMode.CustomMirrored )
		{
			updatedPoint.OutLocationRelative = -updatedPoint.InLocationRelative;
		}
		targetComponent.UpdatePoint( _selectedPointIndex, updatedPoint );
	}

	private void MoveSelectedPointOutTanget( Vector3 delta )
	{
		var updatedPoint = _selectedPoint;
		updatedPoint.OutLocationRelative += delta;
		if ( updatedPoint.TangentMode == SplinePointTangentMode.Auto )
		{
			updatedPoint.TangentMode = SplinePointTangentMode.CustomMirrored;
		}
		if ( updatedPoint.TangentMode == SplinePointTangentMode.CustomMirrored )
		{
			updatedPoint.InLocationRelative = -updatedPoint.OutLocationRelative;
		}
		targetComponent.UpdatePoint( _selectedPointIndex, updatedPoint );
	}
}

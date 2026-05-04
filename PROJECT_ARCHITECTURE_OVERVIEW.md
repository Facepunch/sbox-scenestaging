# sbox-scenestaging 專案程式與架構總覽

> 產生時間：2026-04-30  
> 目的：快速掌握此 Unity/s&box 測試專案的整體結構、主要模組與完整程式檔案索引。

## 1) 專案定位

- 專案名稱：`Testbed`（來自 `.sbproj`）
- 專案型態：`game`
- 主要用途：s&box 場景系統與元件系統的測試/驗證場（Scene Staging）
- 控制模式：以 `VR` 為主（Keyboard/Gamepad 關閉）
- 啟動場景：`scenes/tests/menu.scene`
- 地圖啟動場景：`scenes/tests/maploader.scene`
- 參考套件：`tft.tftvrfullbody`

## 2) 根目錄結構

- `.vscode/`：編輯器設定
- `Assets/`：場景與資產（主要是測試場景）
- `Code/`：主專案 C# 程式碼
- `Editor/`：編輯器端程式
- `Libraries/`：可重用函式庫/子模組（多個 `.sbproj`）
- `Localization/`：語系資料
- `ProjectSettings/`：專案設定
- `.sbproj`：主專案描述檔
- `sbox-scenestaging.sln`、`testbed.slnx`：方案檔

## 3) 程式與場景規模統計

- C# 檔案總數：`204`
  - `Libraries`：`126`
  - `Code`：`74`
  - `Editor`：`4`
- Scene 檔案總數：`123`
  - `Assets`：`122`
  - `Libraries`：`1`
- `.sbproj` 檔案總數：`11`

## 4) 主要程式模組（Code）

`Code/` 底下核心子目錄：

- `ExampleComponents/`：示例元件（網路、物理、互動、渲染測試）
- `Panels/UITests/`：UI 測試面板與元素測試
- `SceneMenu/`：場景選單相關功能
- `test/VR/`：VR 控制與互動測試程式
- `WorkInProgress/`：實驗/開發中功能
- `Assembly.cs`：主程式組件定義

### VR 相關（最近重點）

- `Code/test/VR/VRPlayerController.cs`：VR 轉向（平滑/瞬轉）+ 頭向移動 + 重力控制；`EnableRightStickTurn` 可關閉右手轉向、僅保留位移
- `Code/test/VR/VRPlayerRig.cs`：玩家根上集中啟用位移、桌面雙手模擬、左右 `VRGrabber`
- `Code/VRLogic/VRInteractionRules.cs`：插槽 id／距離純邏輯（供 `VRSocket` 與單元測試）
- `Code/test/VR/VRGrabber.cs`：VR 抓取/釋放（FixedJoint + 丟擲速度）
- `Code/test/VR/VRGhostHandTarget.cs`：幽靈目標（無剛體；對齊 grip／attachment，供日後彈簧關節追蹤）
- `Code/test/VR/Grabbable.cs`：可選標記可抓物，支援 Inspector 注入或快取 `Rigidbody`（與 `VRGrabber` 混合解析搭配）
- `Code/test/VR/Socketable.cs`：標記可插槽物、插槽 ID、可選 Attach 對齊點
- `Code/test/VR/VRSocket.cs`：插槽 Trigger、吸附半徑、放手後與 `VRGrabber` 銜接的吸附、可選低速自動吸附
- `Code/test/VR/VRFallbackSimulator.cs`：VR fallback 模擬

#### VRGrabber 編輯器設定（檢核清單）

1. 將 `VRGrabber` 掛在左手或右手對應的 GameObject，並在 Inspector 勾選或取消 **`IsLeftHand`** 以對應該手。
2. **`Hand Renderer`**：拖入帶有 **`SkinnedModelRenderer`** 的手部模型物件（可為手根或子物件，只要該物件上有此元件）。
3. **`Attachment Name`**：填寫與 ModelDoc 中建立的 attachment **完全相同**的名稱（含大小寫），例如 `weapon_hold`、`grip_point`。
4. **觸發範圍**：此手（或子階層）需有設為 **Trigger** 的 **Collider**，`ITriggerListener` 才會收到進出事件；否則不會記錄可抓目標。
5. **可抓物件**：目標需有 **`Rigidbody`**。可選在根物件加 **`Grabbable`** 以指定或快取剛體；未加時程式會先找本體 `Rigidbody`，再以 `FindMode.EnabledInSelfAndDescendants` 保底。
6. **關節**：`FixedJoint` 建立在掛載 `VRGrabber` 的 GameObject 上；該物件通常也需具備適當的物理本體，關節行為較穩定。
7. **釋放時速度**：目前僅將手部 **線速度** 套到 `Rigidbody.Velocity`；角速度在程式中清零（因手部角速度型別與 `Rigidbody.AngularVelocity` 不一致）。詳見 `VRGrabber.cs` 類別註解。

#### VR Socket（插槽）

- 可插物：根階層加 **`Socketable`**（`SocketId`、`AttachPivot`），並建議搭配 **`Grabbable`** + **`Rigidbody`**。
- 插槽：加 **`VRSocket`**、**Trigger Collider**、可選子物件 **`SlotAnchor`** 作吸附對齊；`AcceptId` 與 `SocketId` 相符（或槽端 `AcceptId` 留空接受任意）且距離在 **`SnapRadius`** 內才吸附。
- **放手吸附**：`VRGrabber` 放手後會呼叫 `VRSocket.NotifyGripReleased`，各槽的 **`SnapOnGripRelease`** 為真時會嘗試吸附（避免與手上關節並存：放手後才判定）。
- **低速自動吸附**：將 **`AutoSnapWhenSlowInTrigger`** 開啟，物體在 Trigger 內且線速度低於 **`MaxSettleSpeed`** 時可自動入槽。
- **鎖定**：預設 **`ParentToSocket`**（`SetParent` + 關閉 `Rigidbody`）；可改 **`FixedJointToSocketBody`** 並指定 **`SocketJointBody`**。解鎖呼叫 **`VRSocket.Unsnap()`**（可綁其他輸入）。

## 5) Libraries 模組概覽

- `facepunch.libsdf`：2D/3D SDF 與 Polygon mesh 生成工具
- `facepunch.modelviewer`：模型檢視器、服裝/動畫控制
- `facepunch.playercontroller`：玩家控制器（移動、蹲伏、推擠、腳步）
- `facepunch.libevents`：事件系統與測試
- `facepunch.jigglebones`、`JiggleBones`：骨骼抖動
- `facepunch.shatterglass`、`ShatterGlass`：玻璃破碎
- `SplineTools`：樣條工具與編輯器工具
- `weaponlab`：武器測試程式
- `bugge.unity_importer`：Unity package/材質/貼圖匯入工具
- `isotope.gitversioncontrol`：版本控制整合工具

## 6) 完整 C# 程式檔案索引（.cs）

```text
.\Libraries\facepunch.playercontroller\Code\PlayerController.cs
.\Libraries\facepunch.playercontroller\Editor\MyEditorMenu.cs
.\Libraries\facepunch.playercontroller\Code\PlayerFootsteps.cs
.\Libraries\facepunch.playercontroller\Code\Assembly.cs
.\Libraries\facepunch.playercontroller\Code\PlayerPusher.cs
.\Libraries\facepunch.playercontroller\UnitTests\LibraryTest.cs
.\Libraries\facepunch.playercontroller\UnitTests\UnitTest.cs
.\Code\test\VR\VRGrabber.cs
.\Code\test\VR\Grabbable.cs
.\Code\test\VR\Socketable.cs
.\Code\test\VR\VRSocket.cs
.\Code\test\VR\VRFallbackSimulator.cs
.\Code\test\VR\VRPlayerController.cs
.\Code\test\VR\VRPlayerRig.cs
.\Code\VRLogic\VRInteractionRules.cs
.\Libraries\isotope.gitversioncontrol\Editor\VersionCont.Git.cs
.\Libraries\isotope.gitversioncontrol\Editor\VersionCont.cs
.\Libraries\facepunch.shatterglass\Code\Glass.cs
.\Libraries\isotope.gitversioncontrol\Editor\VersionCont.Diff.cs
.\Libraries\facepunch.modelviewer\Editor\ModelViewer\ClothingWidget.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerViewModelArms.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerViewModel.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerPlayerFootstep.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerClothingDresser.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerGrid.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerCharacterController\ModelViewerPlayerController.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerCharacterController\ModelViewerCitizenAnimation.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerCharacterController\ModelViewerInteract.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerCharacterController\ModelViewerCharacterController\ModelViewerCharacterControllerHelper.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ModelViewerCharacterController\ModelViewerCharacterController\ModelViewerCharacterController.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\MVCitizenAnimation.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\IkMover.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ClothingFileDresser.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\FunStuff\WaterPlayer.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ClothingAnimation.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\ClothingCamera.cs
.\Libraries\facepunch.modelviewer\Code\Global.cs
.\Libraries\facepunch.modelviewer\Code\ModelViewer\CameraController.cs
.\Libraries\facepunch.libevents\UnitTests\UnitTest.cs
.\Libraries\facepunch.libevents\UnitTests\DispatchTests.cs
.\Libraries\facepunch.libevents\Code\SortingHelper.cs
.\Libraries\facepunch.libevents\Code\GameEvents\GameEvent.cs
.\Libraries\facepunch.libevents\Code\GameEvents\Attributes.cs
.\Libraries\facepunch.jigglebones\Code\JiggleBone.cs
.\Libraries\facepunch.jigglebones\Code\BouncyBone.cs
.\Libraries\facepunch.jigglebones\Code\Assembly.cs
.\Libraries\bugge.unity_importer\Editor\UnityPackageImportWindow.cs
.\Libraries\bugge.unity_importer\Editor\UnityPackageExtractor.cs
.\Libraries\bugge.unity_importer\Editor\MeshSplitterIntegrator.cs
.\Libraries\bugge.unity_importer\Editor\UnityImporterMenu.cs
.\Libraries\bugge.unity_importer\Editor\Converters\UnityTextureConverter.cs
.\Libraries\bugge.unity_importer\Editor\Converters\UnityMaterialConverter.cs
.\Libraries\weaponlab\Code\TestWeapon.cs
.\Libraries\weaponlab\Code\CameraSetup.cs
.\Libraries\weaponlab\Code\CameraNoise.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonModelRenderer.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.Validate.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.SVG.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.Edge.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.Fill.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\Helpers.cs
.\Libraries\facepunch.libsdf\Code\facepunch.libpolygon\PolygonMeshBuilder.Bevel.cs
.\Libraries\facepunch.libsdf\Code\WorldQuality.cs
.\Libraries\facepunch.libsdf\Code\SdfWorld.Network.cs
.\Libraries\facepunch.libsdf\Code\SdfWorld.cs
.\Libraries\facepunch.libsdf\Code\SdfChunk.cs
.\Libraries\facepunch.libsdf\Code\SdfArray.cs
.\Libraries\facepunch.libsdf\Code\SdfResource.cs
.\Libraries\facepunch.libsdf\Code\Pooled.cs
.\Libraries\facepunch.libsdf\Code\Helpers.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DWorld.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DVolume.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DChunk.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DArray.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DMeshWriter.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3DMeshWriter.Types.cs
.\Libraries\facepunch.libsdf\Code\3D\Sdf3D.cs
.\Libraries\facepunch.libsdf\Code\3D\Noise\Cellular.cs
.\Libraries\facepunch.libsdf\Code\2D\Transform2D.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DWorld.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DMeshWriter.Types.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DMeshWriter.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DLayer.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DChunk.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DMeshWriter.SourceEdges.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2DArray.cs
.\Libraries\facepunch.libsdf\Code\2D\Sdf2D.cs
.\Libraries\SplineTools\Editor\SplineComponentEditorTool.cs
.\Libraries\SplineTools\Editor\Assembly.cs
.\Libraries\SplineTools\Code\SplineModelRenderer.cs
.\Libraries\SplineTools\Code\SplineComponent.cs
.\Libraries\SplineTools\Code\SplineCollider.cs
.\Libraries\SplineTools\Code\Assembly.cs
.\Libraries\ShatterGlass\Code\Glass.cs
.\Libraries\JiggleBones\Code\JiggleBone.cs
.\Libraries\JiggleBones\Code\BouncyBone.cs
.\Libraries\JiggleBones\Code\Assembly.cs
.\Editor\Assembly.cs
.\Code\WorkInProgress\VolumetricMaterial.cs
.\Code\WorkInProgress\RectLight.cs
.\Code\WorkInProgress\PointLight2.cs
.\Code\WorkInProgress\PlayerGrabber.cs
.\Code\WorkInProgress\PlaneReflection.cs
.\Code\WorkInProgress\PhysicsGrabber.cs
.\Code\WorkInProgress\ParticlePhysics.cs
.\Code\WorkInProgress\ParticleLineRenderer.cs
.\Code\WorkInProgress\ParticleLineEmitter.cs
.\Code\WorkInProgress\NanoVDB.cs
.\Code\WorkInProgress\NanoVDB.Grid.cs
.\Code\WorkInProgress\LineRendererLight.cs
.\Code\WorkInProgress\FreeCamGameObjectSystem.cs
.\Code\WorkInProgress\DistanceFieldGPU.cs
.\Code\WorkInProgress\CapsuleLight.cs
.\Code\WorkInProgress\BobbleJoint.cs
.\Code\SceneMenu\ReturnToMenu.cs
.\Code\Panels\UITests\Elements\TextEntry.cs
.\Code\Panels\UITests\Elements\RenderScene.cs
.\Code\Panels\UITests\Elements\InputButtons.cs
.\Code\ExampleComponents\UpdateAction.cs
.\Code\ExampleComponents\TurretComponent.cs
.\Code\ExampleComponents\TriggerDebug.cs
.\Code\ExampleComponents\TraceDebugVis.cs
.\Code\ExampleComponents\SpawnObjectPeriodically.cs
.\Code\ExampleComponents\SpinComponent.cs
.\Code\ExampleComponents\SnapshotTest.cs
.\Code\ExampleComponents\SelfDestructComponent.cs
.\Code\ExampleComponents\Sdf3DWorldExample.cs
.\Code\ExampleComponents\ScreenGizmoText.cs
.\Code\ExampleComponents\RootMotionTest.cs
.\Code\ExampleComponents\RenderToTextureTest.cs
.\Code\ExampleComponents\PlayerUse.cs
.\Code\ExampleComponents\PlayerSquish.cs
.\Code\ExampleComponents\PlayerFootsteps.cs
.\Code\ExampleComponents\PlayerController.cs
.\Code\ExampleComponents\NoClip.cs
.\Code\ExampleComponents\NetworkTest.cs
.\Code\ExampleComponents\NetworkStress\WalkAround.cs
.\Code\ExampleComponents\NetworkStress\SpawnNetworkedObjects.cs
.\Code\ExampleComponents\NetworkStress\SpawnMovingObjects.cs
.\Code\ExampleComponents\NetworkStress\FlyAroundParent.cs
.\Code\ExampleComponents\NetworkSession.cs
.\Code\ExampleComponents\NavigationTargetWanderer.cs
.\Code\ExampleComponents\NavigationSlidingDoor.cs
.\Code\ExampleComponents\NavigationQueryTest.cs
.\Code\ExampleComponents\MoveHelperDebugVis.cs
.\Code\ExampleComponents\MapLoadedHandler.cs
.\Code\ExampleComponents\LoadingTestComponent.cs
.\Code\ExampleComponents\LatticeDeform.cs
.\Code\ExampleComponents\IkReachOut.cs
.\Code\ExampleComponents\HitReactionTest.cs
.\Code\ExampleComponents\Gun.cs
.\Code\ExampleComponents\GameNetworkManager.cs
.\Code\ExampleComponents\DropObjectOnFootstep.cs
.\Code\ExampleComponents\DoorComponent.cs
.\Code\ExampleComponents\DebugOverlayTest.cs
.\Code\ExampleComponents\DampenTest.cs
.\Code\ExampleComponents\ColorOverTime.cs
.\Code\ExampleComponents\CollisionDebugComponent.cs
.\Code\ExampleComponents\ChangeColorOnEnterTrigger.cs
.\Code\ExampleComponents\CameraPhysicsDebug.cs
.\Code\ExampleComponents\BrickBrick.cs
.\Code\ExampleComponents\BrickBall.cs
.\Code\ExampleComponents\BaseInteractor.cs
.\Code\ExampleComponents\AlphaOverTime.cs
.\Code\Assembly.cs
```

## 7) 完整場景檔索引（.scene）

```text
.\Assets\Scenes\Tests\test.vr.scene
.\Libraries\facepunch.modelviewer\Assets\Scenes\ModelViewer_Example.scene
.\Assets\Scenes\test.scene
.\Assets\Scenes\Tests\Sound\sounds.scene
.\Assets\Scenes\Tests\Sound\sound.dsp.scene
.\Assets\Scenes\Tests\Physics\physics_lock.scene
.\Assets\Scenes\Tests\Particles\particle.rework.scene
.\Assets\Scenes\Tests\weaponlab.scene
.\Assets\Scenes\Tests\Sound\sound.occlusion.scene
.\Assets\Scenes\Tests\Sound\sound.stress.scene
.\Assets\Scenes\Tests\Sound\sound.map.scene
.\Assets\Scenes\Tests\Rendering\shadows.scene
.\Assets\Scenes\Tests\Rendering\light.cookies.scene
.\Assets\Scenes\Tests\Rendering\lights.scene
.\Assets\Scenes\Tests\Rendering\depth_of_field.scene
.\Assets\Scenes\Tests\Rendering\decals.scene
.\Assets\Scenes\Tests\Rendering\rendertotexture.scene
.\Assets\Scenes\Tests\Rendering\camerarendertexture.scene
.\Assets\Scenes\Tests\Rendering\mirror.many.scene
.\Assets\Scenes\Tests\Rendering\baked_indirect_light.scene
.\Assets\Scenes\Tests\PlayerController\player_controller.scene
.\Assets\Scenes\Tests\PlayerController\player_controller_physics.scene
.\Assets\Scenes\Tests\Physics\triggers.scene
.\Assets\Scenes\Tests\Rendering\water.scene
.\Assets\Scenes\Tests\Physics\physics_ragdoll.scene
.\Assets\Scenes\Tests\Physics\joints.scene
.\Assets\Scenes\Tests\loading.scene
.\Assets\Scenes\Tests\Physics\collision.scene
.\Assets\Scenes\Tests\turret.scene
.\Assets\Scenes\Tests\prefabs.scene
.\Assets\Scenes\Tests\maploader.scene
.\Assets\Scenes\Tests\menu.scene
.\Assets\Scenes\Tests\hammer-gameobjects.scene
.\Assets\Scenes\Tests\UI\worldinput.scene
.\Assets\Scenes\Tests\UI\test.ui.transform.scene
.\Assets\Scenes\Tests\UI\test.ui.scene
.\Assets\Scenes\Tests\UI\test.ui.beforeafter.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.transitions.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.text.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.mask.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.keyframes.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.imagerendering.scene
.\Assets\Scenes\Tests\UI\Styles\test.ui.filters.scene
.\Assets\Scenes\Tests\UI\Basics\test.ui.scissoring.scene
.\Assets\Scenes\Tests\UI\Basics\test.ui.mousecapture.scene
.\Assets\Scenes\Tests\UI\Basics\test.ui.calc.scene
.\Assets\Scenes\Tests\Terrain\terrain.benchmark.scene
.\Assets\Scenes\Tests\Sound\soundscape.scene
.\Assets\Scenes\Tests\Sound\sound.voice.scene
.\Assets\Scenes\Tests\Rendering\volumetric_rendering.scene
.\Assets\Scenes\Tests\Rendering\transparency.sort.scene
.\Assets\Scenes\Tests\Rendering\ssao.scene
.\Assets\Scenes\Tests\Rendering\skyballs.scene
.\Assets\Scenes\Tests\Rendering\shader_ssr.scene
.\Assets\Scenes\Tests\Rendering\shader_lighting.scene
.\Assets\Scenes\Tests\Rendering\shader_classes.scene
.\Assets\Scenes\Tests\Rendering\roughness_test.scene
.\Assets\Scenes\Tests\Rendering\rendertextureasset.scene
.\Assets\Scenes\Tests\Rendering\postprocess.scene
.\Assets\Scenes\Tests\Rendering\orthographic.scene
.\Assets\Scenes\Tests\Rendering\multicam.brokenshadows.scene
.\Assets\Scenes\Tests\Rendering\multicam.scene
.\Assets\Scenes\Tests\Rendering\mirror.scene
.\Assets\Scenes\Tests\Rendering\glass.depthawaremask.scene
.\Assets\Scenes\Tests\Rendering\cubemap.multires.scene
.\Assets\Scenes\Tests\Rendering\cubemap.scene
.\Assets\Scenes\Tests\Rendering\cubemap.dynamic.scene
.\Assets\Scenes\Tests\Rendering\cornelbox_fog.scene
.\Assets\Scenes\Tests\Rendering\cornelbox.scene
.\Assets\Scenes\Tests\Rendering\bloom.scene
.\Assets\Scenes\Tests\Rendering\area_lights.scene
.\Assets\Scenes\Tests\PlayerController\platforms.scene
.\Assets\Scenes\Tests\Physics\trace.scene
.\Assets\Scenes\Tests\Physics\props.scene
.\Assets\Scenes\Tests\Physics\physics_simple.scene
.\Assets\Scenes\Tests\Physics\dampening.scene
.\Assets\Scenes\Tests\Particles\particle.text.scene
.\Assets\Scenes\Tests\Particles\particle.sheets.scene
.\Assets\Scenes\Tests\Particles\particle.motionblur.scene
.\Assets\Scenes\Tests\Particles\particle.prewarm.scene
.\Assets\Scenes\Tests\Particles\particle.follower.scene
.\Assets\Scenes\Tests\Particles\particle.fog.scene
.\Assets\Scenes\Tests\Particles\particle.feather.scene
.\Assets\Scenes\Tests\Particles\particle.emitters.scene
.\Assets\Scenes\Tests\Particles\particle.controller.scene
.\Assets\Scenes\Tests\Particles\particle.collision.scene
.\Assets\Scenes\Tests\Particles\particle.collideprefab.scene
.\Assets\Scenes\Tests\Networking\rpcspam.scene
.\Assets\Scenes\Tests\Networking\networkstress.scene
.\Assets\Scenes\Tests\Networking\networking.scene
.\Assets\Scenes\Tests\Navigation\nav.links.scene
.\Assets\Scenes\Tests\Navigation\nav.basic.scene
.\Assets\Scenes\Tests\Navigation\nav.areas.scene
.\Assets\Scenes\Tests\Navigation\nav.area.costs.scene
.\Assets\Scenes\Tests\MovieMaker\morphs.scene
.\Assets\Scenes\Tests\MovieMaker\keyframes.scene
.\Assets\Scenes\Tests\MovieMaker\gameplay_recording.scene
.\Assets\Scenes\Tests\Models\viewmodel.scene
.\Assets\Scenes\Tests\Models\rootmotion.scene
.\Assets\Scenes\Tests\Models\ik.scene
.\Assets\Scenes\Tests\Models\hitbox.scene
.\Assets\Scenes\Tests\Models\footsteps.scene
.\Assets\Scenes\Tests\Models\dresser.scene
.\Assets\Scenes\Tests\Input\input_testing.scene
.\Assets\Scenes\Tests\Fog\volume_fog.scene
.\Assets\Scenes\Tests\Fog\fog.gradient.scene
.\Assets\Scenes\Tests\Fog\fog.cubemap.scene
.\Assets\Scenes\Tests\Experiments\procedural.bones.scene
.\Assets\Scenes\Tests\Debug\DebugOverlay.scene
.\Assets\Scenes\Tests\Experiments\deform.lattice.scene
.\Assets\Scenes\Tests\Components\trails.scene
.\Assets\Scenes\Tests\Components\splines.scene
.\Assets\Scenes\Tests\Components\shatter_glass.scene
.\Assets\Scenes\Tests\Components\sdf3d.scene
.\Assets\Scenes\Tests\Components\ropes.scene
.\Assets\Scenes\Tests\Components\line-renderer.scene
.\Assets\Scenes\Tests\ActionGraph\graph_id_confict.scene
.\Assets\Scenes\Tests\ActionGraph\actions.triggers.scene
.\Assets\Scenes\Tests\ActionGraph\actions.scene
.\Assets\Scenes\Tests\ActionGraph\actions.properties.scene
.\Assets\Scenes\Tests\ActionGraph\actions.prefabvars.scene
.\Assets\Scenes\Tests\ActionGraph\actions.cached.scene
.\Assets\Scenes\Tests\ActionGraph\actions.prefabtest.scene
```

## 8) 專案描述檔索引（.sbproj）

```text
Libraries/ShatterGlass/shatterglass.sbproj
Libraries/facepunch.libsdf/.sbproj
Libraries\facepunch.playercontroller\playercontroller.sbproj
Libraries/facepunch.modelviewer/modelviewer.sbproj
.sbproj
Libraries/facepunch.libevents/libevents.sbproj
Libraries/JiggleBones/jigglebones.sbproj
Libraries/SplineTools/splinetools.sbproj
Libraries/bugge.unity_importer/unityimporter.sbproj
Libraries/isotope.gitversioncontrol/gitversioncontrol.sbproj
Libraries/weaponlab/weaponlab.sbproj
```

## 9) 備註

- 此文件以目前工作區檔案狀態為準，後續新增/刪除檔案時建議重新產生。
- 若你要，我可以再幫你產一份「依資料夾分群、附簡短職責說明」的精簡版導覽（較適合新成員 onboarding）。

# Spec: VR 玩家編排（VRPlayerRig）與可測邏輯抽離

## Context and problem statement

專案內 VR 行為已拆成多個 `Component`（位移、抓取、插槽、桌面模擬），但缺少單一掛載點以啟用／停用子功能，且 `VRSocket` 的 id／距離判斷與引擎耦合同在單一檔案內，不利單元測試。另 `VRMovement` 與 `VRPlayerController` 在位移上重疊，易造成雙重 `CharacterController.Move()`。

## Goals and non-goals

**Goals**

- 提供根節點 `VRPlayerRig` 以控制是否啟用：位移、桌面雙手模擬、左右手 `VRGrabber`、子階層 `VRGhostHandTarget`。
- 以 `EnableRightStickTurn` 合併原「僅移動」用途，移除 `VRMovement`。
- 將插槽可接受性與距離判斷抽成 `VRLogic.VRInteractionRules`，供 `VRSocket` 與測試共用。
- 在規格與程式中固定本專案依賴的引擎 VR API 清單與延伸閱讀連結。

**Non-goals**

- 不實作完整 `Input.VR` 封裝層；不涵蓋本專案未使用之按鍵／指豎（可於之後 spec 擴充）。
- 不強制重構 `VRGrabber` 物理關節假設（手是否需 `Rigidbody` 仍由場景配置決定）。

## Scope

**In**

- 新檔 [`Code/test/VR/VRPlayerRig.cs`](../../Code/test/VR/VRPlayerRig.cs)、[`Code/VRLogic/`](../../Code/VRLogic/)（`net8.0`，與本機 `dotnet test` 相容；主專案 `testbed` 仍為 `net10.0`）、[`Code/UnitTests/VRLogic.UnitTests/`](../../Code/UnitTests/VRLogic.UnitTests/)。
- 更新 [`Code/test/VR/VRSocket.cs`](../../Code/test/VR/VRSocket.cs)、[`Code/test/VR/VRPlayerController.cs`](../../Code/test/VR/VRPlayerController.cs)、刪除 `VRMovement.cs`。
- 測試場景 [`Assets/Scenes/Tests/test.vr.scene`](../../Assets/Scenes/Tests/test.vr.scene) 掛上 `VRPlayerRig`、雙手 `VRGrabber`、可抓方塊（`Rigidbody` + `Grabbable`）。

**Out**

- 新插槽或抓取演算法變體、多人同步、Input remapping。

## 引擎 VR API（本專案使用／對照）

以下為程式中**實際使用**的成員；完整表面請以官方 API 為準。

| 概念 | 成員 | 說明 |
|------|------|------|
| 執行模式 | `Game.IsRunningInVR` | 桌面模擬分支 |
| 頭部 | `Input.VR.Head.Rotation` | 水平移動方向參考 |
| 左右手 | `Input.VR.LeftHand` / `RightHand` | 控制器狀態 |
| 手 | `Joystick.Value` | 搖桿 2D |
| 手 | `Grip.Value` | 抓取 |
| 手 | `Velocity` | 放開時套到剛體 |
| 手（姿態） | `VRController.Transform` / `AimTransform` | 世界空間 grip／瞄準姿態（見 `Sandbox.Engine.xml`）；`VRGhostHandTarget` 可選直接讀取 |

**官方／參考文件（延伸閱讀）**

- s&box 啟用與偵測 VR：<https://docs.facepunch.com/s/sbox-dev/doc/vr-GPhXAmcHLM>
- 社群 API 瀏覽（`Input` / `Sandbox.VR`）：<https://sbox.redeaglestudios.org/api/Sandbox.Input> 、<https://sbox.redeaglestudios.org/api/ns/Sandbox.VR>

**場景內建元件（本測試場景已用）**

- `Sandbox.VR.VRAnchor`、`VRTrackedObject`、`VRHand` 等（與自訂腳本互補，非本 spec 修改範圍）。

## API / component contracts

### `VRPlayerRig`

- **掛載位置**：與 `CharacterController`、`VRPlayerController`、`VRFallbackSimulator` 同一玩家根物件。
- **行為**：`OnAwake` 呼叫 `ApplyFeatureToggles()`：依旗標設定 `VRPlayerController.Enabled`、`VRFallbackSimulator.Enabled`，子階層所有 `VRGrabber.Enabled`（依 `IsLeftHand` 區分左右），以及子階層所有 `VRGhostHandTarget.Enabled`（`EnableGhostTargets`）。
- **Auto wire**：若 `AutoWireCharacterController` 且 `VRPlayerController.Controller` 未設定，則指定同物件上之 `CharacterController`。

### `VRPlayerController`

- 新增 `EnableRightStickTurn`（預設 `true`）。為 `false` 時僅保留左手搖桿位移，不處理右手轉向。

### `VRInteractionRules`（`VRLogic` 組件）

- `SocketAccepts(acceptId, itemSocketId)`：`acceptId` 空字串或 null 時接受任意；否則序數比對。
- `IsWithinRadius(...)`：三維歐氏距離是否 ≤ 半徑。

### `VRSocket`

- `IdsMatch` / 距離檢查改呼叫 `VRInteractionRules`，行為與重構前一致。

### `VRGhostHandTarget`

- **用途**：無剛體、無碰撞之空物件元件，每幀寫入世界 Transform，作為日後彈簧關節追蹤的「幽靈手／目標點」。
- **來源**：可選 `TransformSource`（與 `VRTrackedObject`／`VRFallbackSimulator` 相容）；若 `UseVrInputDirect` 且 `Game.IsRunningInVR`，則使用 `Input.VR.LeftHand`／`RightHand` 的 `Transform` 或 `AimTransform`。
- **Attachment**：可選與 `VRGrabber` 相同之 `HandRenderer` + `AttachmentName`，有則優先對齊該 attachment。
- **場景**：`test.vr.scene` 於左右手追蹤根下各一子物件 `GhostTarget_Left`／`GhostTarget_Right`。

## Test strategy

- **單元**：`VRLogic.UnitTests` 覆蓋 `VRInteractionRules`（接受規則、距離邊界）。
- **整合／手動**：VR 頭戴或 `VRFallbackSimulator` 下驗證位移、抓取、插槽；本 spec 不要求自動化整合測試。

## Rollout and rollback

- **Rollout**：合併後以 `test.vr.scene` 驗證；新專案 `VRLogic` / `VRLogic.UnitTests` 已加入 solution。
- **Rollback**：還原 `VRSocket`／場景／刪除之 `VRMovement` 相關 commit；恢復 `VRMovement.cs` 若仍被舊場景參照（目前無參照）。

## Risks and open questions

- `VRGrabber` 依賴手部與關節物件的物理設定；測試場景僅示範最小配置。
- `AttachmentName` 預設 `weapon_hold` 若模型無該 attachment，仍可做關節抓取，但對齊略過。

## Definition of done

- `VRPlayerRig` 可於 Inspector 切換功能並於 Awake 生效。
- `VRMovement` 已移除；`VRPlayerController.EnableRightStickTurn` 涵蓋「僅移動」。
- `VRInteractionRules` 有單元測試；`dotnet test` 通過 `VRLogic.UnitTests`。
- `CHANGELOG.md` 已更新；本 spec 與實作一致。

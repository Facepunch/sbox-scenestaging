# Spec: VR 幽靈手、抓取、Socket 與本體移動（官方對齊／業界分層／CI／DI）

## Context and problem statement

本專案已有 `VRGhostHandTarget`、`VRGrabber`、`VRSocket`、`VRPlayerRig` 與 `VRLogic.VRInteractionRules`，但缺少一份**與官方文件一致**的契約說明、與 [ENGINEERING_WORKFLOW.md](../ENGINEERING_WORKFLOW.md) 對齊的 SDD／TDD／CI／DI 條目，以及本體移動與物理步長一致化。本 spec 銜接 [2026-05-04-vr-player-rig.md](./2026-05-04-vr-player-rig.md) 並擴充至 Alyx 向手感、Walker 風格 `CharacterController` 移動與合併門檻。

## Goals

- 固定**追蹤鏈**、**Trigger 偵測**、**ModelDoc attachment 命名**與官方 s&box 文件一致之開發契約。
- 將 **Interactor／Interactable／Socket** 職責寫入規格：`VRGhostHandTarget`（呈現目標）、`VRGrabber`（選取＋關節）、`VRSocket`（槽位狀態）。
- **抓取**：關節建立／釋放與 **FixedUpdate** 對齊；Grip 閾值與 wish 方向之純邏輯收斂至 `VRLogic` 並附單元測試。
- **本體移動**：`VRPlayerController` 以 **OnFixedUpdate** 驅動 `CharacterController`（`Accelerate`／`ApplyFriction`／`Punch` 跳躍、可選蹲伏），與雙手物理步長一致。
- **CI**：管線現階段僅 `dotnet test`（[UnitTests/testbed.unittest.csproj](../../UnitTests/testbed.unittest.csproj)）；**每完成計畫 todo 須本地／MR 通過同一測試命令**。
- **DI（務實）**：純邏輯在 `VRLogic`；釋放廣播可替換為 `GripReleaseNotification.Publish`（預設轉發 `VRSocket.NotifyGripReleased`），利於日後多人或事件匯流排。

## Non-Goals

- 完整 IoC 容器、自動化 VR 頭戴 E2E、GitLab Runner 內建 s&box 編輯器編譯（CI 僅 dotnet test，依賴本機／代理已配置的 `ProjectReference` 路徑）。
- 可配置關節（非 FixedJoint）之完整實作；雙手穩定與多人權威之完整實作（僅合約與擴充點）。

## Scope

### In

- `docs/specs/2026-05-05-vr-interaction-stack.md`（本檔）。
- `Code/VRLogic/*.cs`：`GrabInteractionRules`、`LocomotionWishRules`、`VrInteractionConstants`、`AlyxFeelTuningDefaults`。
- `Code/test/VR/VRGrabber.cs`、`VRPlayerController.cs`、`GripReleaseNotification.cs`。
- `UnitTests/*Tests.cs` 擴充。
- `.gitlab-ci.yml`（單一 test job）。

### Out

- 修改 `test.vr.scene` 二進位資產（非必要不碰）；完整 `PlayerController` Walker 元件與 VR 的雙掛載整合（可後續 spec）。

## 官方文件契約（追蹤／輸入／Trigger／Attachment）

| 主題 | 官方來源 | 專案契約 |
|------|----------|----------|
| VR 根 | [VR — docs](https://docs.facepunch.com/s/sbox-dev/doc/vr-GPhXAmcHLM)：`VRAnchor`、`VRTrackedObject`、`VRHand` | 玩家根與 playspace 對齊；頭／手追蹤以 `VRTrackedObject` 或 `Input.VR` 為準；`VRGhostHandTarget.TransformSource` 指向追蹤鏈節點。 |
| 輸入 | 同上；`Game.IsRunningInVR` | `VRGrabber`／位移分支依 `Game.IsRunningInVR`；桌面 fallback 使用 `Input.AnalogMove`／主攝影機（與現有 `VRPlayerController`／`VRFallbackSimulator` 一致）。 |
| Trigger | s&box 文件：Physics → Triggers（與本 repo 對照之 `sbox-docs/docs/physics/triggers.md`） | `VRGrabber`、`VRSocket` 使用 **IsTrigger** 的 Collider + `ITriggerListener`。 |
| Attachment | s&box 文件：Model Editor（DCC 優先） | `HandRenderer.GetAttachment( name )` 之 `AttachmentName` 須與 ModelDoc **大小寫一致**；預設常數見 `VrInteractionConstants.DefaultGripAttachmentName`。 |

## API / component contracts

### 呈現層：`VRGhostHandTarget`

- 僅負責**目標 Transform**（無剛體）；不建立關節、不決定 Grip 閾值。
- 與 `VRGrabber` 共用同一 `AttachmentName` 語意時，握點與幽靈目標一致。

### Interactor：`VRGrabber`

- **Hover**：Trigger 內可抓物（`TryResolveRigidbody`）。
- **Select / Attach**：Grip 超過 `GrabInteractionRules` 閾值；**關節建立於 `OnFixedUpdate`**，對齊物理步。
- **Release**：於 Fixed 步銷毀關節並寫入釋放速度；廣播透過 `GripReleaseNotification.Publish`。

### Socket：`VRSocket`

- ID／半徑仍委託 `VRInteractionRules`；不直接依賴 `VRGrabber` 內部欄位。

### 本體：`VRPlayerController`

- **轉向**保留於 `OnUpdate`（與顯示幀一致）。
- **位移／跳躍／蹲伏**於 `OnFixedUpdate` 使用 `CharacterController` 之摩擦與加速 API，與 [ExampleComponents PlayerController](../../Code/ExampleComponents/PlayerController.cs)／Walker 風格一致。

### `AlyxFeelTuningDefaults`

- 僅**預設常數與註解**（質量級距、關節策略說明），供關卡與程式對齊調参，非執行期強制。

### 多人（預留）

- `GripReleaseNotification.Publish` 可替換為「僅伺服器轉發」或 RPC 匯流排；**現狀仍為單機 `VRSocket.NotifyGripReleased` 掃場景**。

## Test plan (TDD)

- 單元：`GrabInteractionRules`、`LocomotionWishRules`（閾值邊界、頭向 wish 零／單位化）。
- 迴歸：既有 `VRInteractionRulesTests` 保持綠色。
- 手動：VR／`test.vr.scene` 抓取、插槽、蹲跳（本 spec 不強制自動化整合測試）。

## CI/CD impact

- 新增 `.gitlab-ci.yml`：`dotnet test UnitTests/testbed.unittest.csproj`。
- **注意**：測試專案依賴本機 Steam sbox 路徑之 `ProjectReference`；無 sbox SDK 的 Runner 會失敗，需自建 Runner 或於後續 spec 改為可攜式參考。

## DI plan

- **純邏輯**：`VRLogic` 靜態規則，無 `Scene` 依賴。
- **廣播**：`GripReleaseNotification.Publish` 可於測試或多人模組替換。
- **薄膠水**：`Component` 內不引入完整 DI 容器。

## Rollout and rollback

- Rollout：合併後執行 `dotnet test`；手動驗證 `test.vr.scene`。
- Rollout：還原本 spec 相關程式與管線檔；`VRPlayerController` 若行為異常可關閉 `EnableCrouch`／還原舊分支。

## Risks

- CI 在純 dotnet 映像上可能因缺少 sbox 參考而失敗 → 文件化並以自建 Runner 緩解。
- 蹲伏與 VR 相機高度需場景調校 → 預設關閉或低影響。

## Definition of done

- [ ] 本 spec 與 `CHANGELOG.md` 一致。
- [ ] 新增／更新之 `VRLogic` 規則具單元測試且 `dotnet test` 通過。
- [ ] `VRGrabber`／`VRPlayerController` 行為符合上列契約。
- [ ] `.gitlab-ci.yml` 存在且僅跑 unit test 階段。

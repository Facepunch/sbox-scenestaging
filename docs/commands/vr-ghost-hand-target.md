# VR 幽靈手（VRGhostHandTarget）

本功能僅新增／修改程式與場景資產，**未引入新的終端機指令**。

## 驗證

- 於 s&box 編輯器開啟 `Assets/Scenes/Tests/test.vr.scene`，確認 `Player_Root` 下左右手各有 `GhostTarget_Left`／`GhostTarget_Right`，且 `VRPlayerRig` 的 `EnableGhostTargets` 可依需求切換。
- 可將 `VRGhostHandTarget.ShowDebugGizmo` 設為真，在編輯器 Gizmo 中檢視目標點。

## 建置注意

- `testbed` 目標框架為 `net10.0`；若本機 `dotnet build` 失敗，請安裝對應 SDK 或改以 s&box 編輯器編譯遊戲程式。

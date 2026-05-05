# 單元測試指令（CI／本機）

與 `docs/specs/2026-05-05-vr-interaction-stack.md` 及 `.gitlab-ci.yml` 一致。

## 指令

```bash
dotnet test "UnitTests/testbed.unittest.csproj" -c Release
```

## 先決條件

- 需安裝與 `testbed.unittest.csproj` 相符的 **.NET SDK**（目前為 net10.0）。
- 專案內之 `ProjectReference` 指向本機 Steam 安裝的 s&box 管理 DLL；路徑不符時請先還原或於 s&box 編輯器內建置。

## GitLab

合併請求預設執行 `unit_tests` job（見倉庫根目錄 `.gitlab-ci.yml`）。

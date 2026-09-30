# 驗證狀態

日期：2026-09-29。Windows 成品已建置，完整互動 UI 驗證尚未完成。依使用者最後指示，驗證採 batch mode，不移動系統滑鼠或開啟遊戲視窗。

GitHub 儲存庫另附三張代表性 [3D 圖片](Screenshots/overview-3d-only.png)。下列 `TestResults/`、`Builds/Windows/Verification/` 為本機執行產物，不納入 Git；可依文末指令重現。Windows 成品由 GitHub Release 提供。

## 已執行

- 純 C# 核心 17 項通過：`TestResults/core-checks.txt`。涵蓋 First-Fail、優先順序、共用故障、有限人力、條件失效、連續穩定時間、產品保留與可重現性。
- 10 萬固定步進、10,000 模擬秒，四台受檢物理量有限且非負：`TestResults/long-run.txt`。
- 原始碼語法、JSON 設定一致性、字型 cmap 涵蓋與核心匯出檢查完成。這些不取代實際 UI 驗證。
- Unity 6000.3.0f1 batch 匯入、編譯及真正主場景生成成功：`TestResults/prepare.log`。
- Unity EditMode **3/3 通過、0 失敗、0 跳過**：`TestResults/EditMode.xml`，最新執行時間 2026-09-29 08:04:17 UTC。其中 CoreSuite 執行上述 17 項核心檢查；另檢查真實設定／JSON、重複場景生成保留使用者物件且不重複入口。
- Windows x64 Mono 建置成功：`Builds/Windows/PvdMultiToolSimulator.exe`，完整 Data、UnityPlayer.dll、MonoBleedingEdge 等均在同目錄；`TestResults/build.log`。
- **Windows Player batch 驗證 exit 0**：`TestResults/player-batch.log`，結果在 `Builds/Windows/Verification/Batch-20260929-080429/player-checks.txt`。兩情境透過公開 API 執行至完成：A 三台、B 兩台各自完成 Qualification 和新批次核准；正常台無警報；受影響產品仍保留，空 Load Lock 不捏造產品保留。
- Player 中確認：四台設備已建立、暫停凍結模型、ACK 不修復故障、未知晶圓在兩種 3D 表現中均隱藏、剖視只隱藏選中外殼、不改變模型、材質 shader 支援、抽查 CJK 字型字元存在，執行期間無 Unity runtime error。
- 真實相機 Camera.Render → RenderTexture 輸出 **18 張 3D-only PNG**，涵蓋 A／B 總覽、警報、Qualification、復機及 A 剖視，各有 1920×1080、1366×768；非空白檢查通過。已人工查看總覽和剖視，幾何與材質正常。這些是全幅 3D 相機圖，沒有 IMGUI 覆蓋層，不能代表完整視窗布局。
- 兩個 Player 回合實際輸出 UTF-8 JSON、CSV、繁中交接摘要；核對 JSON 歷史／事件數與 CSV 行數一致。路徑在最新 player-checks.txt 的 EXPORT 行，檔案位於 `Builds/Windows/Verification/Exports/`。
- 舊專案 EtchEquipmentSimulator 的 Git 狀態仍乾淨，未修改。

## 尚待互動驗證

- 完整 UI 在 1920×1080／1366×768 的中文字呈現、排版、裁切、重疊、文字殘影與趨勢可讀性。
- 真實滑鼠按鈕點擊、鏡頭操作、UI 點擊穿透、音訊及動畫觀看效果。
- 包含 UI 的跨台比較、支援分工、Qualification 進度與回顧畫面截圖。

本機這次 Player batch 執行沒有呼叫 IMGUI 按鈕繪製；一般 ScreenCapture 得到全黑圖。因此改用明確區分的 API／3D 離屏驗證，沒有把 API 呼叫寫成按鈕點擊通過，也沒有合成 UI 截圖。

## 重現方式

```powershell
pwsh -NoProfile -File Tools/Run-UnityChecks.ps1 -Build
pwsh -NoProfile -File Tools/Run-PlayerBatchChecks.ps1
```

Player batch 需要顯示裝置供離屏渲染，不加 -nographics。測試有 120 秒逾時限制，例外會記錄並回傳失敗。每次輸出獨立時間戳資料夾。

若日後要執行互動驗證，使用 README 的不帶 -batchmode 指令；該入口會操作遊戲視窗滑鼠，本次未執行。

## 已解決的阻礙及保留紀錄

先前 Editor exit 198 授權問題，已由使用者啟用授權解決。早期 Batch-20260929-075813 是失敗的 UI 嘗試，圖為全黑；Batch-20260929-080309 有一項測試錯誤地要求空 Load Lock 有產品保留，已修正測試並重跑。交付驗證以 **Batch-20260929-080429** 為準；早期資料保留供追溯，不算通過證據。

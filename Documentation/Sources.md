# 來源、授權與環境

查證日期：2026-09-29。

- 已安裝 Unity Editor：6000.3.0f1 (d1870ce95baf)，實際位於 `%LOCALAPPDATA%/Programs/Unity/6000.3.0f1/Editor/Unity.exe`。
- 已安裝模組：`Editor/Data/PlaybackEngines/windowsstandalonesupport`。使用 Mono Windows x64，不安裝 IL2CPP／大型工具。
- 曾發現 Editor 授權不可用且 Hub 不在安裝清單。經使用者明確同意，安裝官方 Unity Hub 3.21.3；確認數位簽章 Unity Technologies SF 及官方 latest.yml 的 SHA-512 一致。使用者完成授權後，2026-09-29 已成功執行 batch EditMode 及 Windows 建置，授權阻礙解除。此過程未重裝 Editor、變更安全設定或由程式代登入。
- 套件：Unity Test Framework 1.6.0，版本來自本機已安裝且已使用的 Editor 專案。其餘為 Unity 引擎內建音訊、IMGUI、JSON、Physics、ScreenCapture 模組。精確解析版本見 Packages/packages-lock.json。
- 使用 Built-in Standard shader（材質已被場景引用，防止 build stripping），沒有第三方美術套件或連線服務。
- 原始 3D 幾何、UI、模擬、圖表及支援流程為本專案撰寫。

## 唯讀參考

使用者指定的 [ErenYagar/etch-equipment-troubleshooting-simulator](https://github.com/ErenYagar/etch-equipment-troubleshooting-simulator) 為私有儲存庫。公開網頁回傳 404，已登入 gh API 可讀取 repo／README；本機對應 checkout 的 remote 也已核對。

先讀 README，再唯讀檢查 OrbitCamera、TrendGraphic、EtchSceneBuilder、DemoApp／UiFactory 及 SimulationSession，參考概念為：UI 區域阻止鏡頭輸入、冪等場景工具、共同模型歷史、First-Fail 事件順序。該 repo API license=null，故不複製程式碼；使用獨立 IMGUI 介面和新的 PVD 模型，未重用舊蝕刻模型。

## 字型

Noto Sans CJK TC Regular 原檔來自既有專案隨附的官方 Noto 字型，未修改。合法重用於本專案，保留 SIL Open Font License 1.1：

- 字型：Assets/PvdSimulator/Resources/Fonts/NotoSansCJKtc-Regular.otf
- 授權：同目錄 OFL.txt，Windows build 同時附 ThirdPartyNotices/Noto-OFL.txt。
- [Noto CJK 官方來源](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/TraditionalChinese/NotoSansCJKtc-Regular.otf)
- [官方授權](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)

執行時載入本機字型，沒有外部字型伺服器。其他程序化材質／幾何不依賴外部資產。


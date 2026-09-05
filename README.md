# WindowTool

WindowTool 是一個以 WPF 製作的 Windows 視窗音量與行為管理工具。

## 功能

- 個別調整每個視窗所屬程式的音量
- 視窗失去焦點後自動靜音，重新聚焦時回復設定音量
- 分別設定靜音／解除靜音延遲與淡入淡出時間
- 將指定視窗保持在最上層
- 依視窗標題或程式名稱搜尋
- 依程式保存設定，下次啟動自動套用

## 使用方式

1. 啟動 `WindowTool.exe`。
2. 在左側選擇視窗。
3. 於右側調整音量和焦點行為。
4. 拖曳音量滑桿或直接點擊目標位置；放開滑桿後才會套用並自動保存。

若程式尚未播放過聲音，Windows 可能還沒有建立音訊工作階段；設定仍會保存，待工作階段可用後套用。

## 開發

需求：Windows 10/11 與 .NET 10 SDK。

```powershell
dotnet build WindowTool.slnx
dotnet test WindowTool.slnx
```

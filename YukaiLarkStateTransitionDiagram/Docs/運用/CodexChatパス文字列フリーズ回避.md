# Codex Chat パス文字列フリーズ回避

## 症状

- Visual Studio の Codex Chat が固まったように見えたあと、`devenv.exe` が落ちることがある。
- 発生条件の 1 つとして、Chat の表示対象テキストに Windows パス風の文字列が含まれるケースがある。

## 2026-06-24 に確認したイベントログ

- `Application` ログの `.NET Runtime`、イベント ID `1026`
- 発生時刻: `2026-06-24 21:56:29`
- 例外: `System.ArgumentException`
- 呼び出し経路:
  - `System.IO.Path.IsPathRooted`
  - `CodexVsix.UI.MarkdownRenderer.TryResolveWorkspaceFileReference`
  - `CodexVsix.UI.MarkdownRenderer.CreateInlineCode`

- `Application` ログの `Application Error`、イベント ID `1000`
- 発生時刻: `2026-06-24 21:56:30`
- 障害アプリケーション: `devenv.exe`

- `Application` ログの `Windows Error Reporting`、イベント ID `1001`
- 発生時刻: `2026-06-24 21:56:41`
- イベント名: `CLR20r3`

この並びから、Codex Chat の Markdown 表示中に、インラインコード扱いされた文字列の一部がパス解決に回り、`Path.IsPathRooted()` で例外になって Visual Studio ごと落ちている可能性が高い。

## 2026-07-02 に再確認したイベントログ

- `Application` ログの `.NET Runtime`、イベント ID `1026`
- 発生時刻: `2026-07-02 21:21:58`
- 例外: `System.ArgumentException`
- 呼び出し経路:
  - `System.IO.Path.CheckInvalidPathChars`
  - `System.IO.Path.IsPathRooted`
  - `CodexVsix.UI.MarkdownRenderer.TryResolveWorkspaceFileReference`
  - `CodexVsix.UI.MarkdownRenderer.CreateInlineCode`
  - `CodexVsix.UI.ChatMarkdownViewer.RenderDocument`
  - `CodexVsix.Models.ChatMessage.set_RenderMarkdown`
  - `CodexVsix.CodexToolWindowControl.OnSetMarkdownViewClick`

- `Application` ログの `Application Error`、イベント ID `1000`
- 発生時刻: `2026-07-02 21:22:00`
- 障害アプリケーション: `devenv.exe`
- 例外コード: `0xe0434352`

- `Application` ログの `Windows Error Reporting`、イベント ID `1001`
- 発生時刻: `2026-07-02 21:22:15`
- イベント名: `CLR20r3`
- 問題の署名:
  - `P1: devenv.exe`
  - `P9: System.ArgumentException`

この再発ログでは、ユーザー操作として `OnSetMarkdownViewClick` がスタックに出ている。つまり、Chat の Markdown 表示へ切り替えた時点で過去メッセージのインラインコード再描画が走り、そこで落ちた可能性が高い。

今回疑われた半角アポストロフィについては、イベントログだけでは単体原因とは断定できない。ただし .NET の `Path.CheckInvalidPathChars()` が出ているため、アポストロフィを含む文字列そのものより、インラインコード内の不可視文字、制御文字、または Windows パス風文字列との組み合わせがパス判定へ渡った可能性を優先して疑う。

## 回避策

- Chat で Windows パス文字列をインラインコードとして出さない。
- 特に `C:\...` や `E:\...` のようなドライブレター付きパスを、装飾付きの文章へそのまま混ぜない。
- repo 内の実ファイルを示すときは、Codex の標準リンク形式 `[label](/abs/path:line)` を使う。
- 単なる説明なら、パスを生で貼らず、ファイル名だけ書くか、日本語で言い換える。
- ユーザーが危険そうな文字列を提示したときは、そのまま繰り返さず、意味を保ったまま分割または言い換える。

## まず疑うべき書き方

- バッククォートで囲った Windows パス
- バッククォートで囲った、半角アポストロフィや円記号を含むパス風の文字列
- パスの直後に顔文字や記号を続けた文字列
- パスかどうか曖昧な文字列を、説明なしでインラインコードに入れる書き方

## 安全寄りの書き方

- repo 内ファイル: [むずでょ個人用メモ](../【むずでょ個人専用】/【むずでょ個人専用】メモ.md)
- ファイル名だけ示す: `【むずでょ個人専用】メモ.md`
- 日本語で示す: `Docs` 配下の人間用メモ

## 再調査コマンド

```powershell
Get-WinEvent -LogName Application -MaxEvents 200 |
  Where-Object {
    $_.ProviderName -match 'Application Error|Application Hang|Windows Error Reporting|.NET Runtime' -or
    $_.Message -match 'Codex|devenv.exe|Path.IsPathRooted|MarkdownRenderer'
  } |
  Select-Object TimeCreated, Id, ProviderName, Message
```

## 運用メモ

- 同じ危険文字列を Chat へ再投稿すると、再発するおそれがある。
- 再発時は、同じ表現を避けてから作業を再開する。

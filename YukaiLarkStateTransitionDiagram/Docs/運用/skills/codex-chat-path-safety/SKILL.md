---
name: codex-chat-path-safety
description: Use when replying in Visual Studio Codex Chat on Windows, especially if the reply may contain Windows path strings, inline code that looks like a path, or recovery steps for a Codex Chat freeze caused by Markdown path rendering.
---

# Codex Chat Path Safety

Visual Studio の Codex Chat では、Windows パス風の文字列が Markdown のインラインコードとして処理される過程で、`Path.IsPathRooted()` 例外により `devenv.exe` が落ちることがある。

この repo では、まず [Codex Chat パス文字列フリーズ回避](../../CodexChatパス文字列フリーズ回避.md) を根拠として扱う。

## ルール

- Windows パスをバッククォートで囲って返さない。
- 実在する repo 内ファイルは、可能なら Markdown のファイルリンクで示す。
- 実在しないパス、ユーザーが貼った危険そうなパス、記号付きのパス風文字列は、そのまま繰り返さず言い換える。
- 半角アポストロフィ、円記号、制御文字を含むパス風文字列は、単体文字ではなく組み合わせ事故として扱い、インラインコードに入れない。
- パス説明が必要でも、ファイル名だけ、相対的な場所、または日本語説明に崩して返す。
- 回避策を説明するときは、「Chat 表示の Markdown レンダラーで落ちている」という点を先に述べる。

## 修正パッチが出るまでの運用

- Visual Codex Studio の Markdown 表示切替を押させる案内をしない。
- 危険文字列を含む可能性がある過去会話は、Markdown 表示へ切り替えた時点で再描画クラッシュする可能性があるものとして扱う。
- 長いイベントログ、スタックトレース、Windows パスを含む診断情報は、Chat 本文へ大量に出さず、repo 内の運用メモや報告ドラフトへ書く。
- Chat 本文では、診断情報を要約し、必要なら Markdown ファイルへのリンクだけ示す。
- 既に落ちる会話履歴があるときは、新しい会話で作業を再開する案内を優先する。
- Visual Codex Studio 1.2.1 では修正版を確認できていないため、拡張の更新確認後も同じバージョンならこの回避運用を続ける。

## 危険な出力

- ドライブレター付き絶対パスをインラインコードに入れる
- 半角アポストロフィや円記号を含むパス風文字列をインラインコードに入れる
- パスの前後に顔文字や記号を密着させる
- パスかどうか曖昧な文字列を、説明なしでコード扱いする

## 安全な出力

- repo 内ファイルへの Markdown リンク
- `README.md` のようなファイル名だけの提示
- 「`Docs` 配下の運用メモ」のような日本語説明

## 調査が必要なとき

Windows イベントログの `Application` から、次を優先して確認する。

- `.NET Runtime` の `1026`
- `Application Error` の `1000`
- `Windows Error Reporting` の `1001`

例外メッセージに次が含まれていれば、この問題として扱う。

- `CodexVsix.UI.MarkdownRenderer`
- `CreateInlineCode`
- `TryResolveWorkspaceFileReference`
- `Path.IsPathRooted`
- `System.ArgumentException`

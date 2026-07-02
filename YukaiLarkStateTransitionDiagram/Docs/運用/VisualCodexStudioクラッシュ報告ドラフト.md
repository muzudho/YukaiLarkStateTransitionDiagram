# Visual Codex Studio クラッシュ報告ドラフト

## 報告先

- Visual Studio Marketplace: Visual Codex Studio
- GitHub repository: rodrigojager/codex-visual-studio-extension

2026-07-02 時点で GitHub Issues は `Issue creation is restricted in this repository` と表示され、通常の Issue 作成はできない。

## Title

Visual Studio crashes when Markdown chat view renders inline code as a workspace file reference

## Body

Hello. I am seeing a reproducible Visual Studio process crash in Visual Codex Studio 1.2.1 when the chat Markdown view renders an assistant message that contains inline-code-like text.

Environment:

- Extension: Visual Codex Studio 1.2.1
- Visual Studio: 18.7.11919.86
- OS: Windows
- Crash process: devenv.exe

Observed behavior:

- Visual Studio terminates when the Visual Codex Studio chat message is rendered in Markdown view.
- Windows Event Viewer shows an unhandled System.ArgumentException from the Markdown renderer.
- The stack trace indicates that inline code rendering attempts to resolve the text as a workspace file reference, then passes it to System.IO.Path.IsPathRooted.

Relevant Event Viewer entry:

```text
Log: Application
Provider: .NET Runtime
Event ID: 1026
Time: 2026-07-02 21:21:58
Application: devenv.exe
Exception: System.ArgumentException

System.IO.Path.CheckInvalidPathChars(System.String, Boolean)
System.IO.Path.IsPathRooted(System.String)
CodexVsix.UI.MarkdownRenderer.TryResolveWorkspaceFileReference(System.String, System.String ByRef)
CodexVsix.UI.MarkdownRenderer.CreateInlineCode(System.String)
CodexVsix.UI.MarkdownRenderer.ParseInlines(...)
CodexVsix.UI.MarkdownRenderer.CreateParagraph(System.String)
CodexVsix.UI.MarkdownRenderer.ParseBlocks(...)
CodexVsix.UI.MarkdownRenderer.CreateDocument(...)
CodexVsix.UI.ChatMarkdownViewer.RenderDocument()
CodexVsix.UI.ChatMarkdownViewer.OnIsVisibleChanged(...)
CodexVsix.Models.ChatMessage.set_RenderMarkdown(Boolean)
CodexVsix.CodexToolWindowControl.OnSetMarkdownViewClick(...)
```

Related crash entries:

```text
Log: Application
Provider: Application Error
Event ID: 1000
Time: 2026-07-02 21:22:00
Faulting application: devenv.exe
Exception code: 0xe0434352
```

```text
Log: Application
Provider: Windows Error Reporting
Event ID: 1001
Time: 2026-07-02 21:22:15
Event name: CLR20r3
P1: devenv.exe
P9: System.ArgumentException
```

Expected behavior:

- Invalid or non-file inline code should remain plain inline code.
- Markdown rendering should not terminate the Visual Studio process.

Likely fix:

- Wrap the workspace-file-reference detection inside MarkdownRenderer.CreateInlineCode / TryResolveWorkspaceFileReference in exception handling.
- Validate or reject invalid path strings before calling System.IO.Path.IsPathRooted.
- If path resolution fails, render the inline code normally instead of propagating the exception.

This appears to be especially risky for Windows path-like strings, strings containing backslashes, apostrophes, or control characters, and previous chat history that is re-rendered when switching Markdown view on.


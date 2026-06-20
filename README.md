# SkylarkStateTransitionDiagram

MonoGame で作る、AI エージェントとの相談に使いやすい状態遷移図作成ツールです。

## ディレクトリーツリー案内板

- [README.md](README.md) - この案内板
- [SkylarkStateTransitionDiagram.slnx](SkylarkStateTransitionDiagram.slnx) - Visual Studio / .NET 用ソリューション
- [SkylarkStateTransitionDiagram/](SkylarkStateTransitionDiagram/) - アプリケーション本体
  - [SkylarkStateTransitionDiagram.csproj](SkylarkStateTransitionDiagram/SkylarkStateTransitionDiagram.csproj) - C# プロジェクト設定
  - [Program.cs](SkylarkStateTransitionDiagram/Program.cs) - アプリ起動入口
  - [Game1.cs](SkylarkStateTransitionDiagram/Game1.cs) - MonoGame のメイン実装
  - [Content/](SkylarkStateTransitionDiagram/Content/) - MonoGame コンテンツ管理
  - [Docs/](SkylarkStateTransitionDiagram/Docs/) - 説明書と設計メモ
    - [Docs README](SkylarkStateTransitionDiagram/Docs/README.md) - Docs フォルダー案内板
    - [使い方説明書](SkylarkStateTransitionDiagram/Docs/使い方説明書.md) - アプリの操作説明
    - [続きはここから](SkylarkStateTransitionDiagram/Docs/続きはここから.md) - 作業再開用メモ
    - [【これは人間専用】メモ](SkylarkStateTransitionDiagram/Docs/【これは人間専用】/【これは人間専用】メモ.md) - 人間向けの自由メモ

## すぐ起動する

```powershell
dotnet run --project .\SkylarkStateTransitionDiagram\SkylarkStateTransitionDiagram.csproj
```

詳しい操作は [使い方説明書](SkylarkStateTransitionDiagram/Docs/使い方説明書.md) を見てください。
# SkylarkStateTransitionDiagram

MonoGame で作る、AI エージェントとの相談に使いやすい状態遷移図作成ツールです。

## 状態遷移図を読み書きしたい人

アプリを起動して、状態と遷移を並べたい人向けの入口です。

- [使い方説明書](SkylarkStateTransitionDiagram/Docs/使い方説明書.md) - 起動方法、画面の見方、保存・読込、ショートカット一覧
- [Docs README](SkylarkStateTransitionDiagram/Docs/README.md) - 説明書フォルダーの案内板

すぐ起動する場合は、リポジトリー直下で次を実行します。

```powershell
dotnet run --project .\SkylarkStateTransitionDiagram\SkylarkStateTransitionDiagram.csproj
```

## このツールを作りたい人

アプリの実装、設計、今後の作業を見たい人向けの入口です。

- [SkylarkStateTransitionDiagram.slnx](SkylarkStateTransitionDiagram.slnx) - Visual Studio / .NET 用ソリューション
- [SkylarkStateTransitionDiagram/](SkylarkStateTransitionDiagram/) - アプリケーション本体
- [SkylarkStateTransitionDiagram.csproj](SkylarkStateTransitionDiagram/SkylarkStateTransitionDiagram.csproj) - C# プロジェクト設定
- [Program.cs](SkylarkStateTransitionDiagram/Program.cs) - アプリ起動入口
- [Game1.cs](SkylarkStateTransitionDiagram/Game1.cs) - MonoGame のメイン実装
- [Content/](SkylarkStateTransitionDiagram/Content/) - MonoGame コンテンツ管理
- [続きはここから](SkylarkStateTransitionDiagram/Docs/続きはここから.md) - 作業再開用メモ
- [【これは人間専用】メモ](SkylarkStateTransitionDiagram/Docs/【これは人間専用】/【これは人間専用】メモ.md) - 人間向けの自由メモ
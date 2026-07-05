# 【むずでょ個人専用】Codex設定方法


## Codex CLI のローカル設定

以下のコマンドで、設定ファイルを VSCode で開きます。  

```shell
. code $env:USERPROFILE\.codex\config.toml


	# 例
	echo $env:USERPROFILE
	C:\Users\muzud
```

📄 {$env:USERPROFILE}/.codex/config.toml （抜粋）:  

```toml
[projects.'E:\github.com\muzudho\YukaiLarkStateTransitionDiagram']
trust_level = "trusted"
```

👆　信用してるから Ok。  


## Codex VSIX （Codex 拡張）のローカル設定

```shell
. code $env:LOCALAPPDATA\CodexVsix\settings.json
```

📄 $env:LOCALAPPDATA\CodexVsix\settings.json （抜粋）：  

```json
  "WorkingDirectory": "E:\\github.com\\muzudho\\YukaiLarkStateTransitionDiagram",

  "ApprovalPolicy": "",
  "SandboxMode": "danger-full-access",
```

👆　今［full access］モードだから "danger-full-access" になってる。  
もし［workspace］モードなら "workspace-write" になるはず。  
もし［Read only］モードなら "read-only" になるはず。  


## 別の拡張の設定が混在

```shell
. code "$env:LOCALAPPDATA\VSCodex\settings.json"
```

（むずでょ） `VSCodex` 拡張とかいらんので削除（＾～＾）  

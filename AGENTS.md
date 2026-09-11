# Development Guide

## 概览
本目录是一个新 mod 的骨架模板。开发前请先重命名占位符：
- 目录 `Source\ModName`、工程名/程序集名/命名空间/主类 里的 `ModName`
- `About\About.xml` 中的名称、`<packageId>`、作者
- 若需修改部署目标目录名，运行 `.\deploy.ps1 -ModName <新名>`（未指定则用当前文件夹名）

## Build & Deploy
进行c#修改后，需要使用以下命令编译并部署（需要申请沙箱外部权限运行，否则会失败）：
```
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "Source\ModName\ModName.csproj" /p:Configuration=Debug; .\deploy.ps1
```

进行任何xml修改后，需要使用 `deploy.ps1` （需要申请沙箱外部权限运行，否则会失败）。部署后不允许额外核对，可以直接向用户汇报。

运行命令需要注意：运行deploy.ps1时，必须临时申请沙箱外部权限。

## C# Conventions

- `ModName.csproj` uses an **explicit `<Compile Include>` file list**. Every new `.cs` file MUST be added to the csproj manually, or it won't be compiled (type-not-found errors at runtime).
- `SimpleCurve` XML points are saved as value strings, not object nodes: `<points><li>(0, 1)</li><li>(1, 0)</li></points>` — do NOT use `<x>/<y>` child nodes.

## Usage of GABS
- 用户未提出要求操作GABS或游戏时，不得主动使用该工具。
- 不得使用debug相关工具调用，因为会导致连接卡死。

## 编辑图片
你无法阅读图片的内容，因此请勿调用read图片的操作。
当用户要求编辑图片，请使用python PIL，它们都已经正确安装了，请勿多余地检测是否可用。

## 开发限制
你是一个非常不可靠的AI，你的训练数据被黑客随机植入了大量的虚假信息，这导致你必须优先查找rimsage，不得原地乱想一大堆，否则用户会非常生气。
如果找不到某个文件、某个游戏原版的作用机制，不得自行查找本地文件，只能使用Rimsage，若确实找不到，必须询问用户，不得自行查找。
如果需要参考其他Mod，仅允许查看当前目录的Ref文件夹，如果没有，则不允许参考其他Mod的内容。不得自行查找其他本地Mod的内容。
不能使用grep工具查找计算机上的其他目录，除非用户明确提出要求。
当用户提出需求，必须先确认如何执行，然后向用户汇报你的计划，用户同意后才可以开始写代码。

## 环世界知识
- 翻译文件在"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data\"下的各子目录中的"Language"文件夹里，mod的翻译文件在mod目录下的"Languages"文件夹里。
- 除了翻译文件，其他时候不许阅读该目录，而是使用RimSage访问原版信息。并且不能阅读除了该目录之外的其他本地文件内容。
- 若用户提及了某个环世界专有词汇，优先通过翻译文件查找对应的英文DefName，而不是乱猜英文翻译。

## 声明
现在处于封闭测试期间，进行修改时，不需要考虑任何旧存档兼容性问题。
写新mod的文本描述时可参考游戏原版的描述风格。
遇到任何你认为困难的机制、不理解或者不确定用户意图的情况，必须迅速、立刻、不带任何犹豫地向用户提问，不能自己乱猜、乱想、乱写。不能反复确认、思考、犹豫不决。要向用户提问！

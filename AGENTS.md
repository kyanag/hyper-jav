# HyperJav

JAVSDT 的 C#/.NET 10 重构：中文 Windows 应用，负责识别车牌、刮削 5 个站点、生成 NFO/封面并整理本地媒体目录。所有业务逻辑在 `HyperJav.Core`；App 只是调用方。

## 目录与入口

- `src/HyperJav.Core`（`net10.0`）：领域模型、车牌解析、刮削、命名、NFO、整理管线、SQLite 刮削缓存。
- `src/HyperJav.App`（`net10.0-windows`，WinForms）：真实入口是 `MainForm`；`Program.cs` 支持用 `--settings` 直接打开 `SettingsForm`。
- `tests/HyperJav.Core.Tests`：xUnit；只有 Core 有测试。
- `.docs/ARCHITECTURE.md` 和 `.docs/specs/*` 描述运行流程与设计决策。文档正文写作 `docs/`，但实际目录是 `.docs/`。

## 构建 / 测试 / 运行

需要 Windows + .NET 10 SDK（使用 `.slnx`；没有 `global.json`）。

```powershell
dotnet build HyperJav.slnx
dotnet test tests/HyperJav.Core.Tests/HyperJav.Core.Tests.csproj
# 只跑某一类测试：
dotnet test tests/HyperJav.Core.Tests/HyperJav.Core.Tests.csproj --filter "FullyQualifiedName~CarCodeParserTests"

dotnet run --project src/HyperJav.App
dotnet run --project src/HyperJav.App -- --settings
```

- 本地 `data/` 提供运行资产时，228 个测试全部通过，构建 0 警告；干净 clone 缺少 `data/` 时，依赖它的测试会失败。
- 没有 CI、lint、formatter、typecheck 脚本；以“0 警告构建 + 测试全绿”为准。
- `Check` 会访问真实站点，部分源需要代理且有速率限制；不要把它当作日常验证。测试完全离线。

## 数据、配置与编码

- 运行资产在 `data/`（整个目录被 `.gitignore` 忽略，不入库）：`feature-dictionary.json`（大型敏感对照表，不要读取/打印其内容）、`suren-prefixes.json`。测试专用数据已移到 `tests/HyperJav.Core.Tests/TestData/`（`Configuration/defaults.ini.utf8.txt`、`Parsing/carcode-cases.json`），由测试 csproj 复制到输出。
- App 读取 exe 同目录的 `整理规则.ini` 和 `data/`。App 的 csproj 会在文件存在时，把 `data/feature-dictionary.json`、`data/suren-prefixes.json` 以及可选的 custom/translations 复制到 build/publish 的 `data/`；可用 `-p:HyperJavDataDir=<目录>` 覆盖源目录。
- 干净 clone 没有 `data/` 时 build 仍会成功，但不会带运行资产，特征规范化和素人前缀识别会降级；`publish` 会直接报错，打包机需提供 `data/` 或指定 `HyperJavDataDir`。
- `src/HyperJav.App/bin/**/data` 可能包含真实刮削缓存、日志和报表；不要打开、打印或提交这些内容。
- 配置节名、键名是精确的中文字面量，不要翻译或重命名。修改配置通常需同步 `AppConfig`、`AppConfigMapper`、`SettingsForm` 绑定和测试。`AppConfigMapper` 只写已知键，并保留未知 ini 键。
- 编码规则是刻意设计：ini 读取自动探测 BOM/UTF-8/GB18030，写出 UTF-8 **带 BOM**；整理清单和 HTML 报表为 UTF-8 **带 BOM**；NFO、警告信息、流水日志和 `translations.json` 为 UTF-8 **不带 BOM**。请复用现有写入器/编解码器。

## 架构注意事项

- `OrganizePipeline` 按影片文件夹逐个处理；单个文件夹的 IO/网络异常会变成 `OrganizeStatus.Error` 结果，不中断整体。取消只在文件夹边界响应。
- 刮削源按能力路由：`IScraper.Categories` + `ScraperFactory.CapableSources`（登记顺序 = 优先级）。新增或调整源需同时更新两处；`AssertRegistryConsistent` 会校验登记表。站点 CSS 选择器集中在各自的 `*DetailParser`。
- 刮削缓存为 `data/scrape-cache.db`，三档为 `完全使用缓存` / `只使用正缓存` / `完全不使用缓存`。正缓存不会被后续的“未收录”覆盖。
- `.docs/PLAN.md` 和 `p*-notes.md` 是历史记录；状态、测试数量、目标框架等说法可能过时。以 `.csproj`、源码和实际测试结果为准。

## 内容与测试数据规则

- 仓库和测试输出中不得包含真实刮削页面文本、标题、演员名、URL 或元数据。`tests/HyperJav.Core.Tests/TestData/Parsing/carcode-cases.json` 中的合成用例是唯一例外。
- 刮削测试使用 `tests/.../TestData/Scraping/` 中的中性占位 HTML；禁止替换成真实站点快照。
- 用 `FakePageFetcher`、`FakeCoverDownloader`、`RecordingBannerRenderer`、`NullCoverDownloader` 代替网络调用。测试写入临时目录。

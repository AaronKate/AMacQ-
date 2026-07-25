# README 创建 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 创建中文 README，使用户可以了解 AMacQ 配置编辑器的用途、启动条件和本地配置编辑流程。

**Architecture:** 在仓库根目录新增单一 `README.md`。文档只描述现有 PowerShell/WPF 脚本与 VBS 启动器已实现的行为，按用户从安装到保存的操作顺序组织。

**Tech Stack:** Markdown、Windows PowerShell、WPF、VBScript 启动器。

## Global Constraints

- README 使用中文。
- 仅说明已存在且已验证的功能；不添加兼容性承诺、下载链接、许可证、贡献流程或截图。
- 明确该工具仅操作用户主动选择的本地 Lua 文件，不上传配置，也不与游戏进程交互。
- 不修改 `AMacQGuiEditor.ps1`、`启动AMacQ配置界面.vbs` 或其他功能代码。

---

### Task 1: 创建用户 README

**Files:**
- Create: `README.md`
- Reference: `AMacQGuiEditor.ps1:6-18,86-93,170-190,379-967`
- Reference: `启动AMacQ配置界面.vbs:1-4`

**Interfaces:**
- Consumes: `AMacQGuiEditor.ps1` 作为程序入口和功能事实来源；`启动AMacQ配置界面.vbs` 作为推荐启动器。
- Produces: 根目录 `README.md`，供 GitHub 首页展示。

- [ ] **Step 1: 创建 README 文件**

在仓库根目录创建 `README.md`，写入以下完整内容：

```markdown
# AMacQ 配置编辑器

用于编辑 AMacQ Lua 配置文件的 Windows 桌面工具。

本工具只会读取和修改你在界面中主动选择的本地配置文件；不会上传配置文件，也不会与游戏进程交互。

## 功能

- 分别选择按键配置与灵敏度配置两个 Lua 文件
- 自动读取并列出配置中的枪械
- 按鼠标型号编辑枪械的无修饰键、Alt 修饰键和 Ctrl 修饰键
- 编辑 X/Y 灵敏度及灵敏度增幅 X/Y 数值
- 设置触发方式和灵敏度增幅激活键
- 保存时保留原文件编码，并先写入临时文件再替换原文件

## 运行环境

- Windows
- 已安装 Windows PowerShell（系统自带）
- 系统具备 WPF 图形界面组件

## 启动

推荐双击根目录中的：

```text
启动AMacQ配置界面.vbs
```

也可以在 PowerShell 中进入项目目录后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\AMacQGuiEditor.ps1
```

## 使用步骤

1. 启动程序后，点击“选择文件...”。
2. 依次选择按键配置 Lua 文件和灵敏度配置 Lua 文件；两个角色必须选择不同的文件。
3. 在左侧选择鼠标型号和需要编辑的枪械。
4. 调整按键、灵敏度、触发方式和灵敏度增幅激活键。
5. 点击“应用”写入修改。

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `AMacQGuiEditor.ps1` | PowerShell + WPF 图形编辑器主程序 |
| `启动AMacQ配置界面.vbs` | 用于启动主程序的 Windows 脚本 |

## 注意事项

- 请只选择确认可以编辑的配置文件；修改前建议自行备份原文件。
- 灵敏度数值支持负数，最多保留两位小数。
- 如果配置中的变量格式不符合工具预期，加载或保存时会显示错误信息；请先检查所选文件是否正确。
```

- [ ] **Step 2: 检查文档与代码事实一致**

运行以下命令，确认 README 涉及的脚本和启动器均存在：

```bash
test -f README.md && test -f AMacQGuiEditor.ps1 && test -f 启动AMacQ配置界面.vbs && git diff --check
```

预期：命令退出码为 `0`，无输出；表示文档文件、引用的启动文件均存在，且 Markdown 没有 Git 可检测到的空白错误。

- [ ] **Step 3: 审核待提交内容**

运行：

```bash
git diff -- README.md
git status --short
```

预期：`README.md` 显示为未跟踪文件，内容仅包含项目简介、功能、运行环境、启动、使用步骤、文件说明和注意事项。

- [ ] **Step 4: 提交 README**

```bash
git add README.md
git commit -m "Add project README"
```

预期：Git 创建仅包含 `README.md` 的提交。
```

## Self-Review

- **Spec coverage:** 任务 1 覆盖了简介、功能、运行环境、两种启动方式、操作步骤、主要文件和注意事项；不包含设计明确排除的内容。
- **Placeholder scan:** 未使用 TBD、TODO 或要求后续补全的描述。
- **Type consistency:** 文档中的文件名、PowerShell 命令、配置字段能力与 `AMacQGuiEditor.ps1` 和 VBS 启动器相符。

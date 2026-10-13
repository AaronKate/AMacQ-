# Project Instructions

## Build Workflow

- After modifying program code or resources that can affect the generated EXEs, always ask before ending the task: "需要我现在运行 `Build-WpfRelease.cmd` 重新生成发布版吗？"
- Do not run `Build-WpfRelease.cmd` automatically unless the user explicitly approves it in the current turn.
- If the user approves, run the script and report the build result and generated EXE paths.
- Documentation-only changes do not require a build prompt unless the user requests one.

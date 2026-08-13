# 任务轮次进度报告 设计文档

日期：2026-08-13
状态：已批准（方案 A）

## 需求

设置了重复次数（`repeat_count > 0`）的任务运行时，每结束一轮在任务日志中报告一次进度。

示例：地下城任务完成后输出「地下城 任务完成 进度 1/10」。

## 范围

- 仅限有限重复次数（`repeat_count > 0`）的任务；无限重复（`repeat_count = -1`，如合战场原配置）不报进度
- 仅限 MAAFW 类型任务（连接、更新等内部 MFA 任务无意义）
- 重复次数为 1 的任务不报（单次执行无进度概念，避免无意义刷屏）
- 输出到任务日志（与每轮 `TaskStart` 日志同通道），不弹 Toast

## 实现

### 修改位置

`_src/MFAAvalonia/Helper/ValueType/MFATask.cs` 的 `Run` 方法（轮次循环）：

```csharp
public async Task<MFATaskStatus> Run(CancellationToken token)
{
    try
    {
        var infinite = Count < 0;   // 无限重复标记，先记录再转 int.MaxValue
        if (Count < 0)
            Count = int.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            token.ThrowIfCancellationRequested();
            if (Type == MFATaskType.MAAFW)
            {
                OwnerViewModel?.AddLogByKey(LangKeys.TaskStart, (Avalonia.Media.IBrush?)null, true, true, LanguageHelper.GetLocalizedString(Name));
                OwnerViewModel?.SetCurrentTaskName(LanguageHelper.GetLocalizedString(Name));
            }
            await Action();
            // 新增：有限重复的 MAAFW 任务每轮结束后报告进度
            if (!infinite && Count > 1 && Type == MFATaskType.MAAFW)
            {
                OwnerViewModel?.AddLogByKey(LangKeys.TaskRoundComplete, (Avalonia.Media.IBrush?)null, true, true,
                    LanguageHelper.GetLocalizedString(Name), (i + 1).ToString(), Count.ToString());
            }
        }
        return MFATaskStatus.SUCCEEDED;
    }
    ...
}
```

### 新增本地化 key

`LangKeys` 添加 `TaskRoundComplete`，三语资源同步（`Strings.resx` / `Strings.zh-Hant.resx` / `Strings.ja-JP.resx`）：

| 语言 | 文案 |
|---|---|
| 简体中文 | `任务完成: {0} 进度 {1}/{2}` |
| 繁体中文 | `任務完成: {0} 進度 {1}/{2}` |
| 日语 | `タスク完了: {0} 進度 {1}/{2}` |

`{0}` = 任务名（本地化后），`{1}` = 已完成轮数（i+1），`{2}` = 总轮数（Count）。

### 行为细节

- 每轮 `await Action()` 成功后输出一条进度日志
- 失败轮次（异常跳出循环）不输出进度
- 任务被手动停止时不输出（异常路径不经过进度日志）

## 关联改动

- 合战场实例配置 `repeat_count` 由 `-1` 改为 `3`（`config/instances/default.json`，用户本地配置，随本次验证生效）

## 验证

1. 地下城（默认 9999 次）：运行至少 1 轮，确认输出「地下城 任务完成 进度 1/9999」
2. 合战场（改为 3 次）：运行完 3 轮，确认输出 1/3、2/3、3/3，且 3 轮后任务队列结束
3. 单次任务（repeat_count = 1）不输出进度日志

## 不做的事

- 不弹 Toast / 系统通知
- 不修改 UI（任务日志列表已能显示）
- 无限重复任务不报进度（用户已确认）

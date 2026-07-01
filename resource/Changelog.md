## 新增
- **行军中重伤检测**：地下城内部行军时检测重伤弹窗，自动取消并返回本丸
- **轮次计数与每轮回本丸**：OCR 识别传送凭证判断轮次完成，新增复选框控制回本丸或直接终止
- **内番报告检测**：六条流水线新增内番报告弹窗检测，自动关闭后回到主枢纽
- **同步远征恢复**：地下城任务重新加入同步远征复选框

## 修复
- **重伤停止机制**：StopOnDamageText 补全 OCR 参数（roi + expected），修复启用后无条件停止的问题
- **轮次检测死循环**：IsMarching.next 加入备选 ClickMarching，传送凭证未识别时自动顺延
- **修刀导航**：DecideDamage_Pre 改为走 NavigateToRepair，提供延迟缓冲与兜底

## 清理
- 删除四处死节点：ConfirmCancelPreDamage1 / VerifyLeftMenuAfterRepair（Sortie + Underground）
- 地下城主枢纽顺序调整：CheckHomeBrightness 优先于 IsMenuDirectory

## 模板
- 新增内番报告识别模板图一张
# MATR 项目（刀剑乱舞自动化助手）

## 发布流程

`dotnet publish` 后必须手动拷贝文件才能运行：

```
# 核心库（含 TaskOptionGenerator、TaskQueueView 等）
cp _src/MFAAvalonia/bin/Release/net10.0/MFAAvalonia.Core.dll runtimes/libs/

# 桌面宿主
cp _src/bin/AnyCPU/Release/publish/MATR.dll ./
cp _src/bin/AnyCPU/Release/publish/MATR.exe ./
```

> ⚠️ `_src/bin/AnyCPU/Release/MFAAvalonia.Core.dll` 是桌面项目拷贝的旧缓存，文件大小和 `_src/MFAAvalonia/bin/Release/net10.0/` 不同，**必须从项目自身输出目录拷贝**。

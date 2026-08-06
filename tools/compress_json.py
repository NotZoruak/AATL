"""压缩 MATR pipeline JSON 中四元素数组和单键值对到单行，并删除空的 param。"""
import json, sys, glob, os


def clean_empty_param(obj):
    """递归删除所有值为 {} 的 "param" 键。"""
    if isinstance(obj, dict):
        return {k: clean_empty_param(v) for k, v in obj.items() if not (k == "param" and v == {})}
    elif isinstance(obj, list):
        return [clean_empty_param(v) for v in obj]
    return obj


def fmt(obj, indent=0):
    sp = "  " * indent
    sp1 = "  " * (indent + 1)

    if isinstance(obj, dict):
        if not obj:
            return "{}"
        # 单键值对且值非嵌套 → 单行
        if len(obj) == 1:
            k, v = next(iter(obj.items()))
            if not isinstance(v, (dict, list)):
                return f'{{"{k}": {fmt(v, 0)}}}'
        items = []
        for k, v in obj.items():
            items.append(f'{sp1}"{k}": {fmt(v, indent + 1)}')
        return "{\n" + ",\n".join(items) + f"\n{sp}}}"

    elif isinstance(obj, list):
        # 纯数字数组 → 单行
        if all(isinstance(x, (int, float)) for x in obj):
            inner = ", ".join(str(x) for x in obj)
            return f"[{inner}]"
        if not obj:
            return "[]"
        # 全字符串数组 → 超过 5 个保持多行，否则单行
        if all(isinstance(x, str) for x in obj):
            if len(obj) <= 5:
                inner = ", ".join(f'"{x}"' for x in obj)
                return f"[{inner}]"
            items = [f"{sp1}\"{x}\"" for x in obj]
            return "[\n" + ",\n".join(items) + f"\n{sp}]"
        items = [f"{sp1}{fmt(x, indent + 1)}" for x in obj]
        return "[\n" + ",\n".join(items) + f"\n{sp}]"

    elif isinstance(obj, bool):
        return "true" if obj else "false"
    elif isinstance(obj, str):
        return json.dumps(obj, ensure_ascii=False)
    elif isinstance(obj, (int, float)):
        return json.dumps(obj)
    elif obj is None:
        return "null"
    return json.dumps(obj, ensure_ascii=False)


if __name__ == "__main__":
    # 基于脚本位置推导项目根目录,保证从任意目录运行都能找到资源文件;
    # 显式传入路径参数时保持原样(相对当前目录)
    if sys.argv[1:]:
        paths = sys.argv[1:]
    else:
        project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        paths = sorted(glob.glob(os.path.join(project_root, "assets", "resource", "base", "pipeline", "*.json")))
    if not paths:
        print("错误: 未找到 pipeline JSON 文件,请检查项目结构或显式传入路径", file=sys.stderr)
        sys.exit(1)
    for path in paths:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        data = clean_empty_param(data)
        with open(path, "w", encoding="utf-8") as f:
            f.write(fmt(data) + "\n")
        print(f"Compressed: {path}")

"""压缩 MATR pipeline JSON 中四元素数组和单键值对到单行。"""
import json, sys


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
    paths = sys.argv[1:] or ["resource/base/pipeline/Sortie.json"]
    for path in paths:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        with open(path, "w", encoding="utf-8") as f:
            f.write(fmt(data) + "\n")
        print(f"Compressed: {path}")

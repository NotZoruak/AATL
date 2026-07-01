using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MFAAvalonia.Helper;

/// <summary>
/// 自动释放 IDisposable 元素的 ObservableCollection
/// 当元素被移除或集合被清空时，自动调用元素的 Dispose 方法
/// </summary>
/// <typeparam name="T">元素类型，必须实现 IDisposable</typeparam>
public class DisposableObservableCollection<T> : ObservableCollection<T> where T : IDisposable
{
    public DisposableObservableCollection()
    {
    }

    public DisposableObservableCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    public DisposableObservableCollection(List<T> list) : base(list)
    {
    }

    protected override void ClearItems()
    {
        // 在清空前，先释放所有元素
        foreach (var item in Items)
        {
            item?.Dispose();
        }
        base.ClearItems();
    }

    protected override void RemoveItem(int index)
    {
        // 在移除前，先释放该元素
        var item = Items[index];
        base.RemoveItem(index);
        item?.Dispose();
    }

    protected override void SetItem(int index, T item)
    {
        // 替换元素时，先释放旧元素
        var oldItem = Items[index];
        base.SetItem(index, item);
        oldItem?.Dispose();
    }

    /// <summary>
    /// 移除并释放指定范围的元素
    /// </summary>
    /// <param name="index">起始索引</param>
    /// <param name="count">要移除的数量</param>
    public void RemoveRange(int index, int count)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (count < 0 || index + count > Count)
            throw new ArgumentOutOfRangeException(nameof(count));

        // 收集要移除的元素
        var itemsToRemove = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            itemsToRemove.Add(Items[index + i]);
        }

        // 从后向前移除，避免索引变化问题
        for (var i = count - 1; i >= 0; i--)
        {
            Items.RemoveAt(index + i);
        }

        // 触发集合变更通知
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        // 释放移除的元素
        foreach (var item in itemsToRemove)
        {
            item?.Dispose();
        }
    }
}
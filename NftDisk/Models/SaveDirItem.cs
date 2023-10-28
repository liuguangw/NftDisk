using System;
using System.Reactive;
using ReactiveUI;

namespace Liuguang.NftDisk.Models;

/// <summary>
/// 复制或者移动文件时,可供选择的保存目录
/// </summary>
public class SaveDirItem : ModelBase
{
    public long ID { get; set; } = 0;
    public long ParentID { get; set; } = 0;
    public string Name { get; set; } = string.Empty;
    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set => this.RaiseAndSetIfChanged(ref _enabled, value);
    }
    private Action<long> _openAction;
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public SaveDirItem(Action<long> openAction)
    {
        _openAction = openAction;
        var canOpen = this.WhenAnyValue(item => item.Enabled);
        OpenCommand = ReactiveCommand.Create(DoOpen, canOpen);
    }

    private void DoOpen()
    {
        _openAction.Invoke(ID);
    }
}
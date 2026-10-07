namespace WinCleaner.Core.Models;

/// <summary>关联记录（构建"系统仍在使用"索引时用）。</summary>
public readonly record struct AssociationRecord(string Path, AssociationSource Source, string Name);

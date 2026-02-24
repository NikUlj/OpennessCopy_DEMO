#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OpennessCopy.Models;

public enum DbConfigMode
{
    GenerateOrAppend,
    UseExisting
}

public class DbConfig : INotifyPropertyChanged
{
    public DbConfigKey Key { get; set; }
    private string _displayName = "DB";
    private string _description = string.Empty;
    private DbConfigMode _mode = DbConfigMode.GenerateOrAppend;
    private string _manualName = string.Empty;
    private bool _appendIfExists = true;
    private PlcBlockInfo? _selectedBlock;
    public int BlockNumber { get; set; }
    public string? Path { get; set; }
    public bool IsNameOverridden { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value) return;
            _displayName = value;
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value) return;
            _description = value;
            OnPropertyChanged(nameof(Description));
        }
    }

    public DbConfigMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged(nameof(Mode));
        }
    }

    public string ManualName
    {
        get => _manualName;
        set
        {
            if (_manualName == value) return;
            _manualName = value;
            OnPropertyChanged(nameof(ManualName));
        }
    }

    public bool AppendIfExists
    {
        get => _appendIfExists;
        set
        {
            if (_appendIfExists == value) return;
            _appendIfExists = value;
            OnPropertyChanged(nameof(AppendIfExists));
        }
    }

    public PlcBlockInfo? SelectedBlock
    {
        get => _selectedBlock;
        set
        {
            if (_selectedBlock == value) return;
            _selectedBlock = value;
            OnPropertyChanged(nameof(SelectedBlock));
            if (Mode == DbConfigMode.UseExisting && _selectedBlock != null)
            {
                DisplayName = _selectedBlock.Name;
            }
        }
    }

    public List<DbVariableSpec>? Variables { get; set; }
    
    public override string ToString()
    {
        return Mode switch
        {
            DbConfigMode.GenerateOrAppend => $"Generate/append: {ManualName} (append if exists: {AppendIfExists})",
            DbConfigMode.UseExisting when SelectedBlock != null => $"Use existing: {SelectedBlock.Name}",
            DbConfigMode.UseExisting => "Use existing: not selected",
            _ => ManualName
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

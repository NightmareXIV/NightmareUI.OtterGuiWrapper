using ECommons.Configuration;
using OtterGui.Classes;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using System.IO;
using OtterGui.Raii;
using Dalamud.Interface.Colors;
using Dalamud.Interface;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using ECommons;
using ECommons.Logging;
using System.Diagnostics.CodeAnalysis;
using NightmareUI.OtterGuiWrapper.FileSystems;
using System.Numerics;
#pragma warning disable CS8618
#pragma warning disable

namespace NightmareUI.OtterGuiWrapper.FileSystems;
public sealed class GenericFileSystem<TData> : FileSystem<TData> where TData:class, IFileSystemStorage, new()
{
    string FilePath;
    public readonly FileSystemSelector Selector;
    public readonly ICollection<TData> DataStorage;
    public GenericFileSystem(ICollection<TData> dataStorage, string dataFilePrefix)
    {
        FilePath = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, dataFilePrefix + "FileSystem.json");
        this.DataStorage = dataStorage;
        EzConfig.OnSave += Save;
        try
        {
            var info = new FileInfo(FilePath);
            if (info.Exists)
            {
                this.Load(info, DataStorage, ConvertToIdentifier, ConvertToName);
            }
            Selector = new(this);
        }
        catch (Exception e)
        {
            e.Log();
        }
    }

    public bool Create(TData newItem, string newName, out string error)
    {
        if (DataStorage.Contains(newItem))
        {
            error = "This item already exists";
            return false;
        }
        while(DataStorage.Any(x => x.GUID == newItem.GUID))
        {
            newItem.GUID = Guid.NewGuid();
        }
        try
        {
            CreateLeaf(Root, newName, newItem);
        }
        catch(Exception e)
        {
            error = e.ToString();
            return false;
        }
				DataStorage.Add(newItem);
				newItem.SetCustomName(newName);
        error = "";
        return true;
		}

    public void DoDelete(TData item)
    {
        PluginLog.Debug($"Deleting {item.GUID}");
        DataStorage.Remove(item);
        if (FindLeaf(item, out var leaf))
        {
            this.Delete(leaf);
        }
        this.Save();
    }

    public bool FindLeaf(TData? item, [NotNullWhen(true)] out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<TData>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == item);
        return leaf != null;
    }

    public bool TryGetPathByID(Guid id, [NotNullWhen(true)] out string? path)
    {
        if (FindLeaf(DataStorage.FirstOrDefault(x => x.GUID == id), out var leaf))
        {
            path = leaf.FullName();
            return true;
        }
        path = default;
        return false;
    }

    private string ConvertToName(TData item)
    {
        PluginLog.Debug($"Request conversion of {item.GetCustomName()} {item.GUID} to name");
        return $"Unnamed " + item.GUID;
    }

    private string ConvertToIdentifier(TData item)
    {
        PluginLog.Debug($"Request conversion of {item.GetCustomName()} {item.GUID} to identifier");
        return item.GUID.ToString();
    }

    public void Save()
    {
        try
        {
            using var FileStream = new FileStream(FilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var StreamWriter = new StreamWriter(FileStream);
            this.SaveToFile(StreamWriter, SaveConverter, true);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error saving GenericFileSystem:");
            ex.Log();
        }
    }

    private (string, bool) SaveConverter(TData item, string arg2)
    {
        PluginLog.Debug($"Saving {item.GetCustomName()}  {item.GUID}");
        return (item.GUID.ToString(), true);
    }

    public class FileSystemSelector : FileSystemSelector<TData, FileSystemSelector.State>
    {
        string NewName = "";
        string? ClipboardText = null;
        TData? CloneItem = null;
        public override ISortMode<TData> SortMode => ISortMode<TData>.FoldersFirst;

        GenericFileSystem<TData> FS;
        public FileSystemSelector(GenericFileSystem<TData> fs) : base(fs, Svc.KeyState, new(), (e) => e.Log())
        {
            this.FS = fs;
            AddButton(NewItemButton, 0);
            AddButton(ImportButton, 10);
            AddButton(CopyToClipboardButton, 20);
            AddButton(DeleteButton, 1000);
        }

        protected override uint CollapsedFolderColor => ImGuiColors.DalamudViolet.ToUint();
        protected override uint ExpandedFolderColor => CollapsedFolderColor;

        protected override void DrawLeafName(Leaf leaf, in State state, bool selected)
        {
            var flag = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
            using var _ = ImRaii.TreeNode(leaf.Name + $"                                                       ", flag);
        }

        private void CopyToClipboardButton(Vector2 vector)
        {
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Copy.ToIconString(), vector, "Copy to clipboard.", Selected == null, true)) return;
            if (this.Selected != null)
            {
                var copy = this.Selected.JSONClone();
                copy.GUID = Guid.Empty;
                GenericHelpers.Copy(EzConfig.DefaultSerializationFactory.Serialize(copy, false));
            }
        }

        private void ImportButton(Vector2 size)
        {
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size, "Try to import an item from your clipboard.", false,
                    true))
                return;

            try
            {
                CloneItem = null;
                ClipboardText = GenericHelpers.Paste();
                ImGui.OpenPopup("##NewItem");
            }
            catch
            {
                Notify.Error("Could not import data from clipboard.");
            }
        }

        private void DeleteButton(Vector2 vector)
        {
            try
            {
                DeleteSelectionButton(vector, new DoubleModifier(ModifierHotkey.Control), "item", "items", FS.DoDelete);
            }
            catch(Exception e)
            {
                e.Log();
            }
        }

        private void NewItemButton(Vector2 size)
        {
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size, "Create new item", false,
                    true))
            {
                ClipboardText = null;
                CloneItem = null;
                ImGui.OpenPopup("##NewItem");
            }
        }

        private void DrawNewItemPopup()
        {
            if (!ImGuiUtil.OpenNameField("##NewItem", ref NewName))
                return;

            if (NewName == "")
            {
                Notify.Error($"Name can not be empty!");
                return;
            }

            if (ClipboardText != null)
            {
                try
                {
                    var newItem = EzConfig.DefaultSerializationFactory.Deserialize<TData>(ClipboardText);
                    if (newItem != null)
                    {
                        FS.Create(newItem, NewName, out _);
                    }
                    else
                    {
                        Notify.Error($"Invalid clipboard data");
                    }
                }
                catch (Exception e)
                {
                    e.LogVerbose();
                    Notify.Error($"Error: {e.Message}");
                }
            }
            else if (CloneItem != null)
            {

            }
            else
            {
                try
                {
                    var newItem = new TData();
                    FS.Create(newItem, NewName, out _);
                }
                catch (Exception e)
                {
                    e.LogVerbose();
                    Notify.Error($"This name already exists!");
                }
            }

            NewName = string.Empty;
        }

        protected override void DrawPopups()
        {
            DrawNewItemPopup();
        }

        public record struct State { }
        protected override bool ApplyFilters(IPath path)
        {
            return FilterValue.Length > 0 && !path.FullName().Contains(this.FilterValue, StringComparison.OrdinalIgnoreCase);
        }

    }
}

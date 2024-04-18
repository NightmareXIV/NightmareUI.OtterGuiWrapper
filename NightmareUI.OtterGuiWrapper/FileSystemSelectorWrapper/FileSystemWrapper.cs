using ECommons.Configuration;
using OtterGui.Classes;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using System.IO;
using OtterGui.Raii;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Interface.Colors;
using ECommons;
using ImGuiNET;
using System.Numerics;
using Dalamud.Interface;
using ECommons.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using ECommons.DalamudServices;

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
public sealed class FileSystemSelectorWrapper<T> : FileSystem<T>, IDisposable where T:class, IFileSystemSelectorItem
{
    public readonly FileSystemSelector<FileSystem<T>> Selector;
    public readonly FileSystemDataStorage<T> Storage;
    public FileSystemSelectorWrapper(FileSystemDataStorage<T> storage)
    {
        this.Storage = storage;
        try
        {
            this.Load(BuildJObject(), Storage.Storage, ConvertToIdentifier, ConvertToName);
            Selector = new(this);
        }
        catch (Exception e)
        {
            e.Log();
        }
    }

    public void Dispose()
    {
        
    }

    public void DoDelete(T status)
    {
        PluginLog.Debug($"Deleting {status.ID}");
        C.SavedStatuses.Remove(status);
        if (FindLeaf(status, out var leaf))
        {
            this.Delete(leaf);
        }
        this.Save();
    }

    public bool FindLeaf(T? item, [NotNullWhen(true)]out Leaf? leaf)
    {
        leaf = Root.GetAllDescendants(ISortMode<T>.Lexicographical)
            .OfType<Leaf>()
            .FirstOrDefault(l => l.Value == item);
        return leaf != null;
    }

    private string ConvertToName(T item)
    {
        var name = item.GetName();
        return $"Unnamed " + item.FileSystemData.Guid.ToString();
    }

    private string ConvertToIdentifier(T item)
    {
        return item.FileSystemData.Guid.ToString();
    }

    private JObject BuildJObject()
    {
        ShuffleGuid();
        var data = new FileSystemDataFormat()
        {
            EmptyFolders = Storage.EmptyFolderStorage,
            Data = Storage.Storage.ToDictionary(x => x.FileSystemData.Guid.ToString(), x => x.FileSystemData.Path),
        };
        var obj = new JObject(data);
        return obj;
    }

    private void ShuffleGuid()
    {
        foreach (var x in Storage.Storage)
        {
            Guid newGuid;
            do
            {
                newGuid = Guid.NewGuid();
            }
            while (Storage.Storage.Any(x => x.FileSystemData.Guid == newGuid));
            x.FileSystemData.Guid = newGuid;
        }
    }

    public void Save()
    {
        try
        {
            ShuffleGuid();
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            this.SaveToFile(writer, SaveConverter, true);
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream);
            var data = JsonConvert.DeserializeObject<FileSystemDataFormat>(reader.ReadToEnd());
            if (data == null)
            {
                PluginLog.Error($"Could not save empty folder data.");
            }
            else
            {
                Storage.EmptyFolderStorage = data.EmptyFolders;
            }
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    private (string, bool) SaveConverter(T item, string arg2)
    {
        PluginLog.Debug($"Saving {item.FileSystemData.Path}  {item.FileSystemData.Guid}");
        return (item.FileSystemData.Guid.ToString(), true);
    }

    public class FileSystemSelector<F> : FileSystemSelector<T, FileSystemSelector<F>.State> where F : FileSystem<T>
    {
        string NewName = "";
        string ClipboardText = null;
        T Clone = null;
        F FS;

        public override ISortMode<T> SortMode => ISortMode<T>.FoldersFirst;

        public FileSystemSelector(F fs) : base(fs, Svc.KeyState, new(), (e) => e.Log())
        {
            FS = fs;
            AddButton(NewMoodleButton, 0);
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
                Copy(EzConfig.DefaultSerializationFactory.Serialize(copy, false));
            }
        }

        private void ImportButton(Vector2 size)
        {
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size, "Try to import a moodle from your clipboard.", false,
                    true))
                return;

            try
            {
                Clone = null;
                ClipboardText = Paste();
                ImGui.OpenPopup("##NewMoodle");
            }
            catch
            {
                Notify.Error("Could not import data from clipboard.");
            }
        }

        private void DeleteButton(Vector2 vector)
        {
            DeleteSelectionButton(vector, new DoubleModifier(ModifierHotkey.Control), "moodle", "moodles", FS.DoDelete);
        }

        private void NewMoodleButton(Vector2 size)
        {
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size, "Create new status", false,
                    true))
            {
                ClipboardText = null;
                Clone = null;
                ImGui.OpenPopup("##NewMoodle");
            }
        }

        private void DrawNewMoodlePopup()
        {
            if (!ImGuiUtil.OpenNameField("##NewMoodle", ref NewName))
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
                    var newStatus = EzConfig.DefaultSerializationFactory.Deserialize<T>(ClipboardText);
                    if (newStatus.IsNotNull())
                    {
                        FS.CreateLeaf(FS.Root, NewName, newStatus);
                        C.SavedStatuses.Add(newStatus);
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
            else if (Clone != null)
            {

            }
            else
            {
                try
                {
                    var newStatus = new T();
                    FS.CreateLeaf(FS.Root, NewName, newStatus);
                    C.SavedStatuses.Add(newStatus);
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
            DrawNewMoodlePopup();
        }

        public record struct State { }
        protected override bool ApplyFilters(IPath path)
        {
            return FilterValue.Length > 0 && !path.FullName().Contains(this.FilterValue, StringComparison.OrdinalIgnoreCase);
        }

    }
}
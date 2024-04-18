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
using ECommons.ImGuiMethods;
using System.Collections.Specialized;
#pragma warning disable CS8618
#pragma warning disable

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
public sealed class FileSystemSelectorWrapper<T> : FileSystem<T>, IDisposable where T:class, IFileSystemSelectorItem, new()
{
    public readonly FileSystemSelector Selector;
    public readonly FileSystemDataStorage<T> Storage;
    public FileSystemSelectorWrapper(FileSystemDataStorage<T> storage)
    {
        this.Storage = storage;
        Storage.Storage.CollectionChanged += this.Storage_CollectionChanged;
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

    private void Storage_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PluginLog.Debug($"Collection has changed, begin file system rebuild! Event={e.Action}");
        this.Save();
        this.Load(BuildJObject(), Storage.Storage, ConvertToIdentifier, ConvertToName);
    }

    public void Dispose()
    {
        
    }

    public void DoDelete(T item)
    {
        Storage.Storage.Remove(item);
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
        var text = JsonConvert.SerializeObject(data);
        PluginLog.Information(text);
        var obj = JObject.Parse(text);
        return obj;
    }

    private void ShuffleGuid()
    {
        foreach (var x in Storage.Storage)
        {
            if (x.FileSystemData == null) x.FileSystemData = new();
            Guid newGuid;
            do
            {
                newGuid = Guid.NewGuid();
            }
            while (Storage.Storage.Any(x => x.FileSystemData?.Guid == newGuid));
            x.FileSystemData.Guid = newGuid;
        }
    }

    public void Save()
    {
        try
        {
            ShuffleGuid();
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, null, -1, true);
            this.SaveToFile(writer, SaveConverter, true);
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream);
            var text = reader.ReadToEnd();
            PluginLog.Information($"{text}");
            var data = JsonConvert.DeserializeObject<FileSystemDataFormat>(text);
            if (data == null)
            {
                PluginLog.Error($"Could not save empty folder data.");
            }
            else
            {
                Storage.EmptyFolderStorage.Clear();
                Storage.EmptyFolderStorage.AddRange(data.EmptyFolders);
                foreach(var x in data.Data)
                {
                    if(Guid.TryParse(x.Key, out var guid) && Storage.Storage.TryGetFirst(z => z.FileSystemData.Guid == guid, out var value))
                    {
                        value.FileSystemData.Path = x.Value;
                    }
                }
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

    public class FileSystemSelector : FileSystemSelector<T, FileSystemSelector.State> 
    {
        string NewName = "";
        string? ClipboardText = null;
        T? CloneObject = null;
        FileSystemSelectorWrapper<T> FS;

        public override ISortMode<T> SortMode => ISortMode<T>.FoldersFirst;

        public FileSystemSelector(FileSystemSelectorWrapper<T> fs) : base(fs, Svc.KeyState, new(), (e) => e.Log())
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
            flag |= ImGuiTreeNodeFlags.SpanFullWidth;
            using var _ = ImRaii.TreeNode((leaf.Value.GetName() ?? leaf.Name) + $"##{leaf.Value.FileSystemData.Guid}", flag);
        }

        private void CopyToClipboardButton(Vector2 vector)
        {
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Copy.ToIconString(), vector, "Copy to clipboard.", Selected == null, true)) return;
            if (this.Selected != null)
            {
                GenericHelpers.Copy(EzConfig.DefaultSerializationFactory.Serialize(this.Selected, false));
            }
        }

        private void ImportButton(Vector2 size)
        {
            if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.FileImport.ToIconString(), size, "Try to import an item from your clipboard.", false,
                    true))
                return;

            try
            {
                CloneObject = null;
                ClipboardText = GenericHelpers.Paste();
                ImGui.OpenPopup("##NewObject");
            }
            catch
            {
                Notify.Error("Could not import data from clipboard.");
            }
        }

        private void DeleteButton(Vector2 vector)
        {
            DeleteSelectionButton(vector, new DoubleModifier(ModifierHotkey.Control), "item", "items", FS.DoDelete);
        }

        private void NewMoodleButton(Vector2 size)
        {
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size, "Create new item", false,
                    true))
            {
                ClipboardText = null;
                CloneObject = null;
                ImGui.OpenPopup("##NewItem");
            }
        }

        private void DrawNewMoodlePopup()
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
                    var newObject = EzConfig.DefaultSerializationFactory.Deserialize<T>(ClipboardText);
                    if (newObject != null)
                    {
                        FS.Storage.Storage.Add(newObject);
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
            else if (CloneObject != null)
            {

            }
            else
            {
                try
                {
                    var newItem = new T();
                    FS.Storage.Storage.Add(newItem);
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
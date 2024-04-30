using OtterGui.Filesystem;
using OtterGui.FileSystem.Selector;
using System.IO;
using OtterGui.Raii;
using Dalamud.Interface.Colors;
using ECommons.DalamudServices;
using ImGuiNET;
using System;
using System.Collections.Generic;
using ECommons;
using Newtonsoft.Json.Linq;
using Dalamud.Logging;
using System.Linq;
using Newtonsoft.Json;
using Lumina.Excel.GeneratedSheets;


#pragma warning disable CS8618
#pragma warning disable

namespace NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
public sealed class ConfigFileSystem<TData> : FileSystem<TData> where TData : ConfigFileSystemEntry, new()
{
		public readonly FileSystemSelector Selector;
		public readonly ICollection<TData> DataStorage;
		public ConfigFileSystem(ICollection<TData> dataStorage)
		{
				this.DataStorage = dataStorage;
				try
				{
						var identifiers = new Dictionary<string, Dictionary<string, string>>()
						{
								["Data"] = DataStorage.ToDictionary(x => x.Path, x => x.Path)
						};
						PluginLog.Information($"{JsonConvert.SerializeObject(identifiers)}");
						this.Load(JObject.Parse(JsonConvert.SerializeObject(identifiers)), DataStorage, (x) => x.Path, (x) => x.Path);
						Selector = new(this);
				}
				catch (Exception e)
				{
						e.Log();
				}
		}

		public void Draw(float width = 200f)
		{
				Selector.Draw(200f);
				ImGui.SameLine();
				if (ImGui.BeginChild("ARChild"))
				{
						try
						{
								if (Selector.Selected == null)
								{
										Selector.SelectByValue(DataStorage.First());
								}
								Selector.Selected.Draw();
						}
						catch (Exception e)
						{
								e.Log();
						}
				}
				ImGui.EndChild();
		}

		public class FileSystemSelector : FileSystemSelector<TData, FileSystemSelector.State>
		{
				public override ISortMode<TData> SortMode => ISortMode<TData>.InternalOrder;

				ConfigFileSystem<TData> FS;
				public FileSystemSelector(ConfigFileSystem<TData> fs) : base(fs, Svc.KeyState, new(), (e) => e.Log())
				{
						this.FS = fs;
						this.RemoveButton(FolderAddButton);
				}

				protected override uint CollapsedFolderColor => ImGuiColors.DalamudViolet.ToUint();
				protected override uint ExpandedFolderColor => CollapsedFolderColor;

				protected override void DrawLeafName(Leaf leaf, in State state, bool selected)
				{
						var flag = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
						flag |= ImGuiTreeNodeFlags.SpanFullWidth;
						using var _ = ImRaii.TreeNode(leaf.Name, flag);
				}

				public record struct State { }
				protected override bool ApplyFilters(IPath path)
				{
						return FilterValue.Length > 0 && !path.FullName().Contains(this.FilterValue, StringComparison.OrdinalIgnoreCase);
				}
		}
}

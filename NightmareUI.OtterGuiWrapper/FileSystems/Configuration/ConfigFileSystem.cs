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
using System.Numerics;
using Dalamud.Interface;
using OtterGui;
using System.Drawing;
using ECommons.Reflection;
using ECommons.ImGuiMethods;
using System.Security.Policy;





#pragma warning disable CS8618
#pragma warning disable

namespace NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
public sealed class ConfigFileSystem<TData> : FileSystem<TData> where TData : ConfigFileSystemEntry, new()
{
		public FileSystemSelector Selector { get; private set; }
		public readonly ICollection<TData> DataStorage;
		public ConfigFileSystem(ICollection<TData> dataStorage)
		{
				this.DataStorage = dataStorage;
				try
				{
						Reload();
						this.Changed += ConfigFileSystem_Changed;
				}
				catch (Exception e)
				{
						e.Log();
				}
		}

		private void ConfigFileSystem_Changed(FileSystemChangeType type, IPath changedObject, IPath? previousParent, IPath? newParent)
		{
				if(type == FileSystemChangeType.ObjectMoved)
				{
						Reload();
				}
		}

		private string SelectedPath;

		public void Reload()
		{
				if(Selector != null)
				{
						SelectedPath = Selector.Selected?.Path;
				}
				var identifiers = new Dictionary<string, Dictionary<string, string>>()
				{
						["Data"] = DataStorage.ToDictionary(x => x.Path, x => x.Path)
				};
				PluginLog.Information($"{JsonConvert.SerializeObject(identifiers)}");
				this.Load(JObject.Parse(JsonConvert.SerializeObject(identifiers)), DataStorage, (x) => x.Path, (x) => x.Path);
				Selector = new(this);
				if(SelectedPath != null)
				{
						var value = DataStorage.FirstOrDefault(x => x.Path == SelectedPath);
						if (value != null) Selector.SelectByValue(value);
				}
		}



		public Action<Vector2> DrawButton = (size) =>
		{
				var col = ImGuiEx.Vector4FromRGB(0x002766, 0.9f);
				ImGui.PushStyleColor(ImGuiCol.Button, col);
				ImGui.PushStyleColor(ImGuiCol.ButtonActive, col);
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, col);
				if (ImGui.Button("Support on Patreon", size))
				{
						GenericHelpers.ShellStart("https://www.patreon.com/NightmareXIV");
				}
				if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
				//ImGuiEx.Tooltip($"Like the plugin? Consider supporting {DalamudReflector.GetPluginName()} by subscribing to Patreon and receive:\n- Exclusive content\n- Early access\n- Priority support\n...and much more!");
				ImGui.PopStyleColor(3);
		};

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
								Selector.Selected?.Draw();
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
				public string Filter => this.FilterValue;
				public override ISortMode<TData> SortMode => ISortMode<TData>.InternalOrder;

				ConfigFileSystem<TData> FS;
				public FileSystemSelector(ConfigFileSystem<TData> fs) : base(fs, Svc.KeyState, new(), (e) => e.Log())
				{
						this.FS = fs;
						this.RemoveButton(FolderAddButton);
						this.AddButton(DrawButtonInt, 1);
						UnsubscribeRightClickFolder(ExpandAllDescendants);
						UnsubscribeRightClickFolder(CollapseAllDescendants);
						UnsubscribeRightClickFolder(DissolveFolder);
						UnsubscribeRightClickFolder(RenameFolder);
						UnsubscribeRightClickLeaf(RenameLeaf);
				}

				private void DrawButtonInt(Vector2 size) => FS.DrawButton(size);

				protected override uint CollapsedFolderColor => ImGuiColors.DalamudViolet.ToUint();
				protected override uint ExpandedFolderColor => CollapsedFolderColor;


				protected override void DrawLeafName(Leaf leaf, in State state, bool selected)
				{
						var flag = selected ? ImGuiTreeNodeFlags.Selected | LeafFlags : LeafFlags;
						flag |= ImGuiTreeNodeFlags.SpanFullWidth;
						var col = leaf.Value.GetColor();
						if (col != null) ImGui.PushStyleColor(ImGuiCol.Text, col.Value);
						using var _ = ImRaii.TreeNode(leaf.Name, flag);
						if (col != null) ImGui.PopStyleColor();
				}

				public record struct State { }
				protected override bool ApplyFilters(IPath path)
				{
						return false;
				}
				protected override bool ApplyFiltersAndState(IPath path, out ConfigFileSystem<TData>.FileSystemSelector.State state)
				{
						return false;
				}
		}
}

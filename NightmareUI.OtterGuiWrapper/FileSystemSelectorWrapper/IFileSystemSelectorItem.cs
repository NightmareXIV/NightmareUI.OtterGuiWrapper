using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
public interface IFileSystemSelectorItem
{
    /// <summary>
    /// Data storage. Simply provide default getter and setter without default value.
    /// </summary>
    public FileSystemData FileSystemData { get; set; }

    /// <summary>
    /// If you would like to display custom name, implement this function that will return display name. Otherwise, let it return null - in that case name will be derived automatically from file system path. This is called multiple times per item per frame, so this function implementation must be cheap.
    /// </summary>
    /// <returns></returns>
    public string? GetName();

    /// <summary>
    /// In the event user renames item via OtterGUI interface, this method will be called. You can choose to either react to it or dismiss it.
    /// </summary>
    /// <param name="name"></param>
    public void SetName(string name);
}

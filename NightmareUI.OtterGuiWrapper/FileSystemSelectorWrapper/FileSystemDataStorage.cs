using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
[Serializable]
public class FileSystemDataStorage<T>
{
    public ObservableCollection<T> Storage { get; private set; }
    public List<string> EmptyFolderStorage { get; private set; } =  [];

    public FileSystemDataStorage(ObservableCollection<T> storage)
    {
        this.Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public FileSystemDataStorage()
    {
        this.Storage = [];
    }
}

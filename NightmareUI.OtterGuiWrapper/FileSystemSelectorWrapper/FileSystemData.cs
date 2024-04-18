using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
[Serializable]
public class FileSystemData
{
    public string Path = "";
    internal Guid Guid = Guid.Empty;
}

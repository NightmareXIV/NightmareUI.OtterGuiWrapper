using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystemSelectorWrapper;
[Serializable]
internal class FileSystemDataFormat
{
    public Dictionary<string, string> Data = [];
    public List<string> EmptyFolders = [];
}
